using System;
using System.Collections.Generic;
using System.Text;
using Chemistry;
using Omics;
using Omics.Fragmentation;
using Omics.Fragmentation.Peptide;
using Omics.SpectralMatch.MslSpectralLibrary;
using Proteomics;
using Proteomics.ProteolyticDigestion;
using Readers.SpectralLibrary;

namespace EngineLayer.ParallelSearch
{
    /// <summary>
    /// Reads a MetaMorpheus <c>.msl</c> spectral library as a PEPTIDE SOURCE for the parallel search,
    /// using the library the way the binary format was designed for: <see cref="MslLibrary.LoadIndexOnly"/>
    /// keeps fragments on disk; the precursor index (mass / charge / iRT metadata, NO fragment I/O) is
    /// filtered on TWO orthogonal axes — precursor mass AND retention time — and only the surviving
    /// CANDIDATES have their fragments fetched on demand.
    ///
    /// Retention time is the axis that makes the filter selective: precursor mass alone is degenerate when
    /// tens of thousands of dense experimental precursors saturate the mass window, but a peptide's predicted
    /// elution (stored as Chronologer iRT in the .msl, calibrated to this run from the base search) collides
    /// with far fewer scans. Entries with iRT==0 (predictor could not place them) fall back to mass-only so
    /// they are never lost.
    ///
    /// Each candidate's stored float fragments are turned into <see cref="Product"/>s directly (no
    /// re-fragmentation). Accession handling: one shared <see cref="Protein"/> per accession (concatenation
    /// of its candidate peptides with per-peptide offsets) so parsimony/coverage stay correct and in-bounds.
    /// </summary>
    public static class MslPeptideReader
    {
        private const double MassPreFilterMarginDa = 0.01;

        /// <summary>RT↔iRT calibration and learned precursor tolerance applied to the candidate pre-filter.</summary>
        public readonly struct CandidatePriors
        {
            public readonly double[] SortedScanMasses;  // experimental precursor masses, ASCENDING
            public readonly double[] ScanRetentionTimes; // RT of each scan, SAME order as SortedScanMasses
            public readonly double PrecursorTolPpm;     // learned precursor tolerance (ppm)
            public readonly double RtSlope, RtIntercept; // observedRT = RtSlope*iRT + RtIntercept
            public readonly double RtWindowMin;          // +/- window in observed-RT minutes (k * residualSD)

            public CandidatePriors(double[] sortedScanMasses, double[] scanRetentionTimes,
                double precursorTolPpm, double rtSlope, double rtIntercept, double rtWindowMin)
            {
                SortedScanMasses = sortedScanMasses;
                ScanRetentionTimes = scanRetentionTimes;
                PrecursorTolPpm = precursorTolPpm;
                RtSlope = rtSlope;
                RtIntercept = rtIntercept;
                RtWindowMin = rtWindowMin;
            }
        }

        private readonly struct PendingPeptide
        {
            public readonly string FullSequence;
            public readonly int Start;
            public readonly int Length;
            public readonly List<Product> Fragments;
            public PendingPeptide(string fullSequence, int start, int length, List<Product> fragments)
            { FullSequence = fullSequence; Start = start; Length = length; Fragments = fragments; }
        }

        private sealed class AccessionGroup
        {
            public readonly StringBuilder Sequence = new();
            public readonly List<PendingPeptide> Peptides = new();
            public bool IsDecoy;
        }

        public static List<(IBioPolymerWithSetMods Peptide, List<Product> Fragments)> ReadPeptides(
            string mslPath, string databaseName, CandidatePriors priors)
        {
            var mods = GlobalVariables.AllModsKnownDictionary;
            var digestionParams = new DigestionParams("trypsin", maxMissedCleavages: 0);
            string fallbackAccession = $"{databaseName}_UNKNOWN";
            var groups = new Dictionary<string, AccessionGroup>();

            int totalEntries = 0, massCandidates = 0, massRtCandidates = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            long loadMs, getEntryTicks = 0, buildTicks = 0;
            var swPhase = System.Diagnostics.Stopwatch.StartNew();
            var candidateIdx = new List<int>();
            using (var library = MslLibrary.LoadIndexOnly(mslPath))
            {
                loadMs = swPhase.ElapsedMilliseconds; // cost of LoadIndexOnly (metadata read + sort)

                // PASS 1 — filter on mass + RT using ONLY the in-memory index (no fragment I/O).
                foreach (var idx in library.QueryMzWindow(float.MinValue, float.MaxValue))
                {
                    totalEntries++;
                    if (IsCandidate(idx.PrecursorMz, idx.Charge, idx.Irt, priors, out bool massMatched))
                    {
                        massRtCandidates++;
                        candidateIdx.Add(idx.PrecursorIdx);
                    }
                    if (massMatched)
                        massCandidates++;
                }

                // PASS 2 — fetch fragments in ON-DISK (PrecursorIdx/write) order, not the m/z order the
                // window query yielded. Fragment blocks are stored in PrecursorIdx order, so a sorted walk
                // reads the file front-to-back (sequential, OS read-ahead) instead of one random seek per
                // candidate — the dominant reader cost. Result order doesn't affect IDs/scores; the shared
                // protein concatenation just follows this order.
                candidateIdx.Sort();
                foreach (int pid in candidateIdx)
                {
                    long t0 = swPhase.ElapsedTicks;
                    MslLibraryEntry entry = library.GetEntry(pid);
                    getEntryTicks += swPhase.ElapsedTicks - t0;
                    if (entry is null) continue;

                    string fullSequence = entry.FullSequence;
                    string baseSequence = IBioPolymerWithSetMods.GetBaseSequenceFromFullSequence(fullSequence);
                    string accession = string.IsNullOrEmpty(entry.ProteinAccession) ? fallbackAccession : entry.ProteinAccession;

                    if (!groups.TryGetValue(accession, out var group))
                    {
                        group = new AccessionGroup { IsDecoy = entry.IsDecoy };
                        groups[accession] = group;
                    }

                    int start = group.Sequence.Length + 1;
                    group.Sequence.Append(baseSequence);
                    long t1 = swPhase.ElapsedTicks;
                    // Lean libraries store no fragments → return null so the engine fragments the peptide on
                    // the fly (cheap, and matches the search's exact dissociation/precision). Full libraries
                    // build Products from the stored float ions.
                    List<Product> built = entry.MatchedFragmentIons is { Count: > 0 }
                        ? BuildProducts(entry.MatchedFragmentIons)
                        : null;
                    buildTicks += swPhase.ElapsedTicks - t1;
                    group.Peptides.Add(new PendingPeptide(fullSequence, start, baseSequence.Length, built));
                }
            }
            double getEntryMs = getEntryTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double buildMs = buildTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

            // Per-database candidate-filter diagnostics. Opt-in (MM_PARALLELSEARCH_DIAG=1) so it is
            // available when profiling the .msl reader but silent in normal runs.
            if (Environment.GetEnvironmentVariable("MM_PARALLELSEARCH_DIAG") == "1")
                Console.WriteLine($"MSL_RT {databaseName}: entries={totalEntries} massCand={massCandidates} " +
                    $"({Pct(massCandidates, totalEntries)}%) massRtCand={massRtCandidates} ({Pct(massRtCandidates, totalEntries)}%) " +
                    $"load={loadMs}ms getEntry={getEntryMs:F0}ms build={buildMs:F0}ms " +
                    $"tolPpm={priors.PrecursorTolPpm:F1} rtWin=+/-{priors.RtWindowMin:F1}min ms={sw.ElapsedMilliseconds}");

            var result = new List<(IBioPolymerWithSetMods, List<Product>)>();
            foreach (var kvp in groups)
            {
                var protein = new Protein(kvp.Value.Sequence.ToString(), kvp.Key, isDecoy: kvp.Value.IsDecoy);
                foreach (var pending in kvp.Value.Peptides)
                {
                    var peptide = new PeptideWithSetModifications(
                        pending.FullSequence, mods, p: protein, digestionParams: digestionParams,
                        oneBasedStartResidueInProtein: pending.Start,
                        oneBasedEndResidueInProtein: pending.Start + pending.Length - 1,
                        missedCleavages: 0);
                    result.Add((peptide, pending.Fragments));
                }
            }
            return result;
        }

        /// <summary>
        /// Reads a MERGED index (many databases in one .msl, each entry's accession stamped "db|accession")
        /// with ONE open, runs the mass+RT candidate filter ONCE over all entries, and returns the surviving
        /// candidate peptides GROUPED BY their source database. The per-database lists are then searched
        /// INDEPENDENTLY (each against the shared base PSMs in its own copy) exactly as in the per-file path —
        /// databases never compete; this only collapses 1000s of file opens into one shared in-memory index.
        /// </summary>
        public static Dictionary<string, List<(IBioPolymerWithSetMods Peptide, List<Product> Fragments)>>
            ReadCandidatesGroupedByDatabase(string mergedMslPath, CandidatePriors priors)
        {
            var mods = GlobalVariables.AllModsKnownDictionary;
            var digestionParams = new DigestionParams("trypsin", maxMissedCleavages: 0);

            // dbTag -> (accession-within-db -> shared-protein layout)
            var byDb = new Dictionary<string, Dictionary<string, AccessionGroup>>();
            var candidateIdx = new List<int>();
            int totalEntries = 0, massRtCandidates = 0;
            var swPhase = System.Diagnostics.Stopwatch.StartNew();
            long loadMs;

            using (var library = MslLibrary.LoadIndexOnly(mergedMslPath))
            {
                loadMs = swPhase.ElapsedMilliseconds;
                // PASS 1 — mass+RT filter over the whole merged index (no fragment I/O).
                foreach (var idx in library.QueryMzWindow(float.MinValue, float.MaxValue))
                {
                    totalEntries++;
                    if (IsCandidate(idx.PrecursorMz, idx.Charge, idx.Irt, priors, out _))
                    {
                        massRtCandidates++;
                        candidateIdx.Add(idx.PrecursorIdx);
                    }
                }

                // PASS 2 — fetch candidates in on-disk order (sequential), group by source database.
                candidateIdx.Sort();
                foreach (int pid in candidateIdx)
                {
                    MslLibraryEntry entry = library.GetEntry(pid);
                    if (entry is null) continue;

                    var (dbTag, accession) = ParseDbTagAndAccession(entry.ProteinAccession);

                    string fullSequence = entry.FullSequence;
                    string baseSequence = IBioPolymerWithSetMods.GetBaseSequenceFromFullSequence(fullSequence);

                    if (!byDb.TryGetValue(dbTag, out var accGroups))
                    {
                        accGroups = new Dictionary<string, AccessionGroup>();
                        byDb[dbTag] = accGroups;
                    }
                    if (!accGroups.TryGetValue(accession, out var group))
                    {
                        group = new AccessionGroup { IsDecoy = entry.IsDecoy };
                        accGroups[accession] = group;
                    }
                    int start = group.Sequence.Length + 1;
                    group.Sequence.Append(baseSequence);
                    List<Product> built = entry.MatchedFragmentIons is { Count: > 0 } ? BuildProducts(entry.MatchedFragmentIons) : null;
                    group.Peptides.Add(new PendingPeptide(fullSequence, start, baseSequence.Length, built));
                }
            }

            if (Environment.GetEnvironmentVariable("MM_PARALLELSEARCH_DIAG") == "1")
                Console.WriteLine($"MSL_MERGED: entries={totalEntries} candidates={massRtCandidates} " +
                    $"({Pct(massRtCandidates, totalEntries)}%) databases={byDb.Count} load={loadMs}ms total={swPhase.ElapsedMilliseconds}ms");

            // Materialize per-database peptide lists (each database is searched independently downstream).
            var result = new Dictionary<string, List<(IBioPolymerWithSetMods, List<Product>)>>(byDb.Count);
            foreach (var dbKvp in byDb)
            {
                var list = new List<(IBioPolymerWithSetMods, List<Product>)>();
                foreach (var accKvp in dbKvp.Value)
                {
                    var protein = new Protein(accKvp.Value.Sequence.ToString(), accKvp.Key, isDecoy: accKvp.Value.IsDecoy);
                    foreach (var pending in accKvp.Value.Peptides)
                    {
                        var peptide = new PeptideWithSetModifications(
                            pending.FullSequence, mods, p: protein, digestionParams: digestionParams,
                            oneBasedStartResidueInProtein: pending.Start,
                            oneBasedEndResidueInProtein: pending.Start + pending.Length - 1,
                            missedCleavages: 0);
                        list.Add((peptide, pending.Fragments));
                    }
                }
                result[dbKvp.Key] = list;
            }
            return result;
        }

        private static double Pct(int n, int d) => d == 0 ? 0 : Math.Round(100.0 * n / d, 1);

        /// <summary>First index i where sorted[i] >= value (sorted ascending).</summary>
        internal static int LowerBound(double[] sorted, double value)
        {
            int i = Array.BinarySearch(sorted, value);
            return i < 0 ? ~i : i;
        }

        /// <summary>
        /// The .msl candidate pre-filter (extracted for testing): does any experimental scan match this library
        /// entry on BOTH precursor mass (within the learned ppm window) AND retention time (within the calibrated
        /// window of the entry's predicted RT)? An iRT of 0 (unpredicted) keeps the entry on mass alone.
        /// <paramref name="massMatched"/> reports whether the mass axis alone matched (for diagnostics).
        /// </summary>
        internal static bool IsCandidate(double precursorMz, int charge, float irt, in CandidatePriors priors, out bool massMatched)
        {
            massMatched = false;
            double neutralMass = precursorMz.ToMass(charge);
            double tolDa = neutralMass * priors.PrecursorTolPpm * 1e-6 + MassPreFilterMarginDa;
            int a = LowerBound(priors.SortedScanMasses, neutralMass - tolDa);
            double hi = neutralMass + tolDa;
            if (a >= priors.SortedScanMasses.Length || priors.SortedScanMasses[a] > hi)
                return false; // no mass match at all
            massMatched = true;

            if (irt == 0f)
                return true; // unpredicted RT -> keep on mass alone
            double predRt = priors.RtSlope * irt + priors.RtIntercept;
            for (int i = a; i < priors.SortedScanMasses.Length && priors.SortedScanMasses[i] <= hi; i++)
                if (Math.Abs(priors.ScanRetentionTimes[i] - predRt) <= priors.RtWindowMin)
                    return true;
            return false;
        }

        /// <summary>
        /// Splits a merged-index accession stamped "db|accession" into its (dbTag, accession) parts. An entry
        /// with no bar falls back to dbTag "UNKNOWN"; an empty accession becomes "&lt;dbTag&gt;_UNKNOWN".
        /// </summary>
        internal static (string dbTag, string accession) ParseDbTagAndAccession(string tagged)
        {
            tagged ??= "";
            int bar = tagged.IndexOf('|');
            string dbTag = bar > 0 ? tagged.Substring(0, bar) : "UNKNOWN";
            string accession = bar >= 0 ? tagged.Substring(bar + 1) : tagged;
            if (string.IsNullOrEmpty(accession))
                accession = dbTag + "_UNKNOWN";
            return (dbTag, accession);
        }

        private static List<Product> BuildProducts(List<MslFragmentIon> ions)
        {
            var products = new List<Product>(ions.Count);
            foreach (var ion in ions)
            {
                FragmentationTerminus terminus =
                    TerminusSpecificProductTypes.ProductTypeToFragmentationTerminus.TryGetValue(
                        ion.ProductType, out var t) ? t : FragmentationTerminus.None;
                products.Add(new Product(
                    ion.ProductType, terminus, ion.Mz.ToMass(ion.Charge), ion.FragmentNumber,
                    ion.ResiduePosition, ion.NeutralLoss, ion.SecondaryProductType, ion.SecondaryFragmentNumber));
            }
            return products;
        }
    }
}

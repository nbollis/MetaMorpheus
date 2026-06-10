using System;
using System.Linq;
using Chemistry;
using MassSpectrometry;

namespace EngineLayer.ParallelSearch.Scoring
{
    /// <summary>
    /// Read-only, flattened (struct-of-arrays) view of the experimental MS2 spectra used by
    /// the transient parallel search. Built ONCE from the shared, precursor-mass-sorted
    /// <see cref="Ms2ScanWithSpecificMass"/> array and reused, read-only, across every
    /// transient database search.
    ///
    /// This is the data the GPU keeps resident in VRAM: experimental fragment masses are
    /// concatenated into one sorted-per-scan array (CSR layout via <see cref="ScanFragmentOffsets"/>),
    /// with parallel charge/intensity arrays and per-scan TIC / precursor charge. The CPU scorer
    /// reconstructs <see cref="MatchedFragmentIon"/>s from the same data; the GPU scorer reads only
    /// the flat arrays.
    ///
    /// Per-scan fragment slices are sorted ascending by monoisotopic mass (the
    /// <see cref="Ms2ScanWithSpecificMass"/> constructor guarantees ExperimentalFragments is sorted),
    /// which is what makes the per-fragment closest-mass lookup a binary search.
    /// </summary>
    public sealed class SpectralScoringData
    {
        /// <summary>The original scans, kept for CPU-side MatchedFragmentIon reconstruction.</summary>
        public Ms2ScanWithSpecificMass[] Scans { get; }

        /// <summary>Precursor masses, ascending (mirrors ClassicSearchEngine.MyScanPrecursorMasses).</summary>
        public double[] ScanPrecursorMasses { get; }

        /// <summary>CSR offsets into the flat fragment arrays; length = Scans.Length + 1.</summary>
        public int[] ScanFragmentOffsets { get; }

        /// <summary>Experimental fragment monoisotopic masses, concatenated; each scan's slice is sorted ascending.</summary>
        public double[] FragmentMonoMasses { get; }

        /// <summary>Charge of each experimental fragment envelope (parallel to FragmentMonoMasses).</summary>
        public int[] FragmentCharges { get; }

        /// <summary>Representative intensity (first peak) of each experimental fragment envelope.</summary>
        public double[] FragmentIntensities { get; }

        /// <summary>Total ion current per scan (scoring normalization).</summary>
        public double[] ScanTotalIonCurrents { get; }

        /// <summary>Precursor charge per scan (fragment-charge acceptance filter).</summary>
        public int[] ScanPrecursorCharges { get; }

        public int ScanCount => Scans.Length;

        private SpectralScoringData(
            Ms2ScanWithSpecificMass[] scans, double[] scanPrecursorMasses, int[] scanFragmentOffsets,
            double[] fragmentMonoMasses, int[] fragmentCharges, double[] fragmentIntensities,
            double[] scanTotalIonCurrents, int[] scanPrecursorCharges)
        {
            Scans = scans;
            ScanPrecursorMasses = scanPrecursorMasses;
            ScanFragmentOffsets = scanFragmentOffsets;
            FragmentMonoMasses = fragmentMonoMasses;
            FragmentCharges = fragmentCharges;
            FragmentIntensities = fragmentIntensities;
            ScanTotalIonCurrents = scanTotalIonCurrents;
            ScanPrecursorCharges = scanPrecursorCharges;
        }

        /// <summary>
        /// Flatten the (already precursor-mass-sorted) scans into struct-of-arrays form.
        /// One-time cost amortized over all transient database searches.
        /// </summary>
        public static SpectralScoringData Build(Ms2ScanWithSpecificMass[] sortedScans)
        {
            if (sortedScans == null) throw new ArgumentNullException(nameof(sortedScans));

            int scanCount = sortedScans.Length;
            var precursorMasses = new double[scanCount];
            var offsets = new int[scanCount + 1];
            var tics = new double[scanCount];
            var precursorCharges = new int[scanCount];

            int totalFragments = 0;
            for (int i = 0; i < scanCount; i++)
            {
                var frags = sortedScans[i].ExperimentalFragments;
                totalFragments += frags?.Length ?? 0;
            }

            var monoMasses = new double[totalFragments];
            var charges = new int[totalFragments];
            var intensities = new double[totalFragments];

            int write = 0;
            for (int i = 0; i < scanCount; i++)
            {
                var scan = sortedScans[i];
                offsets[i] = write;
                precursorMasses[i] = scan.PrecursorMass;
                tics[i] = scan.TotalIonCurrent;
                precursorCharges[i] = scan.PrecursorCharge;

                var frags = scan.ExperimentalFragments;
                if (frags != null)
                {
                    for (int f = 0; f < frags.Length; f++)
                    {
                        var env = frags[f];
                        monoMasses[write] = env.MonoisotopicMass;
                        charges[write] = env.Charge;
                        // Mirrors the existing engine: closestExperimentalMass.Peaks.First().intensity
                        intensities[write] = env.Peaks.First().intensity;
                        write++;
                    }
                }
            }
            offsets[scanCount] = write;

            return new SpectralScoringData(sortedScans, precursorMasses, offsets,
                monoMasses, charges, intensities, tics, precursorCharges);
        }

        /// <summary>
        /// Index of the experimental fragment whose mass is closest to <paramref name="mass"/>
        /// within scan <paramref name="scanIndex"/>'s slice, or -1 if the scan has no fragments.
        /// Reproduces Ms2ScanWithSpecificMass.GetClosestFragmentMass over the flattened slice.
        /// </summary>
        public int GetClosestFragmentIndex(int scanIndex, double mass)
        {
            int lo = ScanFragmentOffsets[scanIndex];
            int hi = ScanFragmentOffsets[scanIndex + 1];
            int len = hi - lo;
            if (len == 0)
                return -1;

            int index = Array.BinarySearch(FragmentMonoMasses, lo, len, mass);
            if (index >= 0)
                return index;

            index = ~index;

            if (index == hi)
                return index - 1;
            if (index == lo || mass - FragmentMonoMasses[index - 1] > FragmentMonoMasses[index] - mass)
                return index;
            return index - 1;
        }
    }
}

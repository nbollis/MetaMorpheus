using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Chemistry;
using Easy.Common.Extensions;
using EngineLayer;
using GuiFunctions.ViewModels.Legends;
using MassSpectrometry;
using MzLibUtil;
using OxyPlot;
using Proteomics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using static GuiFunctions.MetaDrawSettings;
using IsotopicEnvelope = MassSpectrometry.IsotopicEnvelope;

namespace GuiFunctions
{
    public class ChimeraGroupViewModel : BaseViewModel
    {
        private MsDataScan _ms2Scan;
        private MsDataScan _ms1Scan;

        public string FileNameWithoutExtension { get; set; }
        public int OneBasedPrecursorScanNumber { get; set; }
        public int Ms2ScanNumber { get; set; }
        public int Count => ChimericPsms.Count;
        public int ProteinCount { get; }

        public MsDataScan Ms1Scan => _ms1Scan;
        public MsDataScan Ms2Scan => _ms2Scan;
        public ObservableCollection<ChimericPsmModel> ChimericPsms { get; set; }

        private double _chimeraScore;
        public double ChimeraScore { get => _chimeraScore; set { _chimeraScore = value; OnPropertyChanged(nameof(ChimeraScore)); } }
        public int TotalFragments { get; }
        public int UniqueFragments { get; }
        public double FragmentRatio { get; }
        public double SequenceCoverage { get; }


        #region Plotting



        private bool IsColorInitialized { get; set; } = false;

        private Dictionary<OxyColor, List<(MatchedFragmentIon, string)>> _matchedFragmentIonsByColor;
        public Dictionary<OxyColor, List<(MatchedFragmentIon, string)>> MatchedFragmentIonsByColor
        {
            get
            {
                if (IsColorInitialized) return _matchedFragmentIonsByColor;
                AssignIonColors();
                IsColorInitialized = true;
                return _matchedFragmentIonsByColor;
            }
            set
            {
                _matchedFragmentIonsByColor = value;
                OnPropertyChanged(nameof(MatchedFragmentIonsByColor));
            }
        }

        private Dictionary<OxyColor, List<(MatchedFragmentIon, string)>> _precursorIonsByColor;

        public Dictionary<OxyColor, List<(MatchedFragmentIon, string)>> PrecursorIonsByColor
        {
            get
            {
                if (IsColorInitialized) return _precursorIonsByColor;
                AssignIonColors();
                IsColorInitialized = true;
                return _precursorIonsByColor;
            }
            set
            {
                _precursorIonsByColor = value;
                OnPropertyChanged(nameof(PrecursorIonsByColor));
            }
        }


        public Dictionary<string, List<ChimeraLegendItemViewModel>> LegendItems { get; set; }

        #endregion

        public ChimeraGroupViewModel(MsDataScan ms2Scan, MsDataScan ms1Scan, List<PsmFromTsv> psms, bool annotateWithLetterOnly = false)
        {
            if (psms.Select(p => (p.PrecursorScanNum, p.Ms2ScanNumber, p.FileNameWithoutExtension))
                    .Distinct()
                    .Count() != 1)
                throw new ArgumentException("Not a chimeric group of psms");

            _ms2Scan = ms2Scan;
            _ms1Scan = ms1Scan;
            FileNameWithoutExtension = psms.First().FileNameWithoutExtension;
            OneBasedPrecursorScanNumber = psms.First().PrecursorScanNum;
            Ms2ScanNumber = psms.First().Ms2ScanNumber;
            ChimericPsms = new ObservableCollection<ChimericPsmModel>();
            _matchedFragmentIonsByColor = new Dictionary<OxyColor, List<(MatchedFragmentIon, string)>>();
            _precursorIonsByColor = new Dictionary<OxyColor, List<(MatchedFragmentIon, string)>>();
            LegendItems = new ();
            Letters = new Queue<string>(_letters);
            ConstructChimericPsmModels(psms);
            CalculateChimeraScore();

            var terminalFrags = psms.SelectMany(p => p.MatchedIons)
                .Where(p => p.NeutralTheoreticalProduct.SecondaryProductType is null).ToList();
            TotalFragments = terminalFrags.Count;
            UniqueFragments = terminalFrags.Distinct().Count();
            ProteinCount = psms.GroupBy(p => p.ProteinAccession).Count();
            FragmentRatio = UniqueFragments / (double)TotalFragments;
            SequenceCoverage = psms.Average(p => p.SequenceCoverage);

            // remove this later
            AssignIonColors(annotateWithLetterOnly);
        }

        private void CalculateChimeraScore()
        {
            var peaks = Ms1Scan.MassSpectrum.Extract(Ms2Scan.IsolationRange).ToList();
            double sumIntensity = peaks.Sum(p => p.Intensity);

            double peaksMatched = 0;
            double decimalComponent = 0;
            foreach (var precursorPeak in ChimericPsms.SelectMany(psm => psm.Ms2Scan.PrecursorEnvelope.Peaks))
            {
                peaksMatched++;
                decimalComponent += precursorPeak.intensity / sumIntensity;
            }
            ChimeraScore = Math.Round(Math.Floor(peaksMatched / peaks.Count * 100) + decimalComponent, 2);
        }

        public override string ToString()
        {
            return $"{OneBasedPrecursorScanNumber},{Ms2ScanNumber},{Count},{FileNameWithoutExtension}";
        }

        private void ConstructChimericPsmModels(List<PsmFromTsv> psms)
        {
            var commonParameters = ChimeraAnalysisTabViewModel.CommonParameters;
            var scans = GetMs2Scans(_ms1Scan, _ms2Scan, commonParameters);
            var tolerance = new PpmTolerance(100);

            // match each scan with a SpectrumMatch based upon the spectrumMatches peptidemonoMass + massdiffda
            // considering each scan needs to be matched with teh closes spectrum match
            List<(PsmFromTsv, Ms2ScanWithSpecificMass)> matchedPsms = new List<(PsmFromTsv, Ms2ScanWithSpecificMass)>();
            foreach (var scan in scans.Where(p => p.PrecursorEnvelope.Peaks.Count >= 3 && _ms2Scan.IsolationRange.MajorityWithin(p.PrecursorEnvelope.Peaks.Select(m => m.mz))))
            {
                var psm = psms
                    .Where(p => tolerance.Within(p.PrecursorMass, scan.PrecursorMass)
                    //|| (MetaDrawSettings.CheckMzForChimeras && mzTolerance.Within(p.PrecursorMz, scan.PrecursorMonoisotopicPeakMz))
                    //|| scan.PrecursorEnvelope.Peaks.Any(peak => tolerance.Within(peak.mz, p.PrecursorMz)))
                    )
                    .OrderBy(p => Math.Abs(p.PrecursorMass - scan.PrecursorMass))
                    .FirstOrDefault();

                if (psm != null)
                    matchedPsms.Add((psm, scan));
            }

            var distinct = matchedPsms
                .GroupBy(p => p.Item1.QValue + p.Item1.FullSequence)
                    .Select(g => g.First())
                    .OrderBy(p => p.Item1.PrecursorMz)
                    .ToList();

            int min = psms.Count - ChimeraDelta;
            int max = psms.Count + ChimeraDelta;
            if ((distinct.Count >= min && distinct.Count <= max))
            {

                int proteinIndex = 0;
                foreach (var group in distinct.GroupBy(p => p.Item1.ProteinName)
                             .OrderByDescending(p => p.Count()))
                {
                    var proteinColor = ChimeraSpectrumMatchPlot.ColorByProteinDictionary[proteinIndex][0];
                    LegendItems.Add(group.Key, new List<ChimeraLegendItemViewModel>());

                    //if (annotationGroup.Count( ) > 1)
                        //LegendItems[annotationGroup.Key].Add(new ChimeraLegendItemViewModel("Shared Ions", proteinColor));
                    for (int i = 0; i < group.Count(); i++)
                    {
                        var color = ChimeraSpectrumMatchPlot.ColorByProteinDictionary[proteinIndex][i + 1];
                        var chimericPsm = new ChimericPsmModel(group.ElementAt(i).Item1, group.ElementAt(i).Item2,
                            color, proteinColor) {Letter = Letters.Dequeue() };
                        ChimericPsms.Add(chimericPsm);
                        LegendItems[group.Key].Add(new(chimericPsm.ModString, color));
                    }
                    proteinIndex++;
                }
            }
        }

        private List<string> _letters = new List<string> { "A", "B", "C",  "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" };
        public Queue<string> Letters { get; }
        internal void AssignIonColors(bool useLetterOnly = false)
        {

            // precursor peaks
            foreach (var group in ChimericPsms.SelectMany(psm => psm.Ms2Scan.PrecursorEnvelope.Peaks.Select(peak =>
                         {
                             var neutralTheoreticalProduct = new Product(ProductType.M, FragmentationTerminus.None,
                                 peak.mz.ToMass(psm.Ms2Scan.PrecursorEnvelope.Charge),
                                 0, 0, 0);
                             return (psm, new MatchedFragmentIon(
                                 ref neutralTheoreticalProduct,
                                 peak.mz,
                                 peak.intensity,
                                 psm.Ms2Scan.PrecursorEnvelope.Charge));
                         }))
                         .GroupBy(p => p.Item2))
            {
                
                // distinct ions
                if (group.Count() == 1)
                {
                    var psm = group.First().psm;
                    var maxIntensityPrecursorIon = psm.Ms2Scan.PrecursorEnvelope.Peaks.MaxBy(p => p.intensity);
                    if ( Math.Abs(group.Key.Intensity - maxIntensityPrecursorIon.intensity) < 0.00001)
                    {
                        string annotation = "";

                        if (useLetterOnly)
                        {
                            annotation += psm.Letter;
                        }
                        else
                        {
                            annotation += $"Charge = {group.Key.Charge}";
                            annotation += $"\nm/z = {group.Key.Mz:0.00}";
                            annotation += $"\nMono Mass = {psm.Ms2Scan.PrecursorEnvelope.MonoisotopicMass:0.00}";
                            annotation += $"\nProtein = {psm.Psm.ProteinName}";


                            //PeptideWithSetModifications pepWithSetMods = new(psm.Psm.FullSequence.Split("|")[0], GlobalVariables.AllModsKnownDictionary);
                            //foreach (var mod in pepWithSetMods.AllModsOneIsNterminus)
                            //{
                            //    annotation += $"\n{mod.Value.IdWithMotif}{mod.Key}";
                            //}
                        }


                        _precursorIonsByColor.AddOrReplace(psm.Color, group.Key, annotation);
                    }
                    else
                        _precursorIonsByColor.AddOrReplace(psm.Color, group.Key, "");
                }
                // shared ions
                else
                {
                    if (group.Select(p => p.psm.Psm.ProteinAccession).Distinct().Count() == 1)
                    {
                        _precursorIonsByColor.AddOrReplace(group.First().psm.ProteinColor, group.Key, "");
                    }
                    else
                    {
                        _precursorIonsByColor.AddOrReplace(ChimeraSpectrumMatchPlot.MultipleProteinSharedColor, group.Key, "");
                    }
                }
            }

            var accessionDict = ChimericPsms.Select(p => p.Psm.ProteinAccession)
                .Distinct()
                .ToDictionary(p => p, p => ChimericPsms.Count(psm => psm.Psm.ProteinAccession == p));
            foreach (var annotationGroup in ChimericPsms
                         .SelectMany(psm => psm.Psm.MatchedIons
                             .Select(ion => (psm.Psm.ProteinAccession, psm.Color, psm.ProteinColor, ion)))
                             .GroupBy(g => g.ion.Annotation))
            {
                //TODO: Group by mz
                // if only one ion has the same annotation, unique proteoform color
                if (annotationGroup.Count() == 1)
                    _matchedFragmentIonsByColor.AddOrReplace(annotationGroup.First().Color, annotationGroup.First().ion, "");
                else
                {
                    foreach (var mzGroup in annotationGroup.GroupBy(p => p.ion.Mz))
                    {
                        if (mzGroup.Count() == 1)
                        {
                            _matchedFragmentIonsByColor.AddOrReplace(mzGroup.First().Color, mzGroup.First().ion, "");
                        }
                        // if only one protein present
                        else if (mzGroup.Select(p => p.ProteinAccession).Distinct().Count() == 1)
                        {
                            // if all proteoforms of the protein have the ion, protein shared color
                            if (mzGroup.Count() == accessionDict[mzGroup.First().ProteinAccession])
                                _matchedFragmentIonsByColor.AddOrReplace(mzGroup.First().ProteinColor, mzGroup.First().ion, "");
                            // if not all proteoforms have the same ion, their unique color
                            else
                                foreach (var item in mzGroup)
                                    _matchedFragmentIonsByColor.AddOrReplace(item.Color, item.ion, "");
                            
                        }
                        // if only one mz value and multiple proteins, shared color
                        else
                        {
                            _matchedFragmentIonsByColor.AddOrReplace(ChimeraSpectrumMatchPlot.MultipleProteinSharedColor, mzGroup.First().ion, "");
                        }
                    }
                }
            }
        }

        private List<Ms2ScanWithSpecificMass> GetMs2Scans(MsDataScan Ms1Scan, MsDataScan Ms2Scan, CommonParameters commonParameters)
        {
            List<Ms2ScanWithSpecificMass> scansWithPrecursors = new List<Ms2ScanWithSpecificMass>();
            List<(double, int, IsotopicEnvelope)> precursors = new();
            try
            {
                Ms2Scan.RefineSelectedMzAndIntensity(Ms1Scan.MassSpectrum);
            }
            catch (MzLibException ex)
            {
                //Warn("Could not get precursor ion for MS2 scan #" + ms2scan.OneBasedScanNumber + "; " + ex.Message);
            }
            if (Ms2Scan.SelectedIonMonoisotopicGuessMz.HasValue)
            {
                Ms2Scan.ComputeMonoisotopicPeakIntensity(Ms1Scan.MassSpectrum);
            }

            if (commonParameters.DoPrecursorDeconvolution)
            {
                foreach (IsotopicEnvelope envelope in Ms2Scan.GetIsolatedMassesAndCharges(
                             Ms1Scan.MassSpectrum, 1,
                             commonParameters.DeconvolutionMaxAssumedChargeState,
                             commonParameters.DeconvolutionMassTolerance.Value,
                             commonParameters.DeconvolutionIntensityRatio))
                {
                    double monoPeakMz = envelope.MonoisotopicMass.ToMz(envelope.Charge);
                    precursors.Add((monoPeakMz, envelope.Charge, envelope));
                }
            }

            IsotopicEnvelope[] neutralExperimentalFragments = null;
            if (commonParameters.DissociationType != DissociationType.LowCID)
            {
                neutralExperimentalFragments = Ms2ScanWithSpecificMass.GetNeutralExperimentalFragments(Ms2Scan, commonParameters);
            }

            foreach (var precursor in precursors)
            {
                // assign precursor for this MS2 scan
                var scan = new Ms2ScanWithSpecificMass(Ms2Scan, precursor.Item1,
                    precursor.Item2, "", commonParameters, neutralExperimentalFragments);
                scan.PrecursorEnvelope = precursor.Item3;
                scansWithPrecursors.Add(scan);
            }

            return scansWithPrecursors;
        }
    }

    internal static class Extensions
    {
        /// <summary>
        /// Determines if a majority of values are within a range
        /// </summary>
        /// <param name="range"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        internal static bool MajorityWithin(this MzRange range, IEnumerable<double> values)
        {
            int within = values.Count(p => p >= range.Minimum && p <= range.Maximum);
            return within > values.Count() / 2;
        }

        // method to add a value to a list in a dictionary if the key is present, and craete a new list if the key is not present
        public static void AddOrReplace<TKey, TValue, TValue2>(this Dictionary<TKey, List<(TValue, TValue2)>> dictionary, TKey key,
            TValue value, TValue2 value2)
        {
            if (dictionary.ContainsKey(key))
            {
                var previousVersion = dictionary[key].FirstOrDefault(p => p.Item1.Equals(value));
                if (!previousVersion.GetType().IsDefault())
                {
                    dictionary[key].Remove(previousVersion);
                }
                dictionary[key].Add((value, value2));

            }
            else
                dictionary.Add(key, new List<(TValue, TValue2)> { (value, value2) });

        }
    }
  
}

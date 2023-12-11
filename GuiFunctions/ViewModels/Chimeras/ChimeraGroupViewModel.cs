using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Imaging;
using System.Linq;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using Chemistry;
using Easy.Common.Extensions;
using EngineLayer;
using FlashLFQ;
using GuiFunctions.MetaDraw.SpectrumMatch;
using GuiFunctions.ViewModels.Legends;
using MassSpectrometry;
using MzLibUtil;
using OxyPlot;
using Proteomics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using TopDownProteomics.IO.PsiMod;
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

        public MsDataScan Ms1Scan => _ms1Scan;
        public MsDataScan Ms2Scan => _ms2Scan;
        public ObservableCollection<ChimericPsmModel> ChimericPsms { get; set; }

        #region Plotting

        private bool IsColorInitialized { get; set; } = false;

        private Dictionary<OxyColor, List<MatchedFragmentIon>> _matchedFragmentIonsByColor;
        public Dictionary<OxyColor, List<MatchedFragmentIon>> MatchedFragmentIonsByColor
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

        private Dictionary<OxyColor, List<MatchedFragmentIon>> _precursorIonsByColor;

        public Dictionary<OxyColor, List<MatchedFragmentIon>> PrecursorIonsByColor
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

        public ChimeraGroupViewModel(MsDataScan ms2Scan, MsDataScan ms1Scan, List<PsmFromTsv> psms)
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
            _matchedFragmentIonsByColor = new Dictionary<OxyColor, List<MatchedFragmentIon>>();
            _precursorIonsByColor = new Dictionary<OxyColor, List<MatchedFragmentIon>>();
            LegendItems = new ();
            ConstructChimericPsmModels(psms);
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
            var mzTolerance = new PpmTolerance(10);

            // match each scan with a SpectrumMatch based upon the spectrumMatches peptidemonoMass + massdiffda
            // considering each scan needs to be matched with teh closes spectrum match
            List<(PsmFromTsv, Ms2ScanWithSpecificMass)> matchedPsms = new List<(PsmFromTsv, Ms2ScanWithSpecificMass)>();
            foreach (var scan in scans)
            {
                var scanMonoMass = scan.PrecursorMonoisotopicPeakMz.ToMass(scan.PrecursorCharge);
                var psm = psms
                    .Where(p => tolerance.Within(double.Parse(p.PeptideMonoMass) + double.Parse(p.MassDiffDa), scanMonoMass)
                                || (MetaDrawSettings.CheckMzForChimeras && mzTolerance.Within(p.PrecursorMz, scan.PrecursorMonoisotopicPeakMz)))
                    .MinBy(p => Math.Abs(double.Parse(p.PeptideMonoMass) + double.Parse(p.MassDiffDa) - scanMonoMass));
                if (psm != null)
                {
                    matchedPsms.Add((psm, scan));
                }
            }

            var distinct = matchedPsms.DistinctBy(p => p.Item1.QValue + p.Item1.FullSequence).ToList();

            int min = psms.Count - ChimeraDelta;
            int max = psms.Count + ChimeraDelta;
            if (distinct.Count >= min && distinct.Count <= max)
            {

                int proteinIndex = 0;
                foreach (var group in distinct.GroupBy(p => p.Item1.ProteinName)
                             .OrderByDescending(p => p.Count()))
                {
                    var proteinColor = ChimeraSpectrumMatchPlot.ColorByProteinDictionary[proteinIndex][0];
                    LegendItems.Add(group.Key, new List<ChimeraLegendItemViewModel>());

                    if (group.Count( ) > 1)
                        LegendItems[group.Key].Add(new ChimeraLegendItemViewModel("Shared Ions", proteinColor));
                    for (int i = 0; i < group.Count(); i++)
                    {
                        var color = ChimeraSpectrumMatchPlot.ColorByProteinDictionary[proteinIndex][i + 1];
                        ChimericPsms.Add(new ChimericPsmModel(group.ElementAt(i).Item1, group.ElementAt(i).Item2, color, proteinColor));
                        PeptideWithSetModifications pepWithSetMods = new(group.ElementAt(i).Item1.FullSequence.Split("|")[0], GlobalVariables.AllModsKnownDictionary);
                        var modString = String.Join(", ",
                            pepWithSetMods.AllModsOneIsNterminus.Select(p => p.Key + " - " + p.Value.IdWithMotif));
                        LegendItems[group.Key].Add(new(modString, color));
                    }
                    proteinIndex++;
                }
            }
        }


        private void AssignIonColors()
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
                    _precursorIonsByColor.AddOrReplace(group.First().psm.Color, group.Key);
                }
                // shared ions
                else
                {
                    if (group.Select(p => p.psm.Psm.ProteinAccession).Distinct().Count() == 1)
                    {
                        _precursorIonsByColor.AddOrReplace(group.First().psm.ProteinColor, group.Key);
                    }
                    else
                    {
                        _precursorIonsByColor.AddOrReplace(ChimeraSpectrumMatchPlot.MultipleProteinSharedColor, group.Key);
                    }
                }
            }

            // fragment peaks
            foreach (var group in ChimericPsms.SelectMany(psm => psm.Psm.MatchedIons.Select(p => (psm, p))).GroupBy(p => p.p))
            {
                //string proteinName = group.First().psm.Psm.ProteinName;
                //LegendItems.Add(proteinName, new List<ChimeraLegendItemViewModel>());
                // shared ions
                if (group.Count() > 1)
                {
                    if (group.Select(p => p.psm.Psm.ProteinAccession).Distinct().Count() == 1)
                    {
                        _matchedFragmentIonsByColor.AddOrReplace(group.First().psm.ProteinColor, group.Key);
                    }
                    else
                    {
                        _matchedFragmentIonsByColor.AddOrReplace(ChimeraSpectrumMatchPlot.MultipleProteinSharedColor, group.Key);
                    }
                }
                // distinct ions
                else
                {
                    _matchedFragmentIonsByColor.AddOrReplace(group.First().psm.Color, group.Key);
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

    public static class DictionaryExtensions
    {
        // method to add a value to a list in a dictionary if the key is present, and craete a new list if the key is not present
        public static void AddOrReplace<TKey, TValue>(this Dictionary<TKey, List<TValue>> dictionary, TKey key,
            TValue value)
        {
            if (dictionary.ContainsKey(key))
                dictionary[key].Add(value);
            else
                dictionary.Add(key, new List<TValue> { value });
        
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chemistry;
using Easy.Common.Extensions;
using Easy.Common.Interfaces;
using EngineLayer;
using MassSpectrometry;
using MzLibUtil;
using Nett;
using OxyPlot;
using OxyPlot.Wpf;
using Proteomics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using TaskLayer;

namespace GuiFunctions.MetaDraw.SpectrumMatch
{
    public class Ms1ChimeraPlot : SpectrumMatchPlot
    {
        public ChimeraGroupViewModel ChimeraGroup { get; private set; }
        public PlotView PlotView { get; private set; }
        public DoubleRange Range { get; private set; }

        public Ms1ChimeraPlot(PlotView plotView, ChimeraGroupViewModel chimeraGroupVm) : base(plotView, null,
            chimeraGroupVm.Ms1Scan)
        {
            PlotView = plotView;
            Range = chimeraGroupVm.Ms2Scan.IsolationRange;
            ChimeraGroup = chimeraGroupVm;

            AnnotateChimericPeaks(chimeraGroupVm);
            ZoomAxes();
            RefreshChart();
        }

        private void AnnotateChimericPeaks(ChimeraGroupViewModel chimeraGroupVm)
        {
            List<MatchedFragmentIon> allMatchedIons = new();
            List<(string, MatchedFragmentIon)> allDrawnIons = new();

            int proteinIndex = 0;
            foreach (var matchedGroup in chimeraGroupVm.ChimericPsms.GroupBy(p => p.Psm.ProteinAccession)
                         .OrderByDescending(p => p.Count())
                         .ToDictionary(p => p.Key, p => p.ToList()))
            {
                List<MatchedFragmentIon> proteinMatchedIons = new();
                List<MatchedFragmentIon> proteinDrawnIons = new();

                for (int i = 0; i < matchedGroup.Value.Count; i++)
                {
                    var chimericPsm = matchedGroup.Value[i];
                    var envelope = chimericPsm.Ms2Scan.PrecursorEnvelope;
                    var ions = envelope.Peaks.Select(p =>
                    {
                        var neutralTheoreticalProduct = new Product(ProductType.M, FragmentationTerminus.None, p.mz.ToMass(envelope.Charge), 0,
                            0, 0);
                        return new MatchedFragmentIon(
                            ref neutralTheoreticalProduct,
                            (double)chimeraGroupVm.Ms1Scan.MassSpectrum.GetClosestPeakXvalue(p.mz)!,
                            chimeraGroupVm.Ms1Scan.MassSpectrum.YArray[chimeraGroupVm.Ms1Scan.MassSpectrum.GetClosestPeakIndex(p.mz)],
                            envelope.Charge);
                    }).ToList();
                    proteinMatchedIons.AddRange(ions);
                    allMatchedIons.AddRange(ions);

                    // each matched ion
                    foreach (var matchedIon in ions)
                    {
                        OxyColor color;

                        // if drawn by the same protein already
                        if (proteinDrawnIons.Any(p => p.Equals(matchedIon)))
                        {
                            color = chimericPsm.ProteinColor;
                        }
                        // if drawn already by different protein
                        else if (allDrawnIons.Any(p => p.Item2.Equals(matchedIon)))
                        {
                            color = ChimeraSpectrumMatchPlot.MultipleProteinSharedColor;
                        }
                        // if unique peak
                        else
                        {
                            color = chimericPsm.Color;
                            proteinDrawnIons.Add(matchedIon);
                        }
                        AnnotatePeak(matchedIon, false, false, color);
                        allDrawnIons.Add((matchedGroup.Key, matchedIon));
                    }
                }
                proteinIndex++;
            }
        }

        public void ZoomAxes()
        {
            var maxIntensity = ChimeraGroup.Ms1Scan.MassSpectrum.Extract(Range).Max(p => p.Intensity) * 1.2;
            Model.Axes[0].Zoom(Range.Minimum, Range.Maximum);
            Model.Axes[1].Zoom(0, maxIntensity);
        }
    }


    

    public class ChimeraGroup2
    {
        public string DataFile { get; set; }
        public int OneBasedPrecursorScanNumber { get; set; }
        public int Ms2ScanNumber { get; set; }
        public int Count => Psms.Count;

        public MsDataScan Ms1Scan { get; set; }
        public MsDataScan Ms2Scan { get; set; }
        public List<ChimericPsm> ChimericPsms { get; set; }
        internal List<PsmFromTsv> Psms { get; set; }

        public ChimeraGroup2(List<ChimericPsm> psms)
        {
            if (psms.Select(p => (p.Psm.PrecursorScanNum, p.Psm.Ms2ScanNumber, p.Psm.FileNameWithoutExtension))
                    .Distinct()
                    .Count() != 1)
                throw new ArgumentException("Not a chimeric group of psms");

            DataFile = psms.First().Psm.FileNameWithoutExtension;
            OneBasedPrecursorScanNumber = psms.First().Psm.PrecursorScanNum;
            Ms2ScanNumber = psms.First().Psm.Ms2ScanNumber;
            Psms = psms.Select(p => p.Psm).ToList();
            ChimericPsms = psms;
        }

        #region Static 

        static Func<PsmFromTsv, object>[] ChimeraSelector =
        {
            psm => psm.PrecursorScanNum,
            psm => psm.Ms2ScanNumber,
            psm => psm.FileNameWithoutExtension.Replace("-averaged", "")
        };
        static CustomComparer<PsmFromTsv> ChimeraComparer = new CustomComparer<PsmFromTsv>(ChimeraSelector);
        private static CommonParameters _commonParameters { get; set; }
        public static CommonParameters commonParameters => _commonParameters ?? Toml
            .ReadFile<SearchTask>(
                @"C:\Users\Nic\OneDrive - UW-Madison\AUSTIN V CARR - AUSTIN V CARR's files\SpectralAveragingPaper\Supplemental Information\Tasks\SupplementalFile4_Task4-SearchTaskconfig.toml",
                MetaMorpheusTask.tomlConfig).CommonParameters;
        public static IEnumerable<ChimeraGroup> GetChimeraGroups(List<PsmFromTsv> psms, Dictionary<string, MsDataFile> dataFiles)
        {
            if (psms.Select(p => (p.PrecursorScanNum, p.Ms2ScanNumber, p.FileNameWithoutExtension)).Distinct()
                    .Count() != 1)
                throw new ArgumentException("Not a chimeric group of psms");
            foreach (var group in psms.Where(p => p.QValue <= 0.01 && p.DecoyContamTarget == "T")
                         .GroupBy(p => p, ChimeraComparer)
                         .Where(p => p.Count() >= MetaDrawSettings.MinChimera)
                         .OrderByDescending(p => p.Count()))
            {
                // get the scan
                if (!dataFiles.TryGetValue(group.First().FileNameWithoutExtension, out MsDataFile spectraFile))
                    continue;

                var ms1Scan = spectraFile.GetOneBasedScanFromDynamicConnection(group.First().PrecursorScanNum);
                var ms2Scan = spectraFile.GetOneBasedScanFromDynamicConnection(group.First().Ms2ScanNumber);

                yield return new ChimeraGroup(group.ToList());
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
        #endregion

    }

    public class ChimericPsm
    {
        public PsmFromTsv Psm { get; set; }
        public Ms2ScanWithSpecificMass Ms2Scan { get; set; }
        public OxyColor Color { get; set; }
        public OxyColor ProteinColor { get; set; }

        public ChimericPsm(PsmFromTsv psm, Ms2ScanWithSpecificMass ms2Scan, OxyColor color, OxyColor proteinColor)
        {
            Psm = psm;
            Ms2Scan = ms2Scan;
            Color = color;
            ProteinColor = proteinColor;
        }
    }


    public class ChimeraGroup
    {
        public string DataFile { get; set; }
        public int OneBasedPrecursorScanNumber { get; set; }
        public int Ms2ScanNumber { get; set; }
        public int Count => Psms.Count;
        internal Dictionary<string, List<PsmFromTsv>> PsmsByProteinDictionary { get; set; }
        internal List<PsmFromTsv> Psms { get; set; }
        public Dictionary<PsmFromTsv, OxyColor> PsmToColorDictionary { get; set; }
        public Dictionary<PsmFromTsv, Ms2ScanWithSpecificMass> PsmToMs2ScanWithSpecificMassesDictionary { get; set; }
        public Ms2ScanWithSpecificMass Ms2Scan { get; private set; }

        public ChimeraGroup(List<PsmFromTsv> psms)
        {
            if (psms.Select(p => (p.PrecursorScanNum, p.Ms2ScanNumber, p.FileNameWithoutExtension)).Distinct()
                    .Count() != 1)
                throw new ArgumentException("Not a chimeric group of psms");


            DataFile = psms.First().FileNameWithoutExtension;
            OneBasedPrecursorScanNumber = psms.First().PrecursorScanNum;
            Ms2ScanNumber = psms.First().Ms2ScanNumber;
            Psms = psms;
            PsmsByProteinDictionary = psms.GroupBy(p => p.ProteinAccession)
                .ToDictionary(p => p.Key, p => p.ToList());
            PsmToMs2ScanWithSpecificMassesDictionary = new Dictionary<PsmFromTsv, Ms2ScanWithSpecificMass>();
            SetColorDictionary();
        }

        private void SetColorDictionary()
        {
            PsmToColorDictionary = new Dictionary<PsmFromTsv, OxyColor>();
            var ColorByProteinDictionary = ChimeraSpectrumMatchPlot.ColorByProteinDictionary;
            Queue<OxyColor> overflowColors = ChimeraSpectrumMatchPlot.OverflowColors;

            int proteinIndex = 0;
            foreach (var protein in PsmsByProteinDictionary)
            {
                for (int i = 0; i < protein.Value.Count; i++)
                {
                    // more proteins than protein programmed colors
                    if (proteinIndex >= ColorByProteinDictionary.Keys.Count)
                    {
                        proteinIndex = 0;
                    }

                    // more proteoforms than programmed colors
                    if (i + 1 >= ColorByProteinDictionary[proteinIndex].Count)
                    {
                        PsmToColorDictionary.Add(protein.Value[i], overflowColors.Dequeue());
                    }
                    else
                    {
                        PsmToColorDictionary.Add(protein.Value[i], ColorByProteinDictionary[proteinIndex][i + 1]);
                    }
                }

                proteinIndex++;
            }
        }

        public bool IsGroup(PsmFromTsv psm)
        {
            return psm.FileNameWithoutExtension.Equals(DataFile) &&
                   psm.PrecursorScanNum.Equals(OneBasedPrecursorScanNumber) &&
                   psm.Ms2ScanNumber.Equals(Ms2ScanNumber);
        }

        public override string ToString()
        {
            return $"{OneBasedPrecursorScanNumber},{Ms2ScanNumber},{Count},{DataFile}";
        }
    }

    public class CustomComparer<T> : IEqualityComparer<T>
    {
        private readonly Func<T, object>[] propertySelectors;

        public CustomComparer(params Func<T, object>[] propertySelectors)
        {
            this.propertySelectors = propertySelectors;
        }

        public bool Equals(T x, T y)
        {
            if (x == null && y == null)
                return true;
            if (x == null || y == null)
                return false;

            foreach (var selector in propertySelectors)
            {
                if (!Equals(selector(x), selector(y)))
                    return false;
            }

            return true;
        }

        public int GetHashCode(T obj)
        {
            unchecked
            {
                int hash = 17;
                foreach (var selector in propertySelectors)
                {
                    hash = hash * 23 + (selector(obj)?.GetHashCode() ?? 0);
                }
                return hash;
            }
        }
    }
}

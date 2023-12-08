using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chemistry;
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
        public ChimeraGroup ChimeraGroup { get; private set; }
        public MsDataScan Ms1Scan { get; private set; }
        public MsDataScan Ms2Scan { get; private set; }
        public PlotView PlotView { get; private set; }
        public DoubleRange Range { get; private set; }

        public Ms1ChimeraPlot(PlotView plotView, MsDataScan ms1Scan, MsDataScan ms2Scan, ChimeraGroup chimeraGroup) 
            : base(plotView, null, ms1Scan)
        {
            ChimeraGroup = chimeraGroup;
            Ms1Scan = ms1Scan;
            Ms2Scan = ms2Scan;
            PlotView = plotView;
            Range = Ms2Scan.IsolationRange;
            AnnotateChimericPeaks(GetMs2Scans());
        }

        protected void AnnotateChimericPeaks(List<Ms2ScanWithSpecificMass> scans)
        {
            List <(Ms2ScanWithSpecificMass, PsmFromTsv)> matchedScans = MatchPsmsWithScans(scans)
                .DistinctBy(p => p.Item2)
                .ToList();

            List<MatchedFragmentIon> allMatchedIons = new();
            List<(string, MatchedFragmentIon)> allDrawnIons = new();
            var ColorByProteinDictionary = ChimeraSpectrumMatchPlot.ColorByProteinDictionary;
            Queue<OxyColor> overflowColors = ChimeraSpectrumMatchPlot.OverflowColors;

            if (matchedScans.Count == ChimeraGroup.Psms.Count)
            {
                int proteinIndex = 0;
                foreach (var proteinGroup in ChimeraGroup.PsmsByProteinDictionary.Values)
                {
                    List<MatchedFragmentIon> proteinMatchedIons = new();
                    List<MatchedFragmentIon> proteinDrawnIons = new();

                    for (int j = 0; j < proteinGroup.Count; j++)
                    {
                        proteinMatchedIons.AddRange(proteinGroup[j].MatchedIons);
                        allMatchedIons.AddRange(proteinGroup[j].MatchedIons);
                        PeptideWithSetModifications pepWithSetMods = new(proteinGroup[j].FullSequence.Split('|')[0], GlobalVariables.AllModsKnownDictionary);

                        // more proteins than protein programmed colors
                        if (proteinIndex >= ColorByProteinDictionary.Keys.Count)
                        {
                            proteinIndex = 0;
                        }

                        // each matched ion
                        foreach (var matchedIon in proteinGroup[j].MatchedIons)
                        {
                            OxyColor color;

                            // if drawn by the same protein already
                            if (proteinDrawnIons.Any(p => p.Equals(matchedIon)))
                            {
                                color = ColorByProteinDictionary[proteinIndex][0];
                            }
                            // if drawn already by different protein
                            else if (allDrawnIons.Any(p => p.Item2.Equals(matchedIon)))
                            {
                                color = ChimeraSpectrumMatchPlot.MultipleProteinSharedColor;
                            }
                            // if unique peak
                            else
                            {
                                // more proteoforms than programmed colors
                                if (j + 1 >= ColorByProteinDictionary[proteinIndex].Count)
                                {
                                    color = overflowColors.Dequeue();
                                }
                                else
                                {
                                    color = ColorByProteinDictionary[proteinIndex][j + 1];
                                }
                                proteinDrawnIons.Add(matchedIon);
                            }
                            AnnotatePeak(matchedIon, false, false, color);
                            allDrawnIons.Add((proteinGroup[j].BaseSeq, matchedIon));
                        }
                    }
                    proteinIndex++;
                }
            }
        }


        public new void ZoomAxes()
        {


        }


        private IEnumerable<(Ms2ScanWithSpecificMass, PsmFromTsv)> MatchPsmsWithScans(List<Ms2ScanWithSpecificMass> scans)
        {
            var tolerance = new PpmTolerance(100);
            var mzTolerance = new PpmTolerance(10);

            // match each scan with a SpectrumMatch based upon the spectrumMatches peptidemonoMass + massdiffda
            // considering each scan needs to be matched with teh closes spectrum match
            foreach (var scan in scans)
            {
                var scanMonoMass = scan.PrecursorMonoisotopicPeakMz.ToMass(scan.PrecursorCharge);
                var psm = ChimeraGroup.Psms
                    .Where(p => tolerance.Within(double.Parse(p.PeptideMonoMass) + double.Parse(p.MassDiffDa), scanMonoMass)
                    || mzTolerance.Within(p.PrecursorMz, scan.PrecursorMonoisotopicPeakMz))
                    .OrderBy(p => Math.Abs(double.Parse(p.PeptideMonoMass) + double.Parse(p.MassDiffDa) - scanMonoMass))
                    .FirstOrDefault();
                if (psm != null)
                {
                    yield return (scan, psm);
                }
            }
        }

        private List<Ms2ScanWithSpecificMass> GetMs2Scans()
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

        private static CommonParameters _commonParameters { get; set; }
        public static CommonParameters commonParameters => _commonParameters ?? Toml
            .ReadFile<SearchTask>(
                @"C:\Users\Nic\OneDrive - UW-Madison\AUSTIN V CARR - AUSTIN V CARR's files\SpectralAveragingPaper\Supplemental Information\Tasks\SupplementalFile4_Task4-SearchTaskconfig.toml",
                MetaMorpheusTask.tomlConfig).CommonParameters;
    }


    public class ChimeraGroup
    {
        public string DataFile { get; set; }
        public int OneBasedPrecursorScanNumber { get; set; }
        public int Ms2ScanNumber { get; set; }
        public int Count => Psms.Count;
        public Dictionary<string, List<PsmFromTsv>> PsmsByProteinDictionary { get; set; }
        internal List<PsmFromTsv> Psms { get; set; }

        public ChimeraGroup(List<PsmFromTsv> psms)
        {
            if (psms.Select(p => (p.PrecursorScanNum, p.Ms2ScanNumber, p.FileNameWithoutExtension)).Distinct()
                    .Count() != 1)
                throw new ArgumentException("Not a chimeric group of psms");


            DataFile = psms.First().FileNameWithoutExtension;
            OneBasedPrecursorScanNumber = psms.First().PrecursorScanNum;
            Ms2ScanNumber = psms.First().Ms2ScanNumber;
            Psms = psms;
            PsmsByProteinDictionary = psms.GroupBy(p => p.BaseSeq)
                .ToDictionary(p => p.Key, p => p.ToList());
        }

        public bool IsGroup(PsmFromTsv psm)
        {
            return psm.FileNameWithoutExtension.Equals(DataFile) &&
                   psm.PrecursorScanNum.Equals(OneBasedPrecursorScanNumber) &&
                   psm.Ms2ScanNumber.Equals(Ms2ScanNumber);
        }
    }
}

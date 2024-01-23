using EngineLayer;
using Omics.Modifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Omics.Digestion;
using Transcriptomics.Digestion;
using Transcriptomics;
using System.IO;

namespace TaskLayer
{
    public class RnaPostSearchAnalysisTask : MetaMorpheusTask
    {
        public RnaPostSearchAnalysisParameters Parameters { get; set; }
        internal IEnumerable<IGrouping<string, OligoSpectralMatch>> OsmsGroupedByFileName { get; private set; }
        internal List<OligoSpectralMatch> _filteredOsms;

        internal int MassDiffAcceptorNumNotches;
        private bool _pepFilteringNotPerformed;
        private string _filterType;
        private double _filterThreshold;


        public RnaPostSearchAnalysisTask() : base(MyTask.RnaSearch)
        {

        }

        protected override MyTaskResults RunSpecific(string OutputFolder, List<DbForTask> dbFilenameList, List<string> currentRawFileList, string taskId,
            FileSpecificParameters[] fileSettingsList)
        {
            throw new NotImplementedException();
        }

        public MyTaskResults Run()
        {
            // Stop loop if canceled
            if (GlobalVariables.StopLoops) { return Parameters.SearchTaskResults; }

            Parameters.AllOsms = Parameters.AllOsms.Where(psm => psm != null).ToList();
            Parameters.AllOsms.ForEach(psm => psm.ResolveAllAmbiguities());
            Parameters.AllOsms = Parameters.AllOsms.OrderByDescending(b => b.Score)
                .ThenBy(b => b.BioPolymerWithSetModsMonoisotopicMass.HasValue ? Math.Abs(b.ScanPrecursorMass - b.BioPolymerWithSetModsMonoisotopicMass.Value) : double.MaxValue)
                .GroupBy(b => (b.FullFilePath, b.ScanNumber, b.BioPolymerWithSetModsMonoisotopicMass)).Select(b => b.First()).ToList();

            CalculateOsmFdr();
            FilterAllOsms();

            //DoMassDifferenceLocalizationAnalysis();
            //ProteinAnalysis();
            //QuantificationAnalysis();

            //ReportProgress(new ProgressEventArgs(100, "Done!", new List<string> { Parameters.SearchTaskId, "Individual Spectra Files" }));

            //HistogramAnalysis();

            WriteOsmResults();
            //WriteOligoResults();

            return Parameters.SearchTaskResults;
        }

        #region Filtering


        public IEnumerable<OligoSpectralMatch> GetFilteredOsms(bool includeDecoys, bool includeContaminants,
            bool includeAmbiguous)
        {
            return _filteredOsms.Where(p =>
                (includeDecoys || !p.IsDecoy)
                && (includeContaminants || !p.IsContaminant)
                && (includeAmbiguous || p.FullSequence != null));
        }

        private void FilterAllOsms()
        {
            _filterType = "q-value";
            _filterThreshold = CommonParameters.QValueThreshold;
            _filteredOsms = Parameters.AllOsms.Where(p =>
                    p.FdrInfo.QValue <= _filterThreshold
                    && p.FdrInfo.QValueNotch <= _filterThreshold)
                .ToList();
            
            // This property is used for calculating file specific results, which requires calculating
            // FDR separately for each file. Therefore, no filtering is performed
            OsmsGroupedByFileName = Parameters.AllOsms.GroupBy(p => p.FullFilePath);
        }

        #endregion

        #region FDR

        private void CalculateOsmFdr()
        {

            Status("Estimating OSM FDR...", Parameters.SearchTaskId);
            var osms = Parameters.AllOsms.OrderByDescending(p => p.Score)
                .ThenBy(p =>
                    p.BioPolymerWithSetModsMonoisotopicMass.HasValue
                        ? Math.Abs(p.ScanPrecursorMass - p.BioPolymerWithSetModsMonoisotopicMass.Value)
                        : double.MaxValue)
                .ToList();

            double cumulativeTarget = 0;
            double cumulativeDecoy = 0;

            //set up arrays for local FDRs
            double[] cumulativeTargetPerNotch = new double[MassDiffAcceptorNumNotches + 1];
            double[] cumulativeDecoyPerNotch = new double[MassDiffAcceptorNumNotches + 1];

            for (int i = 0; i < osms.Count; i++)
            {
                // Stop if canceled
                if (GlobalVariables.StopLoops)
                {
                    break;
                }

                OligoSpectralMatch psm = osms[i];
                int notch = psm.Notch ?? MassDiffAcceptorNumNotches;
                if (psm.IsDecoy)
                {
                    // the PSM can be ambiguous between a target and a decoy sequence
                    // in that case, count it as the fraction of decoy hits
                    // e.g. if the PSM matched to 1 target and 2 decoys, it counts as 2/3 decoy
                    double decoyHits = 0;
                    double totalHits = 0;
                    var hits = psm.BestMatchingBioPolymersWithSetMods.GroupBy(p => p.Peptide.FullSequence);
                    foreach (var hit in hits)
                    {
                        if (hit.First().Peptide.Parent.IsDecoy)
                        {
                            decoyHits++;
                        }

                        totalHits++;
                    }

                    cumulativeDecoy += decoyHits / totalHits;
                    cumulativeDecoyPerNotch[notch] += decoyHits / totalHits;
                }
                else
                {
                    cumulativeTarget++;
                    cumulativeTargetPerNotch[notch]++;
                }

                double qValue = Math.Min(1, cumulativeDecoy / cumulativeTarget);
                double qValueNotch = Math.Min(1, cumulativeDecoyPerNotch[notch] / cumulativeTargetPerNotch[notch]);

                double pep = psm.FdrInfo == null ? double.NaN : psm.FdrInfo.PEP;
                double pepQValue = psm.FdrInfo == null ? double.NaN : psm.FdrInfo.PEP_QValue;

                psm.SetFdrValues(cumulativeTarget, cumulativeDecoy, qValue, cumulativeTargetPerNotch[notch],
                    cumulativeDecoyPerNotch[notch], qValueNotch, pep, pepQValue);
            }
        }

        #endregion

        #region Writing

        private void WriteOsmResults()
        {
            Status("Writing OSM results...", Parameters.SearchTaskId);

            var thresholdOsmList = GetFilteredOsms(Parameters.SearchParameters.WriteDecoys,
                Parameters.SearchParameters.WriteContaminants, Parameters.SearchParameters.WriteAmbiguous);

            string writtenFile = Path.Combine(Parameters.OutputFolder, "AllOSMs.osmtsv");
            WritePsmsToTsv(thresholdOsmList, writtenFile, Parameters.SearchParameters.ModsToWriteSelection);
            FinishedWritingFile(writtenFile, new List<string> { Parameters.SearchTaskId });
        }

        #endregion

    }
}

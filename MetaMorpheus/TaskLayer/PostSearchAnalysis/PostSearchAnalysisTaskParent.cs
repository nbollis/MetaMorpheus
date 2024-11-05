using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using EngineLayer;
using EngineLayer.FdrAnalysis;
using EngineLayer.HistogramAnalysis;
using EngineLayer.Localization;
using EngineLayer.ModificationAnalysis;
using FlashLFQ;
using MassSpectrometry;
using MathNet.Numerics.Distributions;
using TaskLayer.MbrAnalysis;
using ProteinGroup = EngineLayer.ProteinGroup;

namespace TaskLayer
{
    public abstract class PostSearchAnalysisTaskParent : MetaMorpheusTask
    {
        protected string SpectralMatchMoniker { get; init; }
        protected PostSearchAnalysisParametersParent Parameters { get; set; }
        protected IEnumerable<IGrouping<string, SpectralMatch>> SpectralMatchesGroupedByFile { get; set; }
        protected SpectralRecoveryResults SpectralRecoveryResults { get; set; }
        protected List<ProteinGroup> ProteinGroups { get; set; }
        protected List<SpectralMatch> _filteredSpectralMatches;
        protected bool _pepFilteringNotPerformed;
        protected string _filterType;
        protected double _filterThreshold;

        protected PostSearchAnalysisTaskParent(MyTask taskType) : base(taskType)
        {
        }

        #region Filtering

        /// <summary>
        /// Sets the private field _filteredSpectralMatches by removing all psms with Q and Q_Notch or PEP_QValues greater
        /// than a user defined threshold. Q-Value and PEP Q-Value filtering are mutually exculsive.
        /// In cases where PEP filtering was selected but PEP wasn't performed due to insufficient PSMs, 
        /// filtering defaults to Q and Q_Notch.
        /// _filteredSpectralMatches can be accessed through the GetFilteredSpectralMatches method.
        /// Also, sets the SpectralMatchesGroupedByFile property. This is done here because filtering is performed every time
        /// AllSpectralMatches is updated (i.e., in the Run method and during ProteinAnalysis w/ Silac labelling.)
        /// </summary>
        protected void FilterAllSpectralMatches()
        {
            _filterType = "q-value";
            _filterThreshold = Math.Min(CommonParameters.QValueThreshold, CommonParameters.PepQValueThreshold);

            if (CommonParameters.PepQValueThreshold < CommonParameters.QValueThreshold)
            {
                if (Parameters.AllSpectralMatches.Count < 100)
                {
                    _pepFilteringNotPerformed = true;
                }
                else
                {
                    _filterType = "pep q-value";
                }
            }

            _filteredSpectralMatches = _filterType.Equals("q-value")
                ? Parameters.AllSpectralMatches.Where(p =>
                        p.FdrInfo.QValue <= _filterThreshold
                        && p.FdrInfo.QValueNotch <= _filterThreshold)
                    .ToList()
                : Parameters.AllSpectralMatches.Where(p =>
                        p.FdrInfo.PEP_QValue <= _filterThreshold)
                    .ToList();

            // This property is used for calculating file specific results, which requires calculating
            // FDR separately for each file. Therefore, no filtering is performed
            SpectralMatchesGroupedByFile = Parameters.AllSpectralMatches.GroupBy(p => p.FullFilePath);
        }

        public IEnumerable<SpectralMatch> GetFilteredSpectralMatches(bool includeDecoys, bool includeContaminants,
            bool includeAmbiguous)
        {
            return _filteredSpectralMatches.Where(p =>
                (includeDecoys || !p.IsDecoy)
                && (includeContaminants || !p.IsContaminant)
                &&
                (includeAmbiguous || p.FullSequence != null));
        }

        /// <summary>
        /// Modifies a list of PSMs, removing all that should not be written to a results file.
        /// </summary>
        /// <param name="fileSpecificPsmsOrPeptides"> A list of PSMs to be modified in place </param>
        /// <param name="psmOrPeptideCountForResults"> The number of target psms scoring below threshold </param>
        protected void FilterSpecificSpectralMatches(List<SpectralMatch> fileSpecificPsmsOrPeptides, out int psmOrPeptideCountForResults)
        {
            psmOrPeptideCountForResults = _filterType.Equals("q-value")
                ? fileSpecificPsmsOrPeptides.Count(p =>
                    !p.IsDecoy
                    && p.FdrInfo.QValue <= _filterThreshold
                    && p.FdrInfo.QValueNotch <= _filterThreshold)
                : fileSpecificPsmsOrPeptides.Count(p =>
                    !p.IsDecoy
                    && p.FdrInfo.PEP_QValue <= _filterThreshold);

            if (!Parameters.SearchParameters.WriteHighQValueSpectralMatches)
            {
                if (_filterType.Equals("q-value"))
                {
                    fileSpecificPsmsOrPeptides.RemoveAll(p =>
                        p.FdrInfo.QValue > _filterThreshold |
                        p.FdrInfo.QValueNotch > _filterThreshold);
                }
                else
                {
                    fileSpecificPsmsOrPeptides.RemoveAll(p =>
                        p.FdrInfo.PEP_QValue > _filterThreshold);
                }
            }
            if (!Parameters.SearchParameters.WriteDecoys)
            {
                fileSpecificPsmsOrPeptides.RemoveAll(b => b.IsDecoy);
            }
            if (!Parameters.SearchParameters.WriteContaminants)
            {
                fileSpecificPsmsOrPeptides.RemoveAll(b => b.IsContaminant);
            }
        }

        protected void WriteProteinGroupsToTsv(List<EngineLayer.ProteinGroup> proteinGroups, string filePath, List<string> nestedIds)
        {
            if (proteinGroups != null && proteinGroups.Any())
            {
                double qValueThreshold = Math.Min(CommonParameters.QValueThreshold, CommonParameters.PepQValueThreshold);
                using (StreamWriter output = new StreamWriter(filePath))
                {
                    output.WriteLine(proteinGroups.First().GetTabSeparatedHeader());
                    for (int i = 0; i < proteinGroups.Count; i++)
                    {
                        if (!Parameters.SearchParameters.WriteDecoys && proteinGroups[i].IsDecoy ||
                            !Parameters.SearchParameters.WriteContaminants && proteinGroups[i].IsContaminant ||
                            !Parameters.SearchParameters.WriteHighQValueSpectralMatches && proteinGroups[i].QValue > qValueThreshold)
                        {
                            continue;
                        }
                        else
                        {
                            output.WriteLine(proteinGroups[i]);
                        }
                    }
                }

                FinishedWritingFile(filePath, nestedIds);
            }
        }

        protected void WriteFlashLFQResults()
        {
            if (Parameters.SearchParameters.DoLabelFreeQuantification && Parameters.FlashLfqResults != null)
            {
                // write peaks
                if (SpectralRecoveryResults != null)
                {
                    SpectralRecoveryResults.WritePeakQuantificationResultsToTsv(Parameters.OutputFolder, "AllQuantifiedPeaks");
                }
                else
                {
                    WritePeakQuantificationResultsToTsv(Parameters.FlashLfqResults, Parameters.OutputFolder, "AllQuantifiedPeaks", new List<string> { Parameters.SearchTaskId });
                }

                // write peptide quant results
                string filename = "AllQuantified" + GlobalVariables.AnalyteType + "s";
                if (SpectralRecoveryResults != null)
                {
                    SpectralRecoveryResults.WritePeptideQuantificationResultsToTsv(Parameters.OutputFolder, filename);
                }
                else
                {
                    WritePeptideQuantificationResultsToTsv(Parameters.FlashLfqResults, Parameters.OutputFolder, filename, new List<string> { Parameters.SearchTaskId });
                }

                // write individual results
                if (Parameters.CurrentRawFileList.Count > 1 && Parameters.SearchParameters.WriteIndividualFiles)
                {
                    foreach (var file in Parameters.FlashLfqResults.Peaks)
                    {
                        WritePeakQuantificationResultsToTsv(Parameters.FlashLfqResults, Parameters.IndividualResultsOutputFolder,
                            file.Key.FilenameWithoutExtension + "_QuantifiedPeaks", new List<string> { Parameters.SearchTaskId, "Individual Spectra Files", file.Key.FullFilePathWithExtension });
                    }
                }
            }
        }

        private void WritePeptideQuantificationResultsToTsv(FlashLfqResults flashLFQResults, string outputFolder, string fileName, List<string> nestedIds)
        {
            var fullSeqPath = Path.Combine(outputFolder, fileName + ".tsv");

            flashLFQResults.WriteResults(null, fullSeqPath, null, null, true);

            FinishedWritingFile(fullSeqPath, nestedIds);
        }

        private void WritePeakQuantificationResultsToTsv(FlashLfqResults flashLFQResults, string outputFolder, string fileName, List<string> nestedIds)
        {
            var peaksPath = Path.Combine(outputFolder, fileName + ".tsv");

            flashLFQResults.WriteResults(peaksPath, null, null, null, true);

            FinishedWritingFile(peaksPath, nestedIds);
        }

        #endregion

        protected abstract void QuantificationAnalysis();


        /// <summary>
        /// Calculate estimated false-discovery rate (FDR) for peptide spectral matches (PSMs)
        /// </summary>
        protected void CalculateSpectralMatchFdr()
        {
            // TODO: because FDR is done before parsimony, if a PSM matches to a target and a decoy protein, there may be conflicts between how it's handled in parsimony and the FDR engine here
            // for example, here it may be treated as a decoy PSM, where as in parsimony it will be determined by the parsimony algorithm which is agnostic of target/decoy assignments
            // this could cause weird PSM FDR issues

            Status($"Estimating {SpectralMatchMoniker} FDR...", Parameters.SearchTaskId);
            new FdrAnalysisEngine(Parameters.AllSpectralMatches, Parameters.NumNotches, CommonParameters, FileSpecificParameters, new List<string> { Parameters.SearchTaskId }, analysisType: SpectralMatchMoniker, outputFolder: Parameters.OutputFolder).Run();

            // sort by q-value because of group FDR stuff
            // e.g. multiprotease FDR, non/semi-specific protease, etc
            Parameters.AllSpectralMatches = Parameters.AllSpectralMatches
                .OrderBy(p => p.FdrInfo.QValue)
                .ThenByDescending(p => p.Score)
                .ThenBy(p => p.FdrInfo.CumulativeTarget)
                .ToList();

            Status($"Done estimating {SpectralMatchMoniker} FDR!", Parameters.SearchTaskId);
        }

        protected void HistogramAnalysis()
        {
            if (Parameters.SearchParameters.DoHistogramAnalysis)
            {
                var limitedpsms_with_fdr = GetFilteredSpectralMatches(
                    includeDecoys: false,
                    includeContaminants: true,
                    includeAmbiguous: true).ToList();
                if (limitedpsms_with_fdr.Any())
                {
                    Status("Running histogram analysis...", new List<string> { Parameters.SearchTaskId });
                    var myTreeStructure = new BinTreeStructure();
                    myTreeStructure.GenerateBins(limitedpsms_with_fdr, Parameters.SearchParameters.HistogramBinTolInDaltons);
                    var writtenFile = Path.Combine(Parameters.OutputFolder, "MassDifferenceHistogram.tsv");
                    WriteTree(myTreeStructure, writtenFile);
                    FinishedWritingFile(writtenFile, new List<string> { Parameters.SearchTaskId });
                }
            }
        }

        
        protected void DoMassDifferenceLocalizationAnalysis()
        {
            if (Parameters.SearchParameters.DoLocalizationAnalysis)
            {
                Status("Running mass-difference localization analysis...", Parameters.SearchTaskId);
                for (int spectraFileIndex = 0; spectraFileIndex < Parameters.CurrentRawFileList.Count; spectraFileIndex++)
                {
                    CommonParameters combinedParams =
                        SetAllFileSpecificCommonParams(CommonParameters, Parameters.FileSettingsList[spectraFileIndex]);

                    var origDataFile = Parameters.CurrentRawFileList[spectraFileIndex];
                    Status("Running mass-difference localization analysis...",
                        new List<string> { Parameters.SearchTaskId, "Individual Spectra Files", origDataFile });
                    MsDataFile myMsDataFile = Parameters.MyFileManager.LoadFile(origDataFile, combinedParams);
                    new LocalizationEngine(
                        Parameters.AllSpectralMatches.Where(b => b.FullFilePath.Equals(origDataFile)).ToList(),
                        myMsDataFile, combinedParams, FileSpecificParameters,
                        new List<string> { Parameters.SearchTaskId, "Individual Spectra Files", origDataFile }).Run();
                    Parameters.MyFileManager.DoneWithFile(origDataFile);
                    ReportProgress(new ProgressEventArgs(100, "Done with localization analysis!",
                        new List<string> { Parameters.SearchTaskId, "Individual Spectra Files", origDataFile }));
                }

                // count different modifications observed
                new ModificationAnalysisEngine(Parameters.AllSpectralMatches, CommonParameters, FileSpecificParameters, new List<string> { Parameters.SearchTaskId }).Run();
            }
        }

        protected void WriteSpectralMatchResults(List<ProteinGroup> ProteinGroups = null)
        {
            Status($"Writing {SpectralMatchMoniker} results...", Parameters.SearchTaskId);

            var thresholdPsmList = GetFilteredSpectralMatches(
                includeDecoys: Parameters.SearchParameters.WriteDecoys,
                includeContaminants: Parameters.SearchParameters.WriteContaminants,
                includeAmbiguous: true).ToList();

            // If filter output is false, we need to write all psms, not just ones with Q-value < threshold
            List<SpectralMatch> filteredPsmListForOutput = Parameters.SearchParameters.WriteHighQValueSpectralMatches
                ? Parameters.AllSpectralMatches.Where(p =>
                        (Parameters.SearchParameters.WriteDecoys || !p.IsDecoy)
                        && (Parameters.SearchParameters.WriteContaminants || !p.IsContaminant))
                    .ToList()
                : thresholdPsmList;

            // write PSMs
            string writtenFile = Path.Combine(Parameters.OutputFolder, $"All{SpectralMatchMoniker}s.{SpectralMatchMoniker.ToLower()}tsv");
            WritePsmsToTsv(filteredPsmListForOutput, writtenFile);
            FinishedWritingFile(writtenFile, new List<string> { Parameters.SearchTaskId });

            // write PSMs for percolator
            // percolator native read format is .tab
            writtenFile = Path.Combine(Parameters.OutputFolder, $"All{SpectralMatchMoniker}s_FormattedForPercolator.tab");
            WritePsmsForPercolator(filteredPsmListForOutput, writtenFile);
            FinishedWritingFile(writtenFile, new List<string> { Parameters.SearchTaskId });

            string filterType = _filterType ?? "q-value";
            double filterCutoffForResultsCounts = _filterThreshold;
            int psmOrPeptideCountForResults = thresholdPsmList.Count(p => !p.IsDecoy);

            // write summary text
            if (_pepFilteringNotPerformed)
            {
                Parameters.SearchTaskResults.AddPsmPeptideProteinSummaryText(
                    $"PEP could not be calculated due to an insufficient number of {SpectralMatchMoniker}s. Results were filtered by q-value." +
                    Environment.NewLine);
            }
            Parameters.SearchTaskResults.AddPsmPeptideProteinSummaryText(
                "All target PSMs with " + filterType + " = " + Math.Round(filterCutoffForResultsCounts, 2) + ": " +
                psmOrPeptideCountForResults + Environment.NewLine);

            if (Parameters.SearchParameters.DoParsimony && ProteinGroups != null && ProteinGroups.Any())
            {
                Parameters.SearchTaskResults.AddTaskSummaryText(
                    "All target protein groups with q-value = 0.01 (1% FDR): " +
                    ProteinGroups.Count(b => b.QValue <= 0.01 && !b.IsDecoy) +
                    Environment.NewLine);
            }

            foreach (var psmFileGroup in SpectralMatchesGroupedByFile)
            {
                // FDR Analysis is performed again for each file. File specific results show the results that would be 
                // generated by analyzing one file by itself. Therefore, the FDR info should change between AllResults and FileSpecific
                string strippedFileName = Path.GetFileNameWithoutExtension(psmFileGroup.Key);
                var psmsForThisFile = psmFileGroup.ToList();
                new FdrAnalysisEngine(psmsForThisFile, Parameters.NumNotches, CommonParameters, FileSpecificParameters,
                    new List<string> { Parameters.SearchTaskId }).Run();

                FilterSpecificSpectralMatches(psmsForThisFile, out psmOrPeptideCountForResults);

                // write summary text
                Parameters.SearchTaskResults.AddTaskSummaryText("MS2 spectra in " + strippedFileName + ": " + Parameters.NumMs2SpectraPerFile[strippedFileName][0]);
                Parameters.SearchTaskResults.AddTaskSummaryText("Precursors fragmented in " + strippedFileName + ": " + Parameters.NumMs2SpectraPerFile[strippedFileName][1]);
                Parameters.SearchTaskResults.AddTaskSummaryText(strippedFileName + " target PSMs with " + filterType + " = " +
                    Math.Round(filterCutoffForResultsCounts, 2) + ": " + psmOrPeptideCountForResults + Environment.NewLine);

                // writes all individual spectra file search results to subdirectory
                if (Parameters.CurrentRawFileList.Count > 1 && Parameters.SearchParameters.WriteIndividualFiles)
                {
                    // create individual files subdirectory
                    Directory.CreateDirectory(Parameters.IndividualResultsOutputFolder);

                    // write PSMs
                    writtenFile = Path.Combine(Parameters.IndividualResultsOutputFolder, strippedFileName + $"_{SpectralMatchMoniker}s.{SpectralMatchMoniker.ToLower()}tsv");
                    WritePsmsToTsv(psmsForThisFile, writtenFile);
                    FinishedWritingFile(writtenFile, new List<string> { Parameters.SearchTaskId, "Individual Spectra Files", psmFileGroup.Key });

                    // write PSMs for percolator
                    writtenFile = Path.Combine(Parameters.IndividualResultsOutputFolder, strippedFileName + $"_{SpectralMatchMoniker}sFormattedForPercolator.tab");
                    WritePsmsForPercolator(psmsForThisFile, writtenFile);
                    FinishedWritingFile(writtenFile, new List<string> { Parameters.SearchTaskId, "Individual Spectra Files", psmFileGroup.Key });
                }
            }
        }

        #region Writing

        protected void WritePeptideResults()
        {
            Status("Writing peptide results...", Parameters.SearchTaskId);

            // write best (highest-scoring) PSM per peptide
            string filename = "All" + GlobalVariables.AnalyteType + "s.psmtsv";
            string writtenFile = Path.Combine(Parameters.OutputFolder, filename);
            List<SpectralMatch> peptides = Parameters.AllSpectralMatches
                .GroupBy(b => b.FullSequence)
                .Select(b => b.FirstOrDefault()).ToList();

            new FdrAnalysisEngine(peptides, Parameters.NumNotches, CommonParameters,
                FileSpecificParameters, new List<string> { Parameters.SearchTaskId },
                "Peptide").Run();

            FilterSpecificSpectralMatches(peptides, out int psmOrPeptideCountForResults);

            WritePsmsToTsv(peptides, writtenFile);
            FinishedWritingFile(writtenFile, new List<string> { Parameters.SearchTaskId });

            Parameters.SearchTaskResults.AddPsmPeptideProteinSummaryText(
                "All target " + GlobalVariables.AnalyteType.ToLower() + "s with " + _filterType +
                " = " + Math.Round(_filterThreshold, 2) + " : " + psmOrPeptideCountForResults);

            foreach (var file in SpectralMatchesGroupedByFile)
            {
                // write summary text
                var psmsForThisFile = file.ToList();
                string strippedFileName = Path.GetFileNameWithoutExtension(file.First().FullFilePath);
                var peptidesForFile = psmsForThisFile
                    .GroupBy(b => b.FullSequence)
                    .Select(b => b.FirstOrDefault())
                    .OrderByDescending(b => b.Score)
                    .ToList();

                // FDR Analysis is performed again for each file. File specific results show the results that would be 
                // generated by analyzing one file by itself. Therefore, the FDR info should change between AllResults and FileSpecific
                new FdrAnalysisEngine(peptidesForFile, Parameters.NumNotches, CommonParameters, FileSpecificParameters,
                    new List<string> { Parameters.SearchTaskId }, "Peptide").Run();

                FilterSpecificSpectralMatches(peptidesForFile, out psmOrPeptideCountForResults);

                Parameters.SearchTaskResults.AddTaskSummaryText(
                    strippedFileName + " Target " + GlobalVariables.AnalyteType.ToLower() + "s with "
                    + _filterType + " = " + Math.Round(_filterThreshold, 2)
                    + " : " + psmOrPeptideCountForResults + Environment.NewLine);

                // writes all individual spectra file search results to subdirectory
                if (Parameters.CurrentRawFileList.Count > 1 && Parameters.SearchParameters.WriteIndividualFiles)
                {
                    // create individual files subdirectory
                    Directory.CreateDirectory(Parameters.IndividualResultsOutputFolder);

                    // write best (highest-scoring) PSM per peptide
                    filename = "_" + GlobalVariables.AnalyteType + "s.psmtsv";
                    writtenFile = Path.Combine(Parameters.IndividualResultsOutputFolder, strippedFileName + filename);
                    WritePsmsToTsv(peptidesForFile, writtenFile);
                    FinishedWritingFile(writtenFile, new List<string> { Parameters.SearchTaskId, "Individual Spectra Files", file.First().FullFilePath });
                }
            }
        }

        internal abstract void WritePsmsToTsv(IEnumerable<SpectralMatch> psms, string filepath);
        private static void WriteTree(BinTreeStructure myTreeStructure, string writtenFile)
        {
            using (StreamWriter output = new(writtenFile))
            {
                output.WriteLine("MassShift\tCount\tCountDecoy\tCountTarget\tCountLocalizeableTarget\tCountNonLocalizeableTarget\tFDR\tArea 0.01t\tArea 0.255\tFracLocalizeableTarget\tMine\tUnimodID\tUnimodFormulas\tUnimodDiffs\tAA\tCombos\tModsInCommon\tAAsInCommon\tResidues\tprotNtermLocFrac\tpepNtermLocFrac\tpepCtermLocFrac\tprotCtermLocFrac\tFracWithSingle\tOverlappingFrac\tMedianLength\tUniprot");
                foreach (Bin bin in myTreeStructure.FinalBins.OrderByDescending(b => b.Count))
                {
                    output.WriteLine(bin.MassShift.ToString("F4", CultureInfo.InvariantCulture)
                        + "\t" + bin.Count.ToString(CultureInfo.InvariantCulture)
                        + "\t" + bin.CountDecoy.ToString(CultureInfo.InvariantCulture)
                        + "\t" + bin.CountTarget.ToString(CultureInfo.InvariantCulture)
                        + "\t" + bin.LocalizeableTarget.ToString(CultureInfo.InvariantCulture)
                        + "\t" + (bin.CountTarget - bin.LocalizeableTarget).ToString(CultureInfo.InvariantCulture)
                        + "\t" + (bin.Count == 0 ? double.NaN : (double)bin.CountDecoy / bin.Count).ToString("F3", CultureInfo.InvariantCulture)
                        + "\t" + Normal.CDF(0, 1, bin.ComputeZ(0.01)).ToString("F3", CultureInfo.InvariantCulture)
                        + "\t" + Normal.CDF(0, 1, bin.ComputeZ(0.255)).ToString("F3", CultureInfo.InvariantCulture)
                        + "\t" + (bin.CountTarget == 0 ? double.NaN : (double)bin.LocalizeableTarget / bin.CountTarget).ToString("F3", CultureInfo.InvariantCulture)
                        + "\t" + bin.Mine
                        + "\t" + bin.UnimodId
                        + "\t" + bin.UnimodFormulas
                        + "\t" + bin.UnimodDiffs
                        + "\t" + bin.AA
                        + "\t" + bin.Combos
                        + "\t" + string.Join(",", bin.ModsInCommon.OrderByDescending(b => b.Value).Where(b => b.Value > bin.CountTarget / 10.0).Select(b => b.Key + ":" + ((double)b.Value / bin.CountTarget).ToString("F3", CultureInfo.InvariantCulture)))
                        + "\t" + string.Join(",", bin.AAsInCommon.OrderByDescending(b => b.Value).Where(b => b.Value > bin.CountTarget / 10.0).Select(b => b.Key + ":" + ((double)b.Value / bin.CountTarget).ToString("F3", CultureInfo.InvariantCulture)))
                        + "\t" + string.Join(",", bin.ResidueCount.OrderByDescending(b => b.Value).Select(b => b.Key + ":" + b.Value))
                        + "\t" + (bin.LocalizeableTarget == 0 ? double.NaN : (double)bin.ProtNlocCount / bin.LocalizeableTarget).ToString("F3", CultureInfo.InvariantCulture)
                        + "\t" + (bin.LocalizeableTarget == 0 ? double.NaN : (double)bin.PepNlocCount / bin.LocalizeableTarget).ToString("F3", CultureInfo.InvariantCulture)
                        + "\t" + (bin.LocalizeableTarget == 0 ? double.NaN : (double)bin.PepClocCount / bin.LocalizeableTarget).ToString("F3", CultureInfo.InvariantCulture)
                        + "\t" + (bin.LocalizeableTarget == 0 ? double.NaN : (double)bin.ProtClocCount / bin.LocalizeableTarget).ToString("F3", CultureInfo.InvariantCulture)
                        + "\t" + bin.FracWithSingle.ToString("F3", CultureInfo.InvariantCulture)
                        + "\t" + ((double)bin.Overlapping / bin.CountTarget).ToString("F3", CultureInfo.InvariantCulture)
                        + "\t" + bin.MedianLength.ToString("F3", CultureInfo.InvariantCulture)
                        + "\t" + bin.UniprotID);
                }
            }
        }

        private static void WritePsmsForPercolator(List<SpectralMatch> psmList, string writtenFileForPercolator)
        {
            using (StreamWriter output = new StreamWriter(writtenFileForPercolator))
            {
                string searchType;
                if (psmList.Where(p => p != null).Any() && psmList[0].DigestionParams.DigestionAgent.Name != null && psmList[0].DigestionParams.DigestionAgent.Name == "top-down")
                {
                    searchType = "top-down";
                }
                else
                {
                    searchType = "standard";
                }

                string header = "SpecId\tLabel\tScanNr\t";
                header += string.Join("\t", PsmData.trainingInfos[searchType]);
                header += "\tPeptide\tProteins";

                output.WriteLine(header);

                StringBuilder directions = new StringBuilder();
                directions.Append("DefaultDirection\t-\t-");

                foreach (var headerVariable in PsmData.trainingInfos[searchType])
                {
                    directions.Append("\t");
                    directions.Append(PsmData.assumedAttributeDirection[headerVariable]);
                }

                output.WriteLine(directions.ToString());

                int idNumber = 0;
                psmList.OrderByDescending(p => p.Score);
                foreach (SpectralMatch psm in psmList.Where(p => p.PsmData_forPEPandPercolator != null))
                {
                    foreach (var peptide in psm.BestMatchingBioPolymersWithSetMods)
                    {
                        output.Write(idNumber.ToString());
                        output.Write('\t' + (peptide.Peptide.Parent.IsDecoy ? -1 : 1).ToString());
                        output.Write('\t' + psm.ScanNumber.ToString());
                        output.Write(psm.PsmData_forPEPandPercolator.ToString(searchType));
                        output.Write('\t' + (peptide.Peptide.PreviousResidue + "." + peptide.Peptide.FullSequence + "." + peptide.Peptide.NextResidue).ToString());
                        output.Write('\t' + peptide.Peptide.Parent.Accession ?? "NA");
                        output.WriteLine();
                    }
                    idNumber++;
                }
            }
        }

        protected void CompressIndividualFileResults()
        {
            if (Parameters.SearchParameters.CompressIndividualFiles && Directory.Exists(Parameters.IndividualResultsOutputFolder))
            {
                ZipFile.CreateFromDirectory(Parameters.IndividualResultsOutputFolder, Parameters.IndividualResultsOutputFolder + ".zip");
                Directory.Delete(Parameters.IndividualResultsOutputFolder, true);
            }
        }
        #endregion

    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EngineLayer;
using FlashLFQ;
using Omics.Digestion;
using Proteomics.ProteolyticDigestion;

namespace TaskLayer
{
    public class RnaPostSearchAnalysisTask : PostSearchAnalysisTaskParent
    {
        
        public new RnaPostSearchAnalysisParameters Parameters
        {
            get => (RnaPostSearchAnalysisParameters)base.Parameters;
            set => base.Parameters = value;
        }

        internal int MassDiffAcceptorNumNotches;

        public RnaPostSearchAnalysisTask() : base(MyTask.RnaSearch)
        {
            SpectralMatchMoniker = "OSM";
        }

        protected override MyTaskResults RunSpecific(string OutputFolder, List<DbForTask> dbFilenameList, List<string> currentRawFileList, string taskId,
            FileSpecificParameters[] fileSettingsList)
        {
            MyTaskResults = new MyTaskResults(this);
            return null;
        }

        public MyTaskResults Run()
        {
            // Stop loop if canceled
            if (GlobalVariables.StopLoops) { return Parameters.SearchTaskResults; }

            Parameters.AllSpectralMatches = Parameters.AllSpectralMatches.Where(psm => psm != null).ToList();
            Parameters.AllSpectralMatches.ForEach(psm => psm.ResolveAllAmbiguities());
            Parameters.AllSpectralMatches = Parameters.AllSpectralMatches.OrderByDescending(b => b.Score)
                .ThenBy(b => b.BioPolymerWithSetModsMonoisotopicMass.HasValue ? Math.Abs(b.ScanPrecursorMass - b.BioPolymerWithSetModsMonoisotopicMass.Value) : double.MaxValue)
                .GroupBy(b => (b.FullFilePath, b.ScanNumber, b.BioPolymerWithSetModsMonoisotopicMass)).Select(b => b.First()).ToList();

            CalculateSpectralMatchFdr();

            FilterAllSpectralMatches();
            DoMassDifferenceLocalizationAnalysis();
            TranscriptAnalysis();
            QuantificationAnalysis();

            ReportProgress(new ProgressEventArgs(100, "Done!", new List<string> { Parameters.SearchTaskId, "Individual Spectra Files" }));


            HistogramAnalysis();

            WriteSpectralMatchResults(ProteinGroups);
            WriteTranscriptResults();
            WriteFlashLFQResults();
            WritePeptideResults();
            CompressIndividualFileResults();

            return Parameters.SearchTaskResults;
        }

        private void TranscriptAnalysis()
        {
            if (!Parameters.SearchParameters.DoParsimony)
            {
                return;
            }

            Status("Constructing protein groups...", Parameters.SearchTaskId);

            List<SpectralMatch> psmsForProteinParsimony = Parameters.AllSpectralMatches;

            // run parsimony
            ProteinParsimonyResults proteinAnalysisResults = (ProteinParsimonyResults)new ProteinParsimonyEngine(psmsForProteinParsimony, 
                Parameters.SearchParameters.ModPeptidesAreDifferent, CommonParameters, FileSpecificParameters, new List<string> { Parameters.SearchTaskId }).Run();

            // score protein groups and calculate FDR
            ProteinScoringAndFdrResults proteinScoringAndFdrResults = 
                (ProteinScoringAndFdrResults)new ProteinScoringAndFdrEngine(proteinAnalysisResults.ProteinGroups, psmsForProteinParsimony,
                Parameters.SearchParameters.NoOneHitWonders, Parameters.SearchParameters.ModPeptidesAreDifferent, true, 
                CommonParameters, FileSpecificParameters, new List<string> { Parameters.SearchTaskId }).Run();

            ProteinGroups = proteinScoringAndFdrResults.SortedAndScoredProteinGroups;

            Status("Done constructing protein groups!", Parameters.SearchTaskId);
        }

        private void WriteTranscriptResults()
        {
            if (Parameters.SearchParameters.DoParsimony)
            {
                string fileName = "AllTranscriptGroups.tsv";

                if (Parameters.SearchParameters.DoLabelFreeQuantification)
                {
                    fileName = "AllQuantifiedTranscriptGroups.tsv";
                }

                //set peptide output values
                ProteinGroups.ForEach(x => x.GetIdentifiedPeptidesOutput(null));
                // write protein groups to tsv
                string writtenFile = Path.Combine(Parameters.OutputFolder, fileName);
                WriteProteinGroupsToTsv(ProteinGroups, writtenFile, new List<string> { Parameters.SearchTaskId });

                // write all individual file results to subdirectory
                // local protein fdr, global parsimony, global psm fdr
                if (Parameters.CurrentRawFileList.Count > 1 && Parameters.SearchParameters.WriteIndividualFiles)
                {
                    Directory.CreateDirectory(Parameters.IndividualResultsOutputFolder);
                }

                //write the individual result files for each datafile
                foreach (var fullFilePath in SpectralMatchesGroupedByFile.Select(v => v.Key))
                {
                    string strippedFileName = Path.GetFileNameWithoutExtension(fullFilePath);

                    List<SpectralMatch> psmsForThisFile = SpectralMatchesGroupedByFile.Where(p => p.Key == fullFilePath).SelectMany(g => g).ToList();
                    var subsetProteinGroupsForThisFile = ProteinGroups.Select(p => p.ConstructSubsetProteinGroup(fullFilePath, null)).ToList();

                    ProteinScoringAndFdrResults subsetProteinScoringAndFdrResults = (ProteinScoringAndFdrResults)new ProteinScoringAndFdrEngine(subsetProteinGroupsForThisFile, psmsForThisFile,
                        Parameters.SearchParameters.NoOneHitWonders, Parameters.SearchParameters.ModPeptidesAreDifferent,
                        false, CommonParameters, FileSpecificParameters, new List<string> { Parameters.SearchTaskId, "Individual Spectra Files", fullFilePath }).Run();

                    subsetProteinGroupsForThisFile = subsetProteinScoringAndFdrResults.SortedAndScoredProteinGroups;

                    Parameters.SearchTaskResults.AddTaskSummaryText("Target Transcript groups within 1 % FDR in " + strippedFileName + ": " + subsetProteinGroupsForThisFile.Count(b => b.QValue <= 0.01 && !b.IsDecoy));

                    // write individual spectra file protein groups results to tsv
                    if (Parameters.SearchParameters.WriteIndividualFiles && Parameters.CurrentRawFileList.Count > 1)
                    {
                        writtenFile = Path.Combine(Parameters.IndividualResultsOutputFolder, strippedFileName + "_TranscriptGroups.tsv");
                        WriteProteinGroupsToTsv(subsetProteinGroupsForThisFile, writtenFile, new List<string> { Parameters.SearchTaskId, "Individual Spectra Files", fullFilePath });
                    }

                    FilterSpecificSpectralMatches(psmsForThisFile, out int count); // Filter psms in place before writing
                    ReportProgress(new ProgressEventArgs(100, "Done!", new List<string> { Parameters.SearchTaskId, "Individual Spectra Files", fullFilePath }));
                }
            }
        }

        protected override void QuantificationAnalysis()
        {
            if (!Parameters.SearchParameters.DoLabelFreeQuantification)
            {
                return;
            }

            // pass quantification parameters to FlashLFQ
            Status("Quantifying...", Parameters.SearchTaskId);

            foreach (var file in Parameters.CurrentRawFileList)
            {
                Parameters.MyFileManager.DoneWithFile(file);
            }

            // construct file info for FlashLFQ
            List<SpectraFileInfo> spectraFileInfo;

            // get experimental design info
            string pathToFirstSpectraFile = Directory.GetParent(Parameters.CurrentRawFileList.First()).FullName;
            string assumedExperimentalDesignPath = Path.Combine(pathToFirstSpectraFile, GlobalVariables.ExperimentalDesignFileName);

            if (File.Exists(assumedExperimentalDesignPath))
            {
                // copy experimental design file to output folder
                string writtenFile = Path.Combine(Parameters.OutputFolder, Path.GetFileName(assumedExperimentalDesignPath));
                try
                {
                    File.Copy(assumedExperimentalDesignPath, writtenFile, overwrite: true);
                    FinishedWritingFile(writtenFile, new List<string> { Parameters.SearchTaskId });
                }
                catch
                {
                    Warn("Could not copy Experimental Design file to search task output. That's ok, the search will continue");
                }

                spectraFileInfo = ExperimentalDesign.ReadExperimentalDesign(assumedExperimentalDesignPath, Parameters.CurrentRawFileList, out var errors);

                if (errors.Any())
                {
                    Warn("Error reading experimental design file: " + errors.First() + ". Skipping quantification");
                    return;
                }
            }
            else
            {
                spectraFileInfo = new List<SpectraFileInfo>();

                for (int i = 0; i < Parameters.CurrentRawFileList.Count; i++)
                {
                    var file = Parameters.CurrentRawFileList[i];

                    // experimental design info passed in here for each spectra file
                    spectraFileInfo.Add(new SpectraFileInfo(fullFilePathWithExtension: file, condition: "", biorep: i, fraction: 0, techrep: 0));
                }
            }

            // get PSMs to pass to FlashLFQ
            var unambiguousPsmsBelowOnePercentFdr = GetFilteredSpectralMatches(
                includeDecoys: false,
                includeContaminants: true,
                includeAmbiguous: false);


            // if protein groups were not constructed, just use accession numbers
            var psmToProteinGroups = new Dictionary<SpectralMatch, List<FlashLFQ.ProteinGroup>>();
            var accessionToPg = new Dictionary<string, FlashLFQ.ProteinGroup>();
            foreach (var psm in unambiguousPsmsBelowOnePercentFdr)
            {
                var proteins = psm.BestMatchingBioPolymersWithSetMods.Select(b => b.Peptide.Parent).Distinct();

                foreach (var protein in proteins)
                {
                    if (!accessionToPg.ContainsKey(protein.Accession))
                    {
                        accessionToPg.Add(protein.Accession, new FlashLFQ.ProteinGroup(protein.Accession, string.Join("|", protein.GeneNames.Select(p => p.Item2).Distinct()), protein.Organism));
                    }

                    if (psmToProteinGroups.TryGetValue(psm, out var proteinGroups))
                    {
                        proteinGroups.Add(accessionToPg[protein.Accession]);
                    }
                    else
                    {
                        psmToProteinGroups.Add(psm, new List<FlashLFQ.ProteinGroup> { accessionToPg[protein.Accession] });
                    }
                }
            }
            //group psms by file
            var psmsGroupedByFile = unambiguousPsmsBelowOnePercentFdr.GroupBy(p => p.FullFilePath);

            // some PSMs may not have protein groups (if 2 peptides are required to construct a protein group, some PSMs will be left over)
            // the peptides should still be quantified but not considered for protein quantification
            var undefinedPg = new FlashLFQ.ProteinGroup("UNDEFINED", "", "");
            //sort the unambiguous psms by protease to make MBR compatible with multiple proteases
            Dictionary<DigestionAgent, List<SpectralMatch>> proteaseSortedPsms = new Dictionary<DigestionAgent, List<SpectralMatch>>();
            Dictionary<DigestionAgent, FlashLfqResults> proteaseSortedFlashLFQResults = new Dictionary<DigestionAgent, FlashLfqResults>();

            foreach (IDigestionParams dp in Parameters.ListOfDigestionParams)
            {
                if (!proteaseSortedPsms.ContainsKey(dp.DigestionAgent))
                {
                    proteaseSortedPsms.Add(dp.DigestionAgent, new List<SpectralMatch>());
                }
            }
            foreach (var psm in unambiguousPsmsBelowOnePercentFdr)
            {
                if (!psmToProteinGroups.ContainsKey(psm))
                {
                    psmToProteinGroups.Add(psm, new List<FlashLFQ.ProteinGroup> { undefinedPg });
                }

                proteaseSortedPsms[psm.DigestionParams.DigestionAgent].Add(psm);
            }

            // pass PSM info to FlashLFQ
            var flashLFQIdentifications = new List<Identification>();
            foreach (var spectraFile in psmsGroupedByFile)
            {
                var rawfileinfo = spectraFileInfo.Where(p => p.FullFilePathWithExtension.Equals(spectraFile.Key)).First();

                foreach (var psm in spectraFile)
                {
                    flashLFQIdentifications.Add(new Identification(rawfileinfo, psm.BaseSequence, psm.FullSequence,
                        psm.BioPolymerWithSetModsMonoisotopicMass.Value, psm.ScanRetentionTime, psm.ScanPrecursorCharge, psmToProteinGroups[psm]));
                }
            }

            // run FlashLFQ
            var FlashLfqEngine = new FlashLfqEngine(
                allIdentifications: flashLFQIdentifications,
                normalize: Parameters.SearchParameters.Normalize,
                ppmTolerance: Parameters.SearchParameters.QuantifyPpmTol,
                matchBetweenRunsPpmTolerance: Parameters.SearchParameters.QuantifyPpmTol,  // If these tolerances are not equivalent, then MBR will falsely classify peptides found in the initial search as MBR peaks
                matchBetweenRuns: Parameters.SearchParameters.MatchBetweenRuns,
                silent: true,
                maxThreads: CommonParameters.MaxThreadsToUsePerFile);

            if (flashLFQIdentifications.Any())
            {
                Parameters.FlashLfqResults = FlashLfqEngine.Run();
            }

            // get protein intensity back from FlashLFQ
            if (ProteinGroups != null && Parameters.FlashLfqResults != null)
            {
                foreach (var proteinGroup in ProteinGroups)
                {
                    proteinGroup.FilesForQuantification = spectraFileInfo;
                    proteinGroup.IntensitiesByFile = new Dictionary<SpectraFileInfo, double>();

                    foreach (var spectraFile in proteinGroup.FilesForQuantification)
                    {
                        if (Parameters.FlashLfqResults.ProteinGroups.TryGetValue(proteinGroup.ProteinGroupName, out var flashLfqProteinGroup))
                        {
                            proteinGroup.IntensitiesByFile.Add(spectraFile, flashLfqProteinGroup.GetIntensity(spectraFile));
                        }
                        else
                        {
                            proteinGroup.IntensitiesByFile.Add(spectraFile, 0);
                        }
                    }
                }
            }
        }

        #region Writing

        /// <summary>
        /// Writes PSMs to a .psmtsv file. If multiplex labeling was used (e.g., TMT), the intensities of the diagnostic ions are
        /// included, with each ion being reported in a separate column.
        /// </summary>
        /// <param name="psms">PSMs to be written</param>
        /// <param name="filePath">Full file path, up to and including the filename and extensioh. </param>
        internal override void WritePsmsToTsv(IEnumerable<SpectralMatch> psms, string filePath)
        {
            WritePsmsToTsv(psms, filePath, Parameters.SearchParameters.ModsToWriteSelection);
        }

        #endregion
    }
}

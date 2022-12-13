using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Easy.Common.Extensions;
using EngineLayer;
using EngineLayer.ClassicSearch;
using EngineLayer.Indexing;
using EngineLayer.ModernSearch;
using FlashLFQ;
using IO.MzML;
using IO.ThermoRawFileReader;
using MassSpectrometry;
using MzLibUtil;
using Nett;
using NUnit.Framework;
using Proteomics;
using Proteomics.ProteolyticDigestion;
using TaskLayer;
using UsefulProteomicsDatabases;

namespace Test
{
    [TestFixture]
    public static class AADeleteThis
    {
        [Test]
        public static void RunModernSearchOnOnlyOneOfInterest()
        {
            // loading in 
            string tomlPath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TempTestData\Task3-SearchTaskconfig.toml");
            var task = Toml.ReadFile<SearchTask>(tomlPath, MetaMorpheusTask.tomlConfig);
            var commonParams = task.CommonParameters;
            commonParams.MaxThreadsToUsePerFile = 1;
            var searchParams = task.SearchParameters;
            var fileSpecificCommon = task.FileSpecificParameters;
            var fileSpecificList = new List<FileSpecificParameters> {null};
            var fileSpecific = fileSpecificList.ToArray();

            MassDiffAcceptor massDiffAcceptor = SearchTask.GetMassDiffAcceptor(commonParams.PrecursorMassTolerance,
                searchParams.MassDiffAcceptorType, searchParams.CustomMdac);
            var variableMods = new List<Modification>();
            var fixedMods = new List<Modification>();

            string spectraPath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TempTestData\221110_CaMyoUbiqCytCHgh_130541641_5%_Sample28_25IW_-averaged-calib.mzML");
            var myMsDataFile = Mzml.LoadAllStaticData(spectraPath);

            string databasePath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TempTestData\customProtStandardDB8.xml");
            var proteinList = ProteinDbLoader.LoadProteinXML(databasePath, true, DecoyType.None,
                GlobalVariables.AllModsKnown, false, new List<string>(),
                out Dictionary<string, Modification> unknownMods);



            // running the search stuff
            //var indexEngine = new IndexingEngine(proteinList, variableMods, fixedMods, null, null, null, 1,
            //    DecoyType.None, commonParams, null, searchParams.MaxFragmentSize, false, new List<FileInfo>(),
            //    TargetContaminantAmbiguity.RemoveContaminant, new List<string>());
            //var indexResults = (IndexingResults)indexEngine.Run();
            //PeptideSpectralMatch[] allPsmsArray = new PeptideSpectralMatch[listOfSortedms2Scans.Length];

            var listOfSortedms2Scans = MetaMorpheusTask.GetMs2Scans(myMsDataFile, null, commonParams)
                .OrderBy(b => b.PrecursorMass).ToArray();

            
            Ms2ScanWithSpecificMass[] searchThese = listOfSortedms2Scans.Where(p => p.OneBasedScanNumber == 12).ToArray();
            PeptideSpectralMatch[] allPsmsArray = new PeptideSpectralMatch[searchThese.Length];

            //new ModernSearchEngine(allPsmsArray, searchThese, indexResults.PeptideIndex, indexResults.FragmentIndex, 0,
            //    commonParams, null, massDiffAcceptor, searchParams.MaximumMassThatFragmentIonScoreIsDoubled,
            //    new List<string>()).Run();

            new ClassicSearchEngine(allPsmsArray, searchThese, variableMods, fixedMods, null, null, null, proteinList,
                massDiffAcceptor, commonParams, fileSpecificCommon, null, new List<string>(), false).Run();

            var nonNullPsms = allPsmsArray.Where(p => p != null).ToList();
            nonNullPsms.ForEach(p => p.ResolveAllAmbiguities());

            string outpath = @"D:\Projects\Top Down MetaMorpheus\ChimeraValidation\Outputs";
            if (false)
            {
                string situation = "NoMaxPrecursorChargeForMS2";
                PrintMs2WithSpecificMass(Path.Combine(outpath, $"{situation}_MS2.csv"), searchThese);
                PrintPSMS(Path.Combine(outpath, $"{situation}_NonNullPsms.csv"), nonNullPsms);
            }

            Dictionary<string, int[]> numMs2SpectraPerFile = new Dictionary<string, int[]>();
            numMs2SpectraPerFile.Add(Path.GetFileNameWithoutExtension(spectraPath),
                new int[] { myMsDataFile.GetAllScansList().Count(p => p.MsnOrder == 2), listOfSortedms2Scans.Length });
            List<DbForTask> databaseForTask = new() { new DbForTask(databasePath, false) };
            PostSearchAnalysisParameters parameters = new PostSearchAnalysisParameters
            {
                SearchTaskResults = (MyTaskResults)task.GetType().GetProperty("MyTaskResults", BindingFlags.NonPublic)?.GetValue(task),
                SearchTaskId = "Task3-Search",
                SearchParameters = searchParams,
                ProteinList = proteinList,
                AllPsms = nonNullPsms,
                VariableModifications = variableMods,
                FixedModifications = fixedMods,
                ListOfDigestionParams = new HashSet<DigestionParams>(new List<DigestionParams>{commonParams.DigestionParams}),
                CurrentRawFileList = new List<string>() {spectraPath},
                MyFileManager = new MyFileManager(true),
                NumNotches = task.GetNumNotches(searchParams.MassDiffAcceptorType, searchParams.CustomMdac),
                OutputFolder = outpath,
                IndividualResultsOutputFolder = Path.Combine(outpath, "Individual File Results"),
                FlashLfqResults = null,
                FileSettingsList = fileSpecific,
                NumMs2SpectraPerFile = numMs2SpectraPerFile,
                DatabaseFilenameList = databaseForTask
            };
            PostSearchAnalysisTask postProcessing = new PostSearchAnalysisTask
            {
                Parameters = parameters,
                FileSpecificParameters = fileSpecificCommon,
                CommonParameters = commonParams
            };

            var results = postProcessing.Run();







        }

        private static void PrintMs2WithSpecificMass(string filepath, IEnumerable<Ms2ScanWithSpecificMass> scans)
        {
            using (StreamWriter sw = new StreamWriter(File.Create(filepath)))
            {
                sw.WriteLine("Charge,Precursor Mono Peak Mz,Precursor Mass,Deconvoluted Masses");
                foreach (var scan in scans)
                {
                    sw.WriteLine($"{scan.PrecursorCharge},{scan.PrecursorMonoisotopicPeakMz},{scan.PrecursorMass},{scan.ExperimentalFragments.Count()}");
                }
            }
        }

        private static void PrintPSMS(string filepath, IEnumerable<PeptideSpectralMatch> scans)
        {
            using (StreamWriter sw = new StreamWriter(File.Create(filepath)))
            {
                sw.WriteLine("Charge,Precursor Mono Peak Mz,Precursor Mass,Score");
                foreach (var scan in scans)
                {
                    sw.WriteLine($"{scan.ScanPrecursorCharge},{scan.ScanPrecursorMonoisotopicPeakMz},{scan.ScanPrecursorMass},{scan.Score}");
                }
            }
        }

    }

    
}

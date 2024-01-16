using EngineLayer;
using MassSpectrometry;
using MzLibUtil;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chemistry;
using Nett;
using TaskLayer;
using UsefulProteomicsDatabases;

namespace Test.Transcriptomics
{
    internal class PfizerTest
    {
        [Test]
        public static void PfizerFirstAttempt()
        {
            // paths
            string dbPath = @"D:\DataFiles\RnaTestSets\PfizerData\PfizerBNT-162b2.fasta";
            string dataFilePath = @"D:\DataFiles\RnaTestSets\PfizerData\20220525_WRMnew_B.raw";
            string modFile = Path.Combine(GlobalVariables.DataDir, "Mods", "RnaMods.txt");


            // setup
            List<DbForTask> dbForTasks = new() { new DbForTask(dbPath, false) };
            List<string> spectraPaths = new() { dataFilePath };

            var mods = PtmListLoader.ReadModsFromFile(modFile, out var errorMods)
                .ToDictionary(p => p.IdWithMotif, p => p);
            var methyl = mods["Methylation on T"];
            var cyclicPhosphate = mods["Cyclic Phosphate on X"];
            var pfizerfiveprimecap = mods["Pfizer 5'-Cap on X"];

            List<(string, string)> fixedMods = new()
            {
                (methyl.ModificationType, methyl.IdWithMotif),
                (pfizerfiveprimecap.ModificationType, pfizerfiveprimecap.IdWithMotif)
            };
            List<(string, string)> variableMods = new()
            {
                (cyclicPhosphate.ModificationType, cyclicPhosphate.IdWithMotif)
            };


            CommonParameters commonParams = new
            (
                dissociationType: DissociationType.CID,
                deconvolutionMaxAssumedChargeState: -20,
                deconvolutionIntensityRatio: 3,
                deconvolutionMassTolerance: new PpmTolerance(20),
                scoreCutoff: 5,
                totalPartitions: 1,
                maxThreadsToUsePerFile: 1,
                doPrecursorDeconvolution: true,
                useProvidedPrecursorInfo: false,
                listOfModsFixed: fixedMods,
                listOfModsVariable: variableMods
            );

            RnaSearchParameters searchParams = new()
            {
                DisposeOfFileWhenDone = true,
                FragmentIonTolerance = new PpmTolerance(20),
                PrecursorMassTolerance = new PpmTolerance(20),
                DecoyType = DecoyType.Reverse,
                MatchAllCharges = true,
                MassDiffAcceptorType = MassDiffAcceptorType.Custom,
                CustomMdac = "Custom interval [-10,10]",
                DigestionParams = new(
                    rnase: "RNase T1",
                    maxMods: 6,
                    maxModificationIsoforms: 2048,
                    minLength: 6
                ),
            };

            RnaSearchTask searchTask = new RnaSearchTask()
            {
                CommonParameters = commonParams,
                RnaSearchParameters = searchParams,
            };

            string outputFolder = GetFinalPath(@"D:\Projects\RNA\TestData\Pfizer\SearchingPfizerData_WRMnew_B");
            var taskList = new List<(string, MetaMorpheusTask)> { ("SearchTask", searchTask) };
            var runner = new EverythingRunnerEngine(taskList, spectraPaths, dbForTasks, outputFolder);
            runner.Run();

        }

        internal static string GetFinalPath(string path)
        {
            // check if a file with this name already exists, if so add a number to the end within parenthesis. If that file still exists, increment the number by one and try again
            string end = Path.GetFileName(path);
            int fileCount = 1;
            string finalPath = path;
            while (System.IO.File.Exists(finalPath) || Directory.Exists(finalPath))
            {
                finalPath = path.Replace(end, $"{end}({fileCount})");
                fileCount++;
            }
            return finalPath;
        }
    }
}

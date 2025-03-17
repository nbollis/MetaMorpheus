using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nett;
using NUnit.Framework;
using TaskLayer;

namespace Test.UtilitiesTest
{
    class RunIt
    {

        [Test]
        public static void TestIt()
        {
            string outputFolder = @"D:\Projects\Chimeras\UniqueIonsRequired\FirstTest_SixToTen";
            string searchTomlPath = @"B:\Users\Nic\Chimeras\TopDown_Analysis\Jurkat\Search_WithChimeras.toml";
            string[] dbPath =
            [
                @"B:\Users\Nic\Chimeras\Mann_11cell_analysis\uniprotkb_human_proteome_AND_reviewed_t_2024_03_22.xml",
                @"B:\Users\Nic\Chimeras\InternalMMAnalysis\TopDown_Jurkat\GenerateChimericLibrary_107_1\Task2SearchTask\SpectralLibrary_2025-03-07-20-49-37.msp"
            ];
            string[] specPath =
            [
                @"B:\RawSpectraFiles\JurkatTopDown\107_CalibratedAveraged_Rep2\Task1-AveragingTask\02-18-20_jurkat_td_rep2_fract5-calib-averaged.mzML",
                @"B:\RawSpectraFiles\JurkatTopDown\107_CalibratedAveraged_Rep2\Task1-AveragingTask\02-18-20_jurkat_td_rep2_fract6-calib-averaged.mzML"
            ];

            var dbs = dbPath.Select(p => new DbForTask(p, false)).ToList();

            // Generate search task with different UniqueIonsRequired
            var taskList = new List<(string, MetaMorpheusTask)>();
            for (int i = 5; i < 10; i++)
            {
                var task = Toml.ReadFile<SearchTask>(searchTomlPath, MetaMorpheusTask.tomlConfig);

                string name = $"SearchTask-{i}-UniqueIonsRequired";
                task.CommonParameters.UniqueIonsRequired = i;
                
                taskList.Add((name, task));
            }

            var engine = new EverythingRunnerEngine(taskList, specPath.ToList(), dbs, outputFolder);
            engine.Run();
        }
    }
}

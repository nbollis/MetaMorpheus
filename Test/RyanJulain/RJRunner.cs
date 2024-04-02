using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using NUnit.Framework;
using Proteomics;
using Proteomics.ProteolyticDigestion;
using UsefulProteomicsDatabases;

namespace Test.RyanJulain
{
    internal class RJRunner
    {
        internal static string HumanDatabasePath = @"B:\Users\Nic\Chimeras\TopDown_Analysis\Jurkat\uniprotkb_human_proteome_AND_reviewed_t_2024_03_22.xml";
        internal static string YeastDatabasePath = @"D:\Proteomes\uniprotkb_yeast_proteome_AND_model_orga_2024_03_27.xml";
        internal static string EcoliDatabase = @"D:\Proteomes\uniprotkb_ecoli_proteome_AND_reviewed_t_2024_03_25.xml";




        [Test]
        public static void IndexTryptophanFragments()
        {

            var ecoli = new TryptophanExplorer(EcoliDatabase, 0, "Ecoli");
            ecoli.FindNumberOfFragmentsNeededToDifferentiate();


            var human = new TryptophanExplorer(HumanDatabasePath, 2, "Human");
            human.CreateTryptophanFragmentIndex();
            Task.Delay(2000);
            human.CreateFragmentHistogramFile();
            //human.FindNumberOfFragmentsNeededToDifferentiate();
        }

        [Test]
        public static void RunMany()
        {
            for (int i = 0; i < 3; i++)
            {
                var analysis = new TryptophanExplorer(EcoliDatabase, i, "Ecoli");
                analysis.CreateTryptophanFragmentIndex();
                Task.Delay(2000);
                analysis.CreateFragmentHistogramFile();
                Task.Delay(2000);
                analysis.FindNumberOfFragmentsNeededToDifferentiate();
                Task.Delay(2000);

                analysis = new TryptophanExplorer(YeastDatabasePath, i, "Yeast");
                analysis.CreateTryptophanFragmentIndex();
                Task.Delay(2000);
                analysis.CreateFragmentHistogramFile();
                Task.Delay(2000);
                analysis.FindNumberOfFragmentsNeededToDifferentiate();
                Task.Delay(2000);
            }
        }


        [Test]
        public static void TESTNAME()
        {
            var explorers = new List<TryptophanExplorer>()
            {
                new TryptophanExplorer(HumanDatabasePath, 0, "Human"),
                new TryptophanExplorer(EcoliDatabase, 0, "Ecoli"),
                new TryptophanExplorer(YeastDatabasePath, 0, "Yeast"),
                new TryptophanExplorer(EcoliDatabase, 1, "Ecoli"),
                new TryptophanExplorer(YeastDatabasePath, 1, "Yeast"),
                new TryptophanExplorer(EcoliDatabase, 2, "Ecoli"),
                new TryptophanExplorer(YeastDatabasePath, 2, "Yeast"),
                new TryptophanExplorer(HumanDatabasePath, 1, "Human"),
                new TryptophanExplorer(HumanDatabasePath, 2, "Human"),
            };

            foreach (var explorer in explorers)
            {
                explorer.CreateTryptophanFragmentIndex();
                Task.Delay(2000);
                explorer.CreateFragmentHistogramFile();
                Task.Delay(2000);
                explorer.FindNumberOfFragmentsNeededToDifferentiate();
                Task.Delay(2000);
            }
            explorers.CombineFragmentCountHistogram();
            explorers.CombineAllFragmentCountHistograms();
        }

        #region Temp File Manipulation Methods

        [Test]
        public static void CopyRawFilesToCommonDir()
        {
            string dirpath = @"B:\RawSpectraFiles\Mann_11cell_lines";
            foreach (var directory in Directory.GetDirectories(dirpath))
            {
                var combinedPath = Path.Combine(directory, "Combined");
                if (!Directory.Exists(combinedPath))
                    Directory.CreateDirectory(combinedPath);
                else
                    continue;

                foreach (var file in Directory.GetFiles(directory, "*.raw", SearchOption.AllDirectories))
                {
                    var destinationPath = Path.Combine(combinedPath, Path.GetFileName(file));
                    File.Copy(file, destinationPath);
                }
            }
        }

        #endregion
    }
}

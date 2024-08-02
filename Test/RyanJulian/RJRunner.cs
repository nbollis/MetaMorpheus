using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using NUnit.Framework;
using Proteomics;
using UsefulProteomicsDatabases;


namespace Test.RyanJulian
{
    internal class RJRunner
    {
        internal static string HumanDatabasePath = @"D:\Projects\RadicalFragmentation\FragmentAnalysis\Databases\uniprotkb_human_proteome_AND_reviewed_t_2024_03_22.xml";
        internal static string YeastDatabasePath = @"D:\Projects\RadicalFragmentation\FragmentAnalysis\Databases\uniprotkb_yeast_proteome_AND_model_orga_2024_03_27.xml";
        internal static string EcoliDatabase = @"D:\Projects\RadicalFragmentation\FragmentAnalysis\Databases\uniprotkb_ecoli_proteome_AND_reviewed_t_2024_03_25.xml";
        internal static string OutputDirectory = @"D:\Projects\RadicalFragmentation\FragmentAnalysis";

        public static IEnumerable<TryptophanFragmentExplorer> GetTryptophanAnalyses()
        {
            for (int i = 0; i < 4; i++)
            {
                for (int j = 1; j < 3; j++)
                {
                    yield return new TryptophanFragmentExplorer(EcoliDatabase, i, "Ecoli", j, OutputDirectory);
                    yield return new TryptophanFragmentExplorer(YeastDatabasePath, i, "Yeast", j, OutputDirectory);
                    yield return new TryptophanFragmentExplorer(HumanDatabasePath, i, "Human", j, OutputDirectory);
                }
            }
        }

        public static IEnumerable<CysteineFragmentExplorer> GetCysteineFragmentExplorers()
        {
            int maxFrags = 6;
            for (int i = 0; i < 4; i++)
            {
                for (int j = 1; j < 3; j++)
                {
                    yield return new CysteineFragmentExplorer(EcoliDatabase, i, "Ecoli", j, maxFrags, OutputDirectory);
                    yield return new CysteineFragmentExplorer(YeastDatabasePath, i, "Yeast", j, maxFrags, OutputDirectory);
                    yield return new CysteineFragmentExplorer(HumanDatabasePath, i, "Human", j, maxFrags, OutputDirectory);
                }
            }
        }

        [Test]
        public static void OverNighter()
        {
            RunManyCys();
            RunManyTryp();
            Plotter();
        }

        [Test]
        public static void Plotter()
        {
            List<RadicalFragmentationExplorer> toPlot = new();
            foreach (var analysis in GetTryptophanAnalyses())
            {
                //if (analysis.NumberOfMods == 3 && analysis.AmbiguityLevel == 2)
                //    continue;
                //if (analysis.NumberOfMods == 3 && analysis.Species == "Human")
                //    continue;
                toPlot.Add(analysis);
            }
            toPlot.CreatePlots();


            toPlot.Clear();
            foreach (var analysis in GetCysteineFragmentExplorers())
            {
                //if (analysis.NumberOfMods == 3 && analysis.AmbiguityLevel == 2)
                //    continue;
                //if (analysis.NumberOfMods == 3 && analysis.Species == "Human")
                //    continue;
                toPlot.Add(analysis);
            }
            toPlot.CreatePlots();

            //foreach (var tryptophanFragmentExplorer in GetTryptophanAnalyses().DistinctBy(p => p.Species))
            //{
            //    tryptophanFragmentExplorer.CreateAminoAcidFrequencyFigure();
            //    tryptophanFragmentExplorer.CreateAminoAcidFrequencyFigure(true);
            //}
        }

        [Test]
        public static void RunManyTryp()
        {
            foreach (var analysis in GetTryptophanAnalyses())
            {
                analysis.CreateIndexedFile();
            }

            foreach (var analysis in GetTryptophanAnalyses())
            {
                analysis.CreateFragmentHistogramFile();
            }

            foreach (var analysis in GetTryptophanAnalyses())
            {
                _ = analysis.FindNumberOfFragmentsNeededToDifferentiate();
            }
        }

        [Test]
        public static void RunManyCys()
        {
            foreach (var analysis in GetCysteineFragmentExplorers())
            {
                analysis.CreateIndexedFile();
            }

            foreach (var analysis in GetCysteineFragmentExplorers())
            {
                analysis.CreateFragmentHistogramFile();
            }

            foreach (var analysis in GetCysteineFragmentExplorers())
            {
                _ = analysis.FindNumberOfFragmentsNeededToDifferentiate();
            }
        }

        [Test]
        public static void Combine()
        {
            var analysis = new TryptophanFragmentExplorer(HumanDatabasePath, 2, "Human", 2);
            analysis.Override = true;
            analysis.CombineFragmentHistograms();
            analysis.CombineMinFragmentsNeeded();
        }

     
    }
}

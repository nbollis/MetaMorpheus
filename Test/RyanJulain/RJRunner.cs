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
using Proteomics.ProteolyticDigestion;
using Test.RyanJulian;
using UsefulProteomicsDatabases;

namespace Test.RyanJulain
{
    internal class RJRunner
    {
        internal static string HumanDatabasePath = @"B:\Users\Nic\Chimeras\TopDown_Analysis\Jurkat\uniprotkb_human_proteome_AND_reviewed_t_2024_03_22.xml";
        internal static string YeastDatabasePath = @"D:\Proteomes\uniprotkb_yeast_proteome_AND_model_orga_2024_03_27.xml";
        internal static string EcoliDatabase = @"D:\Proteomes\uniprotkb_ecoli_proteome_AND_reviewed_t_2024_03_25.xml";

        [Test]
        public static void RunMany()
        {
            TryptophanFragmentExplorer analysis;
            for (int i = 0; i < 4; i++)
            {
                for (int j = 1; j < 3; j++)
                {
                    analysis = new TryptophanFragmentExplorer(EcoliDatabase, i, "Ecoli", j);
                    analysis.CreateIndexedFile();
                    analysis.CreateFragmentHistogramFile();
                    analysis.FindNumberOfFragmentsNeededToDifferentiate();

                    analysis = new TryptophanFragmentExplorer(YeastDatabasePath, i, "Yeast", j);
                    analysis.CreateIndexedFile();
                    analysis.CreateFragmentHistogramFile();
                    analysis.FindNumberOfFragmentsNeededToDifferentiate();

                    analysis = new TryptophanFragmentExplorer(HumanDatabasePath, i, "Human", j);
                    analysis.CreateIndexedFile();
                    analysis.CreateFragmentHistogramFile();
                    analysis.FindNumberOfFragmentsNeededToDifferentiate();
                }
            }

            analysis = new TryptophanFragmentExplorer(HumanDatabasePath, 2, "Human", 2);
            analysis.Override = true;
            analysis.CombineFragmentHistograms();
            analysis.CombineMinFragmentsNeeded();
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Easy.Common.Interfaces;
using EngineLayer;
using MassSpectrometry;
using Nett;
using NUnit.Framework;
using Omics.Fragmentation;
using Readers;
using TaskLayer;
using Transcriptomics;
using Transcriptomics.Digestion;

namespace Test.Transcriptomics
{
   // [Ignore("test")]
    internal class Pfizer
    {
        class FragmentMap
        {
            public string Sequence { get; set; }
            public int StartResidue { get; set; }
            public int EndResidue { get; set; }

            public FragmentMap(string sequence, int start, int end)
            {
                Sequence = sequence;
                StartResidue = start;
                EndResidue = end;
            }

            public static IEnumerable<FragmentMap> CreateFragments(List<OsmFromTsv> osms)
            {
                foreach (var osm in osms.Where(p => p.AmbiguityLevel == "1"))
                {
                    string sequence = osm.BaseSeq;
                    foreach (var startAndEnd in osm.StartAndEndResiduesInParentSequence.Split('|'))
                    {
                        var split = startAndEnd.Split(" to ");
                        int start = int.Parse(split[0].Replace("[", ""));
                        int end = int.Parse(split[1].Replace("]",""));
                        yield return new FragmentMap(sequence, start, end);
                    }
                }
            }

            public static List<bool> MapFragmentsToSequence(string sequence, List<FragmentMap> fragments)
            {
                // Initialize a list to represent sequence coverage, initially all false
                List<bool> coverage = new List<bool>(new bool[sequence.Length]);

                foreach (var fragment in fragments)
                {
                    // Adjust the start and end indices to zero-based indexing
                    int startIndex = fragment.StartResidue - 1;
                    int endIndex = fragment.EndResidue - 1;

                    // Mark the corresponding positions in the coverage list as true
                    for (int i = startIndex; i <= endIndex && i < sequence.Length; i++)
                    {
                        coverage[i] = true;
                    }
                }

                return coverage;
            }

            public static List<(bool, int)> MapFragmentsToSequenceWithCount(string sequence, List<FragmentMap> fragments)
            {
                // Initialize a list to represent sequence coverage, initially all false
                var coverage = new (bool, int)[sequence.Length];

                foreach (var fragment in fragments)
                {
                    // Adjust the start and end indices to zero-based indexing
                    int startIndex = fragment.StartResidue - 1;
                    int endIndex = fragment.EndResidue - 1;

                    // Mark the corresponding positions in the coverage list as true
                    for (int i = startIndex; i <= endIndex && i < sequence.Length; i++)
                    {
                        coverage[i] = (true, coverage[i].Item2 + 1);
                    }
                }

                return coverage.ToList();
            }
        }


        [Test]
        public static void GPTMDTest()
        {
            string fastaPath = @"D:\DataFiles\RnaTestSets\PfizerData\PfizerBNT-162b2.fasta";
            var dbForTask = new List<DbForTask>() { new (fastaPath, false) };
            string dataFilePath = @"D:\DataFiles\RnaTestSets\PfizerData\20220525_WRMnew_B.raw";
            var outputFolder = @"D:\Projects\RNA\TestData\Pfizer\GPTMDTest";
            string searchToml =
                @"D:\DataFiles\RnaTestSets\PfizerData\2024-02-13-16-50-48\Task Settings\Task1-RnaSearchTaskconfig.toml";
            var loadedSearchTask = Toml.ReadFile<RnaSearchTask>(searchToml, MetaMorpheusTask.tomlConfig);
            Omics.Fragmentation.Oligo.DissociationTypeCollection.ProductsFromDissociationType[DissociationType.Custom] =
                loadedSearchTask.CommonParameters.CustomIons;
            var gptmdMods = new List<(string, string)>()
            {
                ("Digestion Termini", "Terminal Dephosphorylation on X"),
                ("Digestion Termini", "Terminal Phosphorylation on X"),
                ("Common Variable", "Phosphorothioate on X"),
            };
        

            // temp
            loadedSearchTask.RunTask(outputFolder, dbForTask, new List<string>() { dataFilePath }, "tasktest");


            GptmdParameters gptmdParameters = new() { ListOfModsGptmd = gptmdMods };
            var gptmdTask = new GptmdTask()
            {
                CommonParameters = loadedSearchTask.CommonParameters,
                GptmdParameters = gptmdParameters
            };

            var taskList = new List<(string, MetaMorpheusTask)> { ("Task1-GPTMD", gptmdTask), ("Task2-RnaSearch", loadedSearchTask) };
            var runner = new EverythingRunnerEngine(taskList, new List<string> { dataFilePath }, dbForTask, outputFolder);
            runner.Run();
        }






        [Test]
        public static void SequenceCoverage()
        {
            string resultPath = @"D:\DataFiles\RnaTestSets\PfizerData\2024-02-13-16-53-11\Task1-RnaSearchTask\AllOSMs.osmtsv";
               // @"D:\Projects\RNA\TestData\Pfizer\ThreeMissed_5ppmProduct_Exact\Task1-PfizerSearch\AllOSMs.osmtsv";


            string fastaPath = @"D:\DataFiles\RnaTestSets\PfizerData\PfizerBNT-162b2.fasta";
            var sequence = File.ReadAllLines(fastaPath).Skip(1).Aggregate((a, b) => a + b);
            var osms = SpectrumMatchTsvReader.ReadOsmTsv(resultPath, out List<string> warnings)
                .Where(p => p.DecoyContamTarget == "T").ToList();
            var fragmentMaps = FragmentMap.CreateFragments(osms).ToList();

            // Map fragments to the sequence
            List<bool> sequenceCoverage = FragmentMap.MapFragmentsToSequence(sequence, fragmentMaps);

            // Calculate coverage statistics
            int coveredResidues = sequenceCoverage.Count(b => b);
            double coveragePercentage = (double)coveredResidues / sequence.Length * 100;

            Console.WriteLine($"Sequence Length: {sequence.Length}");
            Console.WriteLine($"Covered Residues: {coveredResidues}");
            Console.WriteLine($"Coverage Percentage: {coveragePercentage}%");

            fragmentMaps = FragmentMap.CreateFragments(osms.Where(p => p.PEP_QValue <= 0.05).ToList()).ToList();
            // Map fragments to the sequence
            sequenceCoverage = FragmentMap.MapFragmentsToSequence(sequence, fragmentMaps);

            // Calculate coverage statistics
            coveredResidues = sequenceCoverage.Count(b => b);
            coveragePercentage = (double)coveredResidues / sequence.Length * 100;

            Console.WriteLine($"5% Sequence Length: {sequence.Length}");
            Console.WriteLine($"5% Covered Residues: {coveredResidues}");
            Console.WriteLine($"5% Coverage Percentage: {coveragePercentage}%");


            fragmentMaps = FragmentMap.CreateFragments(osms.Where(p => p.PEP_QValue <= 0.01).ToList()).ToList();
            // Map fragments to the sequence
            sequenceCoverage = FragmentMap.MapFragmentsToSequence(sequence, fragmentMaps);

            // Calculate coverage statistics
            coveredResidues = sequenceCoverage.Count(b => b);
            coveragePercentage = (double)coveredResidues / sequence.Length * 100;

            Console.WriteLine($"1% Sequence Length: {sequence.Length}");
            Console.WriteLine($"1% Covered Residues: {coveredResidues}");
            Console.WriteLine($"1% Coverage Percentage: {coveragePercentage}%");

        }


        [Test]
        public static void SequenceCoverageOutputWithCount()
        {
            string resultPath =
                @"D:\Projects\RNA\TestData\Pfizer\PfizerIonsPlusLoss_NewDigestion\Task1-PfizerSearch\AllOSMs.osmtsv";


            string fastaPath = @"D:\DataFiles\RnaTestSets\PfizerData\PfizerBNT-162b2.fasta";
            var sequence = File.ReadAllLines(fastaPath).Skip(1).Aggregate((a, b) => a + b);
            var osms = SpectrumMatchTsvReader.ReadOsmTsv(resultPath, out List<string> warnings)
                .Where(p => p.DecoyContamTarget == "T").ToList();
            var fragmentMaps = FragmentMap.CreateFragments(osms).ToList();
            var fragmentMaps1 = FragmentMap.CreateFragments(osms.Where(p => p.QValue <= 0.01).ToList()).ToList();
            var fragmentMaps5 = FragmentMap.CreateFragments(osms.Where(p => p.QValue <= 0.05).ToList()).ToList();
            var sequenceCoverage = FragmentMap.MapFragmentsToSequenceWithCount(sequence, fragmentMaps);
            var sequenceCoverage1 = FragmentMap.MapFragmentsToSequenceWithCount(sequence, fragmentMaps1);
            var sequenceCoverage5 = FragmentMap.MapFragmentsToSequenceWithCount(sequence, fragmentMaps5);
            string outPath = @"D:\Projects\RNA\TestData\Pfizer\sequencecoverage_newDigestion.csv";
            using var sw = new StreamWriter(outPath);
            sw.WriteLine("residue,found?,count,1%found,1%count,5%found,5%count");
            for (int i = 0; i < sequence.Length; i++)
            {
                sw.WriteLine($"{sequence[i]},{sequenceCoverage[i].Item1},{sequenceCoverage[i].Item2},{sequenceCoverage1[i].Item1},{sequenceCoverage1[i].Item2},{sequenceCoverage5[i].Item1},{sequenceCoverage5[i].Item2}");
            }
        }

        [Test]
        public static void runfagmentanalysisengine()
        {
            string dirPath = @"D:\Projects\RNA\TestData\Pfizer";
           // var files = Directory.GetFiles(dirPath, "*.osmtsv", SearchOption.AllDirectories).ToList();
           var directories = new List<string>()
           {
               "AllIons", "PfizerIons", "ThreeMissed_Min4_NoVariable", "ThreeMissed_5ppmProduct", "ThreeMissed_5ppmProduct_Exact", 
               "ThreeMissed_5ppmProduct_OneMissedMono", "ThreeMissed_5ppmProduct_PlusMinus3", 
           };
           var files = directories.Select(p => Path.Combine(dirPath, p, "Task1-PfizerSearch", "AllOSMs.osmtsv"))
               .ToList();


            //var outpath = Path.Combine(dirPath, "FragmentAnalysis.csv");
            var outpath = Path.Combine(dirPath, "FragmentAnalysisGreater.csv");
            var engine = new FragmentAnalysisEngine(files, outpath);
            engine.Run();

        }


        [Test]
        public static void TESTNAME()
        {
            string path = @"D:\Projects\SpectralAveraging\PaperTestOutputs\ChimeraImages\InDocument";
            var outpath = @"D:\Projects\SpectralAveraging\PaperTestOutputs\ChimeraImages\InDocument\collection.csv";


            using var writer = new StreamWriter(outpath);
            writer.WriteLine("Fraction,PrecursorScanNumber,FragmentationScanNumber");
            foreach (var file in Directory.GetFiles(path, "*.png"))
            {
                var splits = Path.GetFileNameWithoutExtension(file).Split('_');
                var fraction = string.Join('_', new[] { splits[1], splits[2], splits[3], splits[4] });
                var precursor = splits[5];
                var fragmentation = splits[6];
                writer.WriteLine($"{fraction},{precursor},{fragmentation}");
            }
        }
    }
}

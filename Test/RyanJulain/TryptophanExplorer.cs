using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using GuiFunctions.MetaDraw.SpectrumMatch;
using MzLibUtil;
using NUnit.Framework;
using Org.BouncyCastle.Asn1.X509;
using Proteomics;
using Proteomics.ProteolyticDigestion;
using UsefulProteomicsDatabases;

namespace Test.RyanJulain
{
    public class TryptophanExplorer
    {
        
        internal static string DirectoryPath = @"B:\Users\Nic\RyanJulian\Under60kDa";
        private static bool doMods = false;
        internal string FragmentsNeededPath;
        internal string FragmentCountPath;
        internal string FragmentIndexPath;

        internal string Species { get; set; }
        internal int NumberOfMods { get; set; }
        private string DatabasePath { get; set; }
        public TryptophanExplorer(string databasePath, int numberOfMods, string species)
        {
            DatabasePath = databasePath;
            NumberOfMods = numberOfMods;
            Species = species;
            FragmentIndexPath = Path.Combine(DirectoryPath, $"{species}_{FileIdentifiers.TryptophanFragmentIndex}_{NumberOfMods}Mods.csv");
            FragmentCountPath = Path.Combine(DirectoryPath, $"{species}_{FileIdentifiers.TryptophanFragmentCountHistogram}_{NumberOfMods}Mods.csv");
            FragmentsNeededPath = Path.Combine(DirectoryPath, $"{species}_{FileIdentifiers.MinFragmentNeededHistogram}_{NumberOfMods}Mods.csv");
        }

        private PrecursorFragmentMassFile precursorFragmentMassFile;
        public PrecursorFragmentMassFile PrecursorFragmentMassFile
        {
            get
            {
                if (precursorFragmentMassFile != null) return precursorFragmentMassFile;

                string fileName = $"{Species}_{FileIdentifiers.TryptophanFragmentIndex}_{NumberOfMods}Mods.csv";
                var filePath = Path.Combine(DirectoryPath, fileName);

                precursorFragmentMassFile = File.Exists(filePath) 
                    ? new PrecursorFragmentMassFile() { FilePath = filePath }
                    : CreateTryptophanFragmentIndex();
                return precursorFragmentMassFile;
            }
        }

        public PrecursorFragmentMassFile CreateTryptophanFragmentIndex()
        {
            if (File.Exists(FragmentIndexPath))
                return PrecursorFragmentMassFile;


            var modifications = NumberOfMods == 0 ? new List<Modification>() : GlobalVariables.AllModsKnown;
            var proteins = ProteinDbLoader.LoadProteinXML(DatabasePath, true, DecoyType.None, modifications, false, new List<string>(), out var um);
            var digestionParameters = new DigestionParams("tryptophan oxidation", 0, 7, Int32.MaxValue, 100000,
                InitiatorMethionineBehavior.Retain, NumberOfMods);
            var precursorDigestionParameters = new DigestionParams("top-down", 0, 2, Int32.MaxValue, 100000,
                InitiatorMethionineBehavior.Retain, NumberOfMods);

            var sets = new List<PrecursorFragmentMassSet>();
            var fixedMods = new List<Modification>();
            var variableMods = new List<Modification>();

            var proteolysisProducts = new List<ProteolysisProduct>();
            var disulfideBonds = new List<DisulfideBond>();
            foreach (var protein in proteins)
            {
                var topDownDigestionProducts = protein.Digest(precursorDigestionParameters, fixedMods, variableMods)
                    .DistinctBy(p => p.FullSequence).ToList();
                foreach (var proteoform in topDownDigestionProducts)
                {
                    if (DirectoryPath.Contains("Under60kDa") && proteoform.MonoisotopicMass >= 60000)
                        continue;
                    

                    var mods = proteoform.AllModsOneIsNterminus
                        //.Where(p => p.Key >= proteoform.OneBasedStartResidueInProtein && p.Key <= proteoform.OneBasedEndResidueInProtein)
                        .ToDictionary(p => p.Key, p => new List<Modification>() { p.Value });
                    

                    var proteinReconstruction = new Protein(proteoform.BaseSequence, proteoform.Protein.Accession,
                        proteoform.Protein.Organism, proteoform.Protein.GeneNames.ToList(),
                        mods, proteolysisProducts, proteoform.Protein.Name, proteoform.Protein.FullName,
                        proteoform.Protein.IsDecoy, proteoform.Protein.IsContaminant,
                        proteoform.Protein.DatabaseReferences.ToList(), proteoform.Protein.SequenceVariations.ToList(),
                        proteoform.Protein.AppliedSequenceVariations, proteoform.Protein.SampleNameForVariants,
                        disulfideBonds, proteoform.Protein.SpliceSites.ToList(),
                        proteoform.Protein.DatabaseFilePath, false
                    );



                    var peps = proteinReconstruction.Digest(digestionParameters, fixedMods, variableMods);
                    var fragments = peps.Select(p => p.MonoisotopicMass).ToList();
                    fragments.Add(proteoform.MonoisotopicMass);

                    sets.Add(new PrecursorFragmentMassSet(proteoform.MonoisotopicMass, proteoform.Protein.Accession, fragments, proteoform.FullSequence));
                }
            }

            // sanitize
            var uniqueSets = sets.DistinctBy(p => p,
                AveragingPaper.CustomComparer<PrecursorFragmentMassSet>.PrecursorFragmentMassComparer).ToList();
            var file = new PrecursorFragmentMassFile()
            {
                FilePath = FragmentIndexPath,
                Results = uniqueSets
            };
            file.WriteResults(FragmentIndexPath);
            return file;
        }

        public void CreateFragmentHistogramFile()
        {
            if (File.Exists(FragmentCountPath))
                return;

            var fragmentCounts = PrecursorFragmentMassFile.Results.GroupBy(p => p.FragmentCount)
                .OrderBy(p => p.Key)
                .ToDictionary(p => p.Key, p => p.Count());
            using (var sw = new StreamWriter(FragmentCountPath))
            {
                sw.WriteLine("Tryptophan Fragments,Count of Proteins");
                foreach (var kvp in fragmentCounts)
                {
                    sw.WriteLine($"{kvp.Key},{kvp.Value}");
                }
            }
        }

        public void FindNumberOfFragmentsNeededToDifferentiate()
        {
            if (File.Exists(FragmentsNeededPath))
                return;

            var tolerance = new PpmTolerance(10);
            using var sw = new StreamWriter(FragmentsNeededPath);
            sw.WriteLine("Accession,NumberInPrecursorGroup,FragmentsAvailable,FragmentsNeeded");
            foreach (var proteoform in PrecursorFragmentMassFile.Results)
            {
                var withinTolerance = PrecursorFragmentMassFile.Results
                    .Where(p => !p.Equals(proteoform) && tolerance.Within(proteoform.PrecursorMass, p.PrecursorMass))
                    .ToList();

                var minFragments = withinTolerance.Count > 1 ? MinFragmentMassesToDifferentiate(proteoform.FragmentMassesHashSet, withinTolerance, tolerance) : 0;
                sw.WriteLine($"{proteoform.Accession},{withinTolerance.Count},{proteoform.FragmentMasses.Count},{minFragments}");
            }
        }

        static int MinFragmentMassesToDifferentiate(HashSet<double> targetProteoform, List<PrecursorFragmentMassSet> otherProteoforms, Tolerance tolerance)
        {
            // check to see if target proteoform has a fragment that is unique to all other proteoform fragments within tolerance
            if (HasUniqueFragment(targetProteoform, otherProteoforms, tolerance))
                return 1;

            if (otherProteoforms.All(p => p.FragmentMassesHashSet.SequenceEqual(targetProteoform)))
                return -1;

            // Generate all combinations of fragment masses from the target otherProteoform
            // Order by count of fragment masses and check to see if they can differentiate the target
            foreach (var combination in GenerateCombinations(targetProteoform.ToList())
                         .Where(p => p.Count > 1)
                         .OrderBy(p => p.Count))
            {
                // get those that can be explained by these fragments
                var idkMan = otherProteoforms.Where(p => p.FragmentMassesHashSet.ListContainsWithin(combination, tolerance)).ToList();
                if (idkMan.Count == 0)
                    return combination.Count;
                else if (HasUniqueFragment(targetProteoform, idkMan, tolerance))
                    return combination.Count + 1;
            }

            return -1;
        }

        static bool HasUniqueFragment(HashSet<double> targetProteoform, List<PrecursorFragmentMassSet> otherProteoforms,
            Tolerance tolerance)
        {
            // check to see if target proteoform has a fragment that is unique to all other proteoform fragments within tolerance
            foreach (var targetFragment in targetProteoform)
            {
                bool isUniqueFragment = true;
                foreach (var otherProteoform in otherProteoforms)
                {
                    foreach (var otherFragment in otherProteoform.FragmentMassesHashSet)
                    {
                        if (tolerance.Within(targetFragment, otherFragment))
                        {
                            isUniqueFragment = false;
                            break;
                        }
                    }
                    if (!isUniqueFragment)
                        break;
                }
                if (isUniqueFragment)
                    return true;
            }

            return false;
        }

        // Function to generate all combinations of fragment masses from a given list
        static List<List<double>> GenerateCombinations(List<double> fragmentMasses)
        {
            List<List<double>> combinations = new List<List<double>>();
            int n = fragmentMasses.Count;
            for (int i = 0; i < (1 << n); i++)
            {
                List<double> combination = new List<double>();
                for (int j = 0; j < n; j++)
                {
                    if ((i & (1 << j)) > 0)
                    {
                        combination.Add(fragmentMasses[j]);
                    }
                }
                combinations.Add(combination);
            }
            return combinations;
        }
    }

    public static class extensions
    {
        public static bool ContainsWithin(this IEnumerable<double> list, double value, Tolerance tolerance)
        {
            return list.Any(p => tolerance.Within(p, value));
        }

        public static bool ListContainsWithin(this IEnumerable<double> list, List<double> values, Tolerance tolerance)
        {
            return values.All(p => list.ContainsWithin(p, tolerance));
        }
    }

    public static class TyrptophanResultOperations
    {
        public static void CombineAllFragmentCountHistograms(this List<TryptophanExplorer> tryptophanExplorers)
        {
            using var sw = new StreamWriter(Path.Combine(TryptophanExplorer.DirectoryPath, $"Combined_{FileIdentifiers.MinFragmentNeededHistogram}.csv"));
            sw.WriteLine("Organism,Mods,Accession,NumberInGroup,FragmentsAvailable,FragmentsNeeded");
            foreach (var trypExplorer in tryptophanExplorers)
            {
                string organism = trypExplorer.Species;
                int mods = trypExplorer.NumberOfMods;
                if (File.Exists(trypExplorer.FragmentsNeededPath))
                {
                    using var sr = new StreamReader(trypExplorer.FragmentsNeededPath);
                    sr.ReadLine();
                    while (!sr.EndOfStream)
                    {
                        sw.WriteLine($"{organism},{mods},{sr.ReadLine()}");
                    }
                }
            }
        }

        public static void CombineFragmentCountHistogram(this List<TryptophanExplorer> tryptophanExplorers)
        {
            using var sw = new StreamWriter(Path.Combine(TryptophanExplorer.DirectoryPath, $"Combined_{FileIdentifiers.TryptophanFragmentCountHistogram}.csv"));
            sw.WriteLine("Organism,Mods,Fragments,Count");
            foreach (var trypExplorer in tryptophanExplorers)
            {
                string organism = trypExplorer.Species;
                int mods = trypExplorer.NumberOfMods;
                if (File.Exists(trypExplorer.FragmentCountPath))
                {
                    using var sr = new StreamReader(trypExplorer.FragmentCountPath);
                    sr.ReadLine();
                    while (!sr.EndOfStream)
                    {
                        sw.WriteLine($"{organism},{mods},{sr.ReadLine()}");
                    }
                }
            }
        }
    }
}

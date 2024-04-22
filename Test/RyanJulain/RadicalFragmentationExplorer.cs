using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using MzLibUtil;
using Proteomics;
using Proteomics.ProteolyticDigestion;
using Test.AveragingPaper;
using Test.RyanJulain;
using UsefulProteomicsDatabases;

namespace Test.RyanJulian
{
    public abstract class RadicalFragmentationExplorer
    {
        public bool Override { get; set; } = false;
        protected string BaseDirectorPath = @"B:\Users\Nic\RyanJulian";
        protected string DirectoryPath => Path.Combine(BaseDirectorPath, AnalysisType);
        protected string IndexDirectoryPath => Path.Combine(BaseDirectorPath, "IndexedFragments", AnalysisType);
        protected int AmbiguityLevel { get; set; }
        protected string Species { get; set; }
        protected int NumberOfMods { get; set; }
        protected string DatabasePath { get; set; }
        protected abstract string AnalysisType { get; }
        protected int MaximumFragmentationEvents { get; set; }
        protected string MaxFragmentString => MaximumFragmentationEvents == int.MaxValue ? "All" : MaximumFragmentationEvents.ToString();

        protected RadicalFragmentationExplorer(string databasePath, int numberOfMods, string species, int maximumFragmentationEvents = int.MaxValue,
            int ambiguityLevel = 1)
        {
            DatabasePath = databasePath;
            NumberOfMods = numberOfMods;
            Species = species;
            MaximumFragmentationEvents = maximumFragmentationEvents;
            AmbiguityLevel = ambiguityLevel;
        }

        #region Result Files

        protected string _precursorFragmentMassFilePath => Path.Combine(IndexDirectoryPath, 
            $"{Species}_{NumberOfMods}Mods_{MaxFragmentString}_Level({AmbiguityLevel})Ambiguity_{FileIdentifiers.FragmentIndex}" );
        protected PrecursorFragmentMassFile _precursorFragmentMassFile;
        public PrecursorFragmentMassFile PrecursorFragmentMassFile
        {
            get
            {
                if (_precursorFragmentMassFile != null) return _precursorFragmentMassFile;
                if (File.Exists(_precursorFragmentMassFilePath))
                {
                    _precursorFragmentMassFile = new PrecursorFragmentMassFile(_precursorFragmentMassFilePath);
                }
                else
                {
                    _precursorFragmentMassFile = CreateIndexedFile();
                }

                return _precursorFragmentMassFile;
            }
        }

        protected string _fragmentHistogramFilePath => Path.Combine(DirectoryPath, 
            $"{Species}_{NumberOfMods}Mods_{MaxFragmentString}_Level({AmbiguityLevel})Ambiguity_{FileIdentifiers.FragmentCountHistogram}");
        protected FragmentHistogramFile _fragmentHistogramFile;
        public FragmentHistogramFile FragmentHistogramFile
        {
            get
            {
                if (_fragmentHistogramFile != null) return _fragmentHistogramFile;
                if (File.Exists(_fragmentHistogramFilePath))
                {
                    _fragmentHistogramFile = new FragmentHistogramFile(_fragmentHistogramFilePath);
                }
                else
                {
                    CreateFragmentHistogramFile();
                    _fragmentHistogramFile = new FragmentHistogramFile(_fragmentHistogramFilePath);
                }

                return _fragmentHistogramFile;
            }
        }

        protected string _minFragmentNeededFilePath => Path.Combine(DirectoryPath, 
            $"{Species}_{NumberOfMods}Mods_{MaxFragmentString}_Level({AmbiguityLevel})Ambiguity_{FileIdentifiers.MinFragmentNeeded}");
        protected FragmentsToDistinguishFile _minFragmentNeededFile;
        public FragmentsToDistinguishFile MinFragmentNeededFile
        {
            get
            {
                if (_minFragmentNeededFile != null) return _minFragmentNeededFile;
                if (File.Exists(_minFragmentNeededFilePath))
                {
                    _minFragmentNeededFile = new FragmentsToDistinguishFile(_minFragmentNeededFilePath);
                }
                else
                {
                    FindNumberOfFragmentsNeededToDifferentiate();
                    _minFragmentNeededFile = new FragmentsToDistinguishFile(_minFragmentNeededFilePath);
                }

                return _minFragmentNeededFile;
            }
        }

        #endregion

        #region Methods
        protected DigestionParams PrecursorDigestionParams => new DigestionParams("top-down", 0, 2, Int32.MaxValue, 100000,
            InitiatorMethionineBehavior.Retain, NumberOfMods);

        public PrecursorFragmentMassFile CreateIndexedFile()
        {
            if (!Override && File.Exists(_precursorFragmentMassFilePath))
                return PrecursorFragmentMassFile;

            CustomComparer<PrecursorFragmentMassSet> comparer = AmbiguityLevel switch
            {
                1 => CustomComparer<PrecursorFragmentMassSet>.LevelOneComparer,
                2 => CustomComparer<PrecursorFragmentMassSet>.LevelTwoComparer,
                _ => throw new Exception("Ambiguity level not supported")
            };
            var modifications = NumberOfMods == 0 ? new List<Modification>() : GlobalVariables.AllModsKnown;
            var proteins = ProteinDbLoader.LoadProteinXML(DatabasePath, true, DecoyType.None, modifications, false, new List<string>(), out var um);
            
            var sets = new List<PrecursorFragmentMassSet>();
            foreach (var protein in proteins)
            {
                sets.AddRange(GeneratePrecursorFragmentMasses(protein));
            }

            var uniqueSets = sets.DistinctBy(p => p, comparer).ToList();
            var file = new PrecursorFragmentMassFile()
            {
                FilePath = _precursorFragmentMassFilePath,
                Results = uniqueSets
            };
            file.WriteResults(_precursorFragmentMassFilePath);
            return _precursorFragmentMassFile = file;
        }
        public abstract IEnumerable<PrecursorFragmentMassSet> GeneratePrecursorFragmentMasses(Protein protein);

        public FragmentsToDistinguishFile FindNumberOfFragmentsNeededToDifferentiate()
        {
            if (!Override && File.Exists(_minFragmentNeededFilePath))
                return MinFragmentNeededFile;

            var tolerance = new PpmTolerance(10);
            var results = new List<FragmentsToDistinguishRecord>();
            foreach (var proteoform in PrecursorFragmentMassFile.Results)
            {
                var withinTolerance = PrecursorFragmentMassFile.Results
                    .Where(p => !p.Equals(proteoform) && tolerance.Within(proteoform.PrecursorMass, p.PrecursorMass))
                    .ToList();
                var minFragments = withinTolerance.Count > 1 ? MinFragmentMassesToDifferentiate(proteoform.FragmentMassesHashSet, withinTolerance, tolerance) : 0;
                results.Add(new FragmentsToDistinguishRecord
                {
                    Species = Species,
                    NumberOfMods = NumberOfMods,
                    MaxFragments = MaximumFragmentationEvents,
                    AnalysisType = AnalysisType,
                    AmbiguityLevel = AmbiguityLevel,
                    Accession = proteoform.Accession,
                    NumberInPrecursorGroup = withinTolerance.Count,
                    FragmentsAvailable = proteoform.FragmentMasses.Count,
                    FragmentCountNeededToDifferentiate = minFragments
                });
            }

            var fragmentsToDistinguishFile = new FragmentsToDistinguishFile(_minFragmentNeededFilePath) { Results = results };
            fragmentsToDistinguishFile.WriteResults(_minFragmentNeededFilePath);
            return _minFragmentNeededFile = fragmentsToDistinguishFile;
        }

        public FragmentHistogramFile CreateFragmentHistogramFile()
        {
            if (!Override && File.Exists(_fragmentHistogramFilePath))
                return FragmentHistogramFile;

            var fragmentCounts = PrecursorFragmentMassFile.Results.GroupBy(p => p.FragmentCount)
                .OrderBy(p => p.Key)
                .Select(p => new FragmentHistogramRecord
                {
                    Species = Species,
                    NumberOfMods = NumberOfMods,
                    MaxFragments = MaximumFragmentationEvents,
                    AnalysisType = AnalysisType,
                    AmbiguityLevel = AmbiguityLevel,
                    FragmentCount = p.Key,
                    ProteinCount = p.Count()
                }).ToList();
            var file = new FragmentHistogramFile(_fragmentHistogramFilePath) { Results = fragmentCounts };
            file.WriteResults(_fragmentHistogramFilePath);
            return _fragmentHistogramFile = file;
        }

        protected static int MinFragmentMassesToDifferentiate(HashSet<double> targetProteoform, List<PrecursorFragmentMassSet> otherProteoforms, Tolerance tolerance)
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
                if (HasUniqueFragment(targetProteoform, idkMan, tolerance))
                    return combination.Count + 1;
            }

            return -1;
        }

        protected static bool HasUniqueFragment(HashSet<double> targetProteoform, List<PrecursorFragmentMassSet> otherProteoforms,
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
        protected static List<List<double>> GenerateCombinations(List<double> fragmentMasses)
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



        #endregion

        #region Plotting //TODO: this

        public void PlotFragmentHistogram()
        {
            
        }

        public void PlotMinFragmentsNeeded()
        {

        }

        #endregion
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
}


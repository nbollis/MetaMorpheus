using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EngineLayer;
using MzLibUtil;
using Proteomics;
using Proteomics.ProteolyticDigestion;
using Test.AveragingPaper;
using UsefulProteomicsDatabases;

namespace Test.RyanJulian
{
    public abstract class RadicalFragmentationExplorer
    {
        public bool Override { get; set; } = false;
        protected string BaseDirectorPath { get; init; }
        protected string DirectoryPath => Path.Combine(BaseDirectorPath, AnalysisType);
        public string FigureDirectory => Path.Combine(BaseDirectorPath, "Figure");
        protected string IndexDirectoryPath => Path.Combine(BaseDirectorPath, "IndexedFragments", AnalysisType);
        public int AmbiguityLevel { get; set; }
        public string Species { get; set; }
        public int NumberOfMods { get; set; }
        protected string DatabasePath { get; set; }
        public abstract string AnalysisType { get; }
        protected int MaximumFragmentationEvents { get; set; }
        protected string MaxFragmentString => MaximumFragmentationEvents == int.MaxValue ? "All" : MaximumFragmentationEvents.ToString();

        protected List<Modification> fixedMods;
        protected List<Modification> variableMods;
        protected List<ProteolysisProduct> proteolysisProducts;
        protected List<DisulfideBond> disulfideBonds;

        protected RadicalFragmentationExplorer(string databasePath, int numberOfMods, string species, int maximumFragmentationEvents = int.MaxValue,
            int ambiguityLevel = 1, string? baseDirectory = null)
        {
            DatabasePath = databasePath;
            NumberOfMods = numberOfMods;
            Species = species;
            MaximumFragmentationEvents = maximumFragmentationEvents;
            AmbiguityLevel = ambiguityLevel;
            BaseDirectorPath = baseDirectory ?? @"D:\Projects\RadicalFragmentation\FragmentAnalysis";

            fixedMods = new List<Modification>();
            variableMods = new List<Modification>();
            proteolysisProducts = new List<ProteolysisProduct>();
            disulfideBonds = new List<DisulfideBond>();
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
            
            var sets = new List<PrecursorFragmentMassSet>();
            var level1Path = Path.Combine(IndexDirectoryPath,
                $"{Species}_{NumberOfMods}Mods_{MaxFragmentString}_Level(1)Ambiguity_{FileIdentifiers.FragmentIndex}");
            if (AmbiguityLevel == 2 && File.Exists(level1Path))
            {
                sets = new PrecursorFragmentMassFile(level1Path).Results.DistinctBy(p => p, comparer)
                        .ToList();
            }
            else
            {
                var modifications = NumberOfMods == 0 ? new List<Modification>() : GlobalVariables.AllModsKnown;
                var proteins = ProteinDbLoader.LoadProteinXML(DatabasePath, true, DecoyType.None, modifications, false, new List<string>(), out var um);

                foreach (var protein in proteins)
                {
                    sets.AddRange(GeneratePrecursorFragmentMasses(protein));
                }
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

            var dataSplits = 10;
            var tolerance = new PpmTolerance(10);

            string[] tempFilePaths = new string[dataSplits];
            for (int i = 0; i < dataSplits; i++)
                tempFilePaths[i] = _minFragmentNeededFilePath.Replace(".csv", $"_{i}.csv");

            // split processed data into n chuncks
            var toSplit = new List<(PrecursorFragmentMassSet, List<PrecursorFragmentMassSet>)>();
            var orderedResults = PrecursorFragmentMassFile.Results.OrderBy(p => p.PrecursorMass).ToList();
            for (var index = 0; index < orderedResults.Count; index++)
            {
                var outerResult = orderedResults[index];

                var first = orderedResults.First(p =>
                    tolerance.Within(p.PrecursorMass, outerResult.PrecursorMass));
                var firstIndex = orderedResults.IndexOf(first);

                // iterate through ordered until one does not fall within tolerance
                var innerResults = new List<PrecursorFragmentMassSet> { first };
                for (int i = firstIndex + 1; i < orderedResults.Count; i++)
                {
                    if (orderedResults[i].Equals(first))
                        continue;
                    if (tolerance.Within(orderedResults[i].PrecursorMass, first.PrecursorMass))
                        innerResults.Add(orderedResults[i]);
                    else
                        break;
                }

                toSplit.Add((outerResult, innerResults));

                if (firstIndex != 0)
                {
                    int toRemove = firstIndex;
                    orderedResults.RemoveRange(0, toRemove);
                    index -= toRemove;
                }
            }

            var toProcess = toSplit.Split(dataSplits).ToList();

            // Process a single chunk at a time
            for (int i = 0; i < dataSplits; i++)
            {
                if (File.Exists(tempFilePaths[i]))
                    continue;

                var results = new List<FragmentsToDistinguishRecord>();
                Parallel.ForEach(toProcess[i], new ParallelOptions() { MaxDegreeOfParallelism = 8 },
                    (result) =>
                    {
                        var minFragments = result.Item2.Count > 1
                            ? MinFragmentMassesToDifferentiate(result.Item1.FragmentMassesHashSet, result.Item2,
                                tolerance)
                            : 0;
                        var record = new FragmentsToDistinguishRecord
                        {
                            Species = Species,
                            NumberOfMods = NumberOfMods,
                            MaxFragments = MaximumFragmentationEvents,
                            AnalysisType = AnalysisType,
                            AmbiguityLevel = AmbiguityLevel,
                            Accession = result.Item1.Accession,
                            NumberInPrecursorGroup = result.Item2.Count,
                            FragmentsAvailable = result.Item1.FragmentMasses.Count,
                            FragmentCountNeededToDifferentiate = minFragments
                        };
                        lock (results)
                        {
                            results.Add(record);
                        }
                    });

                // write that chunk
                var tempFile = new FragmentsToDistinguishFile(tempFilePaths[i]) { Results = results };
                tempFile.WriteResults(tempFilePaths[i]);
            }

            // combine all temporary files into a single file and delete the temp files
            var allResults = new List<FragmentsToDistinguishRecord>();
            foreach (var tempFile in tempFilePaths)
                allResults.AddRange(new FragmentsToDistinguishFile(tempFile).Results);
            
            var fragmentsToDistinguishFile = new FragmentsToDistinguishFile(_minFragmentNeededFilePath) { Results = allResults };
            fragmentsToDistinguishFile.WriteResults(_minFragmentNeededFilePath);

            foreach (var tempFile in tempFilePaths)
                File.Delete(tempFile);

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
                var idkMan = otherProteoforms
                    .Where(p => p.FragmentMassesHashSet.ListContainsWithin(combination, tolerance))
                    .ToList();
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
        protected static IEnumerable<List<double>> GenerateCombinations(List<double> fragmentMasses)
        {
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

                yield return combination;
            }
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


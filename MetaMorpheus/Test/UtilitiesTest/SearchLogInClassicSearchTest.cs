using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using EngineLayer.ClassicSearch;
using EngineLayer.SpectrumMatch;
using MassSpectrometry;
using Nett;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Omics.Modifications;
using Org.BouncyCastle.Asn1.X509;
using Proteomics;
using Readers;
using TaskLayer;
using UsefulProteomicsDatabases;

namespace Test.UtilitiesTest
{
    internal class SearchLogInClassicSearchTest
    {

        private static List<Protein> proteins;
        private static Ms2ScanWithSpecificMass[] arrayOfSortedMs2Scans;
        private static CommonParameters commonParameters;


        [OneTimeSetUp]
        public static void OneTimeSetUp()
        {
            string tomlPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "SlicedSearchTaskConfig.toml");
            var task = Toml.ReadFile<SearchTask>(tomlPath, MetaMorpheusTask.tomlConfig);
            commonParameters = task.CommonParameters;

            string mzmLPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "sliced-raw.mzML");
            MsDataFile mzMl = FileReader.ReadFile<MsDataFileToResultFileAdapter>(mzmLPath).LoadAllStaticData();
            arrayOfSortedMs2Scans = MetaMorpheusTask.GetMs2Scans(mzMl, mzmLPath, commonParameters)
                .OrderBy(b => b.PrecursorMass).ToArray();

            string dbPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "sliced-db.fasta");
            proteins = ProteinDbLoader.LoadProteinFasta(dbPath, true, DecoyType.Reverse, false, out _);

        }

        [Test]
        [TestCase(SearchLogType.TopScoringOnly)]
        public static void TestSearchLogs(SearchLogType logType)
        {
            var searchMode = new SingleAbsoluteAroundZeroSearchMode(5);
            var psmsArray = new SpectralMatch[arrayOfSortedMs2Scans.Length];
            LoadModifications(out List<Modification> variableModifications, out List<Modification> fixedModifications, out List<string> localizableModificationTypes);
            var engine = new ClassicSearchEngine(psmsArray, arrayOfSortedMs2Scans, variableModifications, fixedModifications, null, null, null, proteins, searchMode, commonParameters, null, null, [], false, false, logType);
            engine.Run();

            psmsArray = psmsArray.Where(p => p is not null).ToArray();

            foreach (var spectralMatch in psmsArray)
            {
                // Extract information from search log. 
                var searchLog = spectralMatch.SearchLog;
                List<ISearchAttempt> allMatches = searchLog.GetAttempts().ToList();
                List<ISearchAttempt> bestMatches = searchLog.GetTopScoringAttempts().ToList();
                List<SpectralMatchHypothesis> bestMatchesHypothesis = searchLog.GetTopScoringAttemptsWithSequenceInformation().ToList();
                SpectralMatchHypothesis veryBestMatch = bestMatchesHypothesis.First();

                // All targets and decoys should be present in Global Matches. 
                List<ISearchAttempt> targets = searchLog.GetAttemptsByType(false).ToList();
                List<ISearchAttempt> decoys = searchLog.GetAttemptsByType(true).ToList();
                CollectionAssert.IsSupersetOf(allMatches, targets);
                CollectionAssert.IsSupersetOf(allMatches, decoys);

                // First Match should be the very best match by score
                Assert.That(veryBestMatch.Score, Is.EqualTo(searchLog.Score));
                Assert.That(bestMatchesHypothesis.Count, Is.EqualTo(searchLog.NumberOfBestScoringResults));

                // Best matches should be the same as the best matches with sequence information
                Assert.That(bestMatchesHypothesis.Count == bestMatches.Count);
                Assert.That(allMatches.Count, Is.EqualTo(searchLog.Count));

                switch (logType)
                {
                    case SearchLogType.TopScoringOnly:

                        // all matches should be the best matches
                        Assert.That(allMatches.Count, Is.EqualTo(bestMatches.Count));
                        Assert.That(allMatches.Count, Is.EqualTo(bestMatchesHypothesis.Count));
                        CollectionAssert.AreEqual(allMatches, bestMatches);
                        CollectionAssert.AreEqual(allMatches, bestMatchesHypothesis);

                        // Best Matches should contain all targets and all decoys
                        CollectionAssert.IsSupersetOf(bestMatches, targets);
                        CollectionAssert.IsSupersetOf(bestMatches, decoys);

                        foreach (var match in allMatches)
                        {
                            Assert.That(match.Score, Is.EqualTo(searchLog.Score).Within(SpectralMatch.ToleranceForScoreDifferentiation));
                            Assert.That(match, Is.TypeOf<SpectralMatchHypothesis>());
                        } 
                        break;

                    case SearchLogType.Keep7DecoyScores:
                    case SearchLogType.KeepAllDecoyScores:

                        int maxDecoys = logType == SearchLogType.Keep7DecoyScores ? 7 : int.MaxValue;

                        // Best Scoring matches should contain all targets in log and have sequence information
                        CollectionAssert.IsSupersetOf(bestMatches, targets);
                        foreach (var target in targets)
                        {
                            Assert.That(target.Score, Is.EqualTo(searchLog.Score).Within(SpectralMatch.ToleranceForScoreDifferentiation));
                        }

                        // Decoys should only exist if they are the top scoring OR if they are in the top N decoys
                        int decoyCount = 0;
                        foreach (var decoy in decoys)
                        {
                            // top scoring decoy - should have sequence information
                            if (Math.Abs(decoy.Score - searchLog.Score) < SpectralMatch.ToleranceForScoreDifferentiation)
                            {
                                Assert.That(bestMatches.Contains(decoy));
                                Assert.That(decoy, Is.TypeOf<SpectralMatchHypothesis>());
                            }
                            // lower scoring decoy - should not have sequence information
                            else
                            {
                                decoyCount++;
                                Assert.That(decoy.Score, Is.LessThanOrEqualTo(searchLog.Score));
                                Assert.That(decoy, Is.Not.TypeOf<SpectralMatchHypothesis>());
                                Assert.That(decoy, Is.TypeOf<MinimalSearchAttempt>());
                            }

                            // Decoys should not exceed the maximum number of decoys to keep
                            Assert.That(decoyCount, Is.LessThanOrEqualTo(maxDecoys));
                        }
                        break;

                    case SearchLogType.KeepAllTargetAndDecoyScores:

                        // All Targets and decoys of best score should have sequence information 
                                

                        break;

                    default:
                        Assert.Fail();
                        break;
                }
            }

        }


        protected static void LoadModifications(out List<Modification> variableModifications, out List<Modification> fixedModifications, out List<string> localizableModificationTypes)
        {
            // load modifications
            variableModifications = GlobalVariables.AllModsKnown.OfType<Modification>().Where(b => commonParameters.ListOfModsVariable.Contains((b.ModificationType, b.IdWithMotif))).ToList();
            fixedModifications = GlobalVariables.AllModsKnown.OfType<Modification>().Where(b => commonParameters.ListOfModsFixed.Contains((b.ModificationType, b.IdWithMotif))).ToList();
            localizableModificationTypes = GlobalVariables.AllModTypesKnown.ToList();

            var recognizedVariable = variableModifications.Select(p => p.IdWithMotif);
            var recognizedFixed = fixedModifications.Select(p => p.IdWithMotif);
            var unknownMods = commonParameters.ListOfModsVariable.Select(p => p.Item2).Except(recognizedVariable).ToList();
            unknownMods.AddRange(commonParameters.ListOfModsFixed.Select(p => p.Item2).Except(recognizedFixed));

        }
    }
}

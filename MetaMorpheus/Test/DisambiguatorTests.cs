using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using EngineLayer.SpectrumMatch;
using iText.Signatures;
using MassSpectrometry;
using NUnit.Framework;
using Omics;
using Omics.Fragmentation;
using Omics.Modifications;
using Proteomics;
using Proteomics.ProteolyticDigestion;

namespace Test
{
    public class TestSpectralMatch : PeptideSpectralMatch
    {
        private static MzSpectrum testSpectrum =
            new MzSpectrum(new double[] { 100, 200, 300 }, new double[] { 1, 1, 1 }, false);

        private static MsDataScan testScan = new MsDataScan(testSpectrum, 1, 1, true, Polarity.Positive, 0.1,
            new(100, 300), "", MZAnalyzerType.Orbitrap
            , 3, 50, null, "", null, null, null, null, null, null, null, null, null, null);

        private static Ms2ScanWithSpecificMass Ms2ScanWithSpecificMass =
            new Ms2ScanWithSpecificMass(testScan, 100, 2, "", new CommonParameters(), null, 1, 1, 1);

        public TestSpectralMatch(IBioPolymerWithSetMods peptide, CommonParameters commonParameters,
            List<MatchedFragmentIon> matchedFragmentIons, SearchLogType logType = SearchLogType.TopScoringOnly) 
            : base(peptide, 0, matchedFragmentIons.Count, 0, Ms2ScanWithSpecificMass, commonParameters, matchedFragmentIons, logType)
        {
        }
    }

    public static class DisambiguatorTests
    {
        private static List<MatchedFragmentIon> MatchedFragmentIons;
        private static List<MatchedFragmentIon> UniqueIons;
        private static IBioPolymerWithSetMods testPeptide;

        private static Protein targetProtein;
        [OneTimeSetUp]
        public static void Setup()
        {
            var products = new List<Product>();
            Random valueSetter = new Random();

            targetProtein = new Protein("PEPTIDEK", "accession");
            testPeptide = new PeptideWithSetModifications("PEPTIDE", new(), p: targetProtein);
            testPeptide.Fragment(DissociationType.HCD, FragmentationTerminus.Both, products);

            // Create a list of matched fragment ions
            MatchedFragmentIons = products.Select(p => new MatchedFragmentIon(p, valueSetter.NextDouble(), valueSetter.NextDouble(), 1)).ToList();

            products.Clear();
            var pep = new PeptideWithSetModifications("EDIKAWCIALYACCLCPICIAK", new(), p: targetProtein);
            pep.Fragment(DissociationType.HCD, FragmentationTerminus.Both, products);

            // create a list of unique ions to seed into results
            UniqueIons = products.Select(p => new MatchedFragmentIon(p, valueSetter.NextDouble(), valueSetter.NextDouble(), 1)).ToList();
        }

        [Test]
        public static void U()
        {
            // Create a list of spectral matches
            List<SpectralMatch> allMatches = new List<SpectralMatch>();

            // Create a common parameters object
            CommonParameters commonParams = new CommonParameters(scoreCutoff: 0);

            
            for (int uniqueIonsAdded = 0; uniqueIonsAdded < MatchedFragmentIons.Count; uniqueIonsAdded++)
            {
                var toAdd1 = MatchedFragmentIons.Take(uniqueIonsAdded).ToList();
                var toAdd2 = MatchedFragmentIons.Skip(uniqueIonsAdded).Take(uniqueIonsAdded).ToList();

                for (int uniqueIonsRequired = 0; uniqueIonsRequired < MatchedFragmentIons.Count; uniqueIonsRequired++)
                {
                    allMatches.Clear();
                    var allSharedIons = new TestSpectralMatch(testPeptide, commonParams, MatchedFragmentIons);
                    allMatches.Add(allSharedIons);

                    // create two matches with all shared ions and uniqueIonsAdded unique ions
                    var additionalIons1 = new TestSpectralMatch(testPeptide, commonParams, MatchedFragmentIons.Concat(toAdd1).ToList());
                    var additionalIons2 = new TestSpectralMatch(testPeptide, commonParams, MatchedFragmentIons.Concat(toAdd2).ToList());
                    allMatches.Add(additionalIons1);
                    allMatches.Add(additionalIons2);

                    commonParams.UniqueIonsRequired = uniqueIonsRequired;
                    if (uniqueIonsAdded >= uniqueIonsRequired) // 
                    {
                        var disambiguatedMatches = allMatches.Disambiguate(DisambiguationStrategy.UniqueIonFilter, commonParams).ToList();
                        Assert.That(disambiguatedMatches.Count, Is.EqualTo(3));
                        foreach (var match in disambiguatedMatches)
                        {
                            Assert.That(match.SearchLog.NumberOfBestScoringResults, Is.EqualTo(1));
                        }
                    }
                    else
                    {
                        var disambiguatedMatches = allMatches.Disambiguate(DisambiguationStrategy.UniqueIonFilter, commonParams).ToList();
                        Assert.That(disambiguatedMatches.Count, Is.EqualTo(2));
                        foreach (var match in disambiguatedMatches)
                        {
                            Assert.That(match.SearchLog.NumberOfBestScoringResults, Is.EqualTo(1));
                        }
                    }
                }
            }

            
        }
    }
}

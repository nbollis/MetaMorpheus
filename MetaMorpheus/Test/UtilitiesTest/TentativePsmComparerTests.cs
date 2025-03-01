using EngineLayer.Util;
using NUnit.Framework;
using Omics;
using Omics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using EngineLayer;

namespace Test.UtilitiesTest
{
    [TestFixture]
    [ExcludeFromCodeCoverage]
    public class TentativePsmComparerTests
    {
        [Test]
        public void TestCompare_ScoreDifference()
        {
            var comparer = new TentativePsmComparer();
            var psm1 = (Score: 10.0, (notch: 1, pwsm: null as IBioPolymerWithSetMods, ions: new List<MatchedFragmentIon>()));
            var psm2 = (Score: 5.0, (notch: 1, pwsm: null as IBioPolymerWithSetMods, ions: new List<MatchedFragmentIon>()));

            int result = comparer.Compare(psm1, psm2);

            Assert.That(result, Is.EqualTo(1));
        }

        [Test]
        public void TestCompare_SameScore_DifferentNotch()
        {
            var comparer = new TentativePsmComparer();
            var psm1 = (Score: 10.0, (notch: 1, pwsm: null as IBioPolymerWithSetMods, ions: new List<MatchedFragmentIon>()));
            var psm2 = (Score: 10.0, (notch: 2, pwsm: null as IBioPolymerWithSetMods, ions: new List<MatchedFragmentIon>()));

            int result = comparer.Compare(psm1, psm2);

            Assert.That(result, Is.EqualTo(1));
        }

        [Test]
        public void TestCompare_SameScoreAndNotch_DifferentIonCount()
        {
            var comparer = new TentativePsmComparer();
            var psm1 = (Score: 10.0, (notch: 1, pwsm: null as IBioPolymerWithSetMods, ions: new List<MatchedFragmentIon>()));
            var psm2 = (Score: 10.0, (notch: 1, pwsm: null as IBioPolymerWithSetMods, ions: new List<MatchedFragmentIon>() { default(MatchedFragmentIon)}));

            int result = comparer.Compare(psm1, psm2);

            Assert.That(result, Is.EqualTo(-1));
        }

        [Test]
        public void TestCompare_SameScoreAndNotch_DifferentMod()
        {
            var comparer = new TentativePsmComparer();
            var pwsm1 = new PeptideWithSetModifications("PEPTIDE", GlobalVariables.AllModsKnownDictionary);
            var pwsm2 = new PeptideWithSetModifications("PE[UniProt:4-carboxyglutamate on E]PTIDE", GlobalVariables.AllModsKnownDictionary);
            var psm1 = (Score: 10.0, (notch: 1, pwsm: pwsm1, ions: new List<MatchedFragmentIon>()));
            var psm2 = (Score: 10.0, (notch: 1, pwsm: pwsm2, ions: new List<MatchedFragmentIon>() { default(MatchedFragmentIon) }));

            int result = comparer.Compare(psm1, psm2);

            Assert.That(result, Is.EqualTo(-1));
        }
    }
}

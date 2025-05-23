﻿using Chemistry;
using EngineLayer;
using EngineLayer.Localization;
using MassSpectrometry;
using MzLibUtil;
using NUnit.Framework;
using Proteomics;
using Omics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using System;
using System.Collections.Generic;
using System.Linq;
using Omics.Modifications;
using Omics;

namespace Test
{
    [TestFixture]
    public static class EventArgsTest
    {

        [Test]
        public static void SingleEventArgsTest()
        {
            
            Protein parentProteinForMatch = new Protein("MEK", null);
            CommonParameters commonParameters = new CommonParameters(digestionParams: new DigestionParams(minPeptideLength: 1));
            var fsp = new List<(string fileName, CommonParameters fileSpecificParameters)>();
            fsp.Add(("", commonParameters));
            ModificationMotif.TryGetMotif("E", out ModificationMotif motif);
            List<Modification> variableModifications = new List<Modification> { new Modification(_originalId: "21", _target: motif, _locationRestriction: "Anywhere.", _monoisotopicMass: 21.981943) };

            var allPeptidesWithSetModifications = parentProteinForMatch.Digest(commonParameters.DigestionParams, new List<Modification>(), variableModifications).ToList();
            Assert.That(allPeptidesWithSetModifications.Count(), Is.EqualTo(4));
            var ps = allPeptidesWithSetModifications.First();

            var pepWithSetModsForSpectrum = allPeptidesWithSetModifications[1];
            MsDataFile myMsDataFile = new TestDataFile(new List<IBioPolymerWithSetMods> { pepWithSetModsForSpectrum });
            Tolerance fragmentTolerance = new AbsoluteTolerance(0.01);

            Ms2ScanWithSpecificMass scan = new Ms2ScanWithSpecificMass(myMsDataFile.GetAllScansList().Last(), pepWithSetModsForSpectrum.MonoisotopicMass.ToMz(1), 1, null, new CommonParameters());

            var theoreticalProducts = new List<Product>();
            ps.Fragment(DissociationType.HCD, FragmentationTerminus.Both, theoreticalProducts);
            
            var matchedIons = MetaMorpheusEngine.MatchFragmentIons(scan, theoreticalProducts, new CommonParameters());
            SpectralMatch newPsm = new PeptideSpectralMatch(ps, 0, 0, 2, scan, commonParameters, matchedIons);

            LocalizationEngine f = new LocalizationEngine(new List<SpectralMatch> { newPsm }, myMsDataFile, new CommonParameters(), fsp, new List<string>());

            var singleEngine= new SingleEngineEventArgs(f);
            Assert.That(singleEngine.MyEngine.Equals(f));

            var singleFile = new SingleFileEventArgs("", new List<string>());
            Assert.That(singleFile.WrittenFile.Equals(""));

            var stringList = new StringListEventArgs(new List<string>());
            var rr = (stringList.StringList.DefaultIfEmpty().First());
            Assert.That(stringList.StringList.DefaultIfEmpty().First() == null);
            
        }
    }
}

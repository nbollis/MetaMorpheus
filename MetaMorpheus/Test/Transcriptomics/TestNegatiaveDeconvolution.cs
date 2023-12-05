﻿using MassSpectrometry;
using MzLibUtil;
using NUnit.Framework;
using Readers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using EngineLayer;

namespace Test.Transcriptomics
{
    [ExcludeFromCodeCoverage]
    internal class TestNegatiaveDeconvolution
    {
        [Test]
        [TestCase(373.85, -5, 1874.28)] // GUAGUC -5
        [TestCase(467.57, -4, 1874.28)] // GUAGUC -4
        [TestCase(623.75, -3, 1874.28)] // GUAGUC -3
        [TestCase(936.13, -2, 1874.28)] // GUAGUC -2
        [TestCase(473.05, -4, 1896.26)] // GUAGUC +Na -H -4
        [TestCase(631.07, -3, 1896.26)] // GUAGUC +Na -H -3
        [TestCase(947.121, -2, 1896.26)] // GUAGUC +Na -H -2
       // [TestCase(356.9749, -1, 357.9829)] // random peak
        public void TestNegativeModeClassicDeconvolution(double expectedMz, int expectedCharge, double expectedMonoMass)
        {
            // get scan
            string filePath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"Transcriptomics\TestData",
                "GUACUG_NegativeMode_Sliced.mzML");
            var scan = MsDataFileReader.GetDataFile(filePath).GetAllScansList().First();
            var tolerance = new PpmTolerance(20);

            // set up deconvolution
            DeconvolutionParameters deconParams = new ClassicDeconvolutionParameters(-10, -1, 20, 3, Polarity.Negative);
            Deconvoluter deconvoluter = new(DeconvolutionType.ClassicDeconvolution, deconParams);



            // ensure each expected result is found, with correct mz, charge, and monoisotopic mass
            // deconvoluter
            List<IsotopicEnvelope> deconvolutionResults = deconvoluter.Deconvolute(scan).ToList();
            var resultsWithPeakOfInterest = deconvolutionResults.FirstOrDefault(envelope =>
                envelope.Peaks.Any(peak => tolerance.Within(peak.mz, expectedMz)));
            if (resultsWithPeakOfInterest is null) Assert.Fail();
            Assert.That(tolerance.Within(expectedMonoMass, resultsWithPeakOfInterest.MonoisotopicMass));
            Assert.That(expectedCharge, Is.EqualTo(resultsWithPeakOfInterest.Charge));

            // get neutral fragments
            var deconvolutionResults2 = Ms2ScanWithSpecificMass.GetNeutralExperimentalFragments(scan,
                commonParam: new CommonParameters(deconvoluter: deconvoluter)).ToList();
            var temp = deconvolutionResults2.Select(p => p.Charge).Distinct();


            resultsWithPeakOfInterest = deconvolutionResults2.FirstOrDefault(envelope =>
                envelope.Peaks.Any(peak => tolerance.Within(peak.mz, expectedMz)));
            if (resultsWithPeakOfInterest is null) Assert.Fail();
            Assert.That(tolerance.Within(expectedMonoMass, resultsWithPeakOfInterest.MonoisotopicMass));
            Assert.That(expectedCharge, Is.EqualTo(resultsWithPeakOfInterest.Charge));


        }
    }
}

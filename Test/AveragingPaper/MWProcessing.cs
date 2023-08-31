using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using NUnit.Framework;
using Proteomics;
using Proteomics.ProteolyticDigestion;
using Readers;
using UsefulProteomicsDatabases;

namespace Test
{
    [TestFixture]
    internal class MwProcessing
    {
        public static string CalibCentroidDirectory1 =
            @"D:\Projects\SpectralAveraging\PaperTestOutputs\JurkatTopDown_DeconvolutionAnalysis\Rep1CalibCentroid";
        public static string CalibAverageCentroidDirectory1 =
            @"D:\Projects\SpectralAveraging\PaperTestOutputs\JurkatTopDown_DeconvolutionAnalysis\Rep1CalibAverageCentroid";
        public static string CalibCentroidDirectory2 =
            @"D:\Projects\SpectralAveraging\PaperTestOutputs\JurkatTopDown_DeconvolutionAnalysis\Rep2CalibCentroided";
        public static string CalibAverageCentroidDirectory2 =
            @"D:\Projects\SpectralAveraging\PaperTestOutputs\JurkatTopDown_DeconvolutionAnalysis\Rep2CalibAverageCentroid";

        private Dictionary<string, Modification> _unknownModifications;


        private static string[] GetFeatureFiles(string directoryPath)
        {
            return Directory.GetFiles(directoryPath).Where(p => p.EndsWith("ms1.feature")).OrderBy(p => p).ToArray();
        }


        [Test]
        public void FeatureMassHistogram()
        {
            var directories = new List<string> { CalibCentroidDirectory2, CalibAverageCentroidDirectory2 };

            foreach (var directory in directories)
            {
                var topFDFeatureFiles = GetFeatureFiles(Path.Combine(directory, "TopFD"));
                var flashFeatureFiles = GetFeatureFiles(Path.Combine(directory, "FlashDeconv"));


                // topFD
                List<double> topFdMasses = new List<double>();
                foreach (var featureFile in topFDFeatureFiles)
                {
                    var file = FileReader.ReadFile<Readers.Ms1FeatureFile>(featureFile);
                    topFdMasses.AddRange(file.Results.Select(p => Math.Log10(p.Mass)));
                }
                var topFDHist = new Histogram(topFdMasses, 0.01);
                topFDHist.ExportHistogram(Path.Combine(directory, "TopFDLogHist.csv"), true);

                // FlashDeconv

                List<double> flashMasses = new List<double>();
                foreach (var featureFile in flashFeatureFiles)
                {
                    var file = FileReader.ReadFile<Readers.Ms1FeatureFile>(featureFile);
                    flashMasses.AddRange(file.Results.Select(p => Math.Log10(p.Mass)));
                }
                var flashHist = new Histogram(flashMasses, 0.01);
                flashHist.ExportHistogram(Path.Combine(directory, "FlashLogHist.csv"), true);
            }
        }

        [Test]
        public void PsmMassHistogram()
        {


            var directories = new List<string> { /*CalibCentroidDirectory2,*/ CalibAverageCentroidDirectory2 };
            List<double> masses = new List<double>();
            foreach (var directory in directories)
            {
                //var path = Path.Combine(directory, "MMSearch", "Task2-SearchTask", "AllPSMs.psmtsv");
                var path =
                    @"B:\Users\Katie\Jurkat proteoforms paper\Jurkat28FXNs\2017-07-05-12-56-46\Task3Search\allPSMs_5ppmAroundZero.psmtsv";
                var psms = PsmTsvReader.ReadTsv(path, out List<string> warnings).Where(p => p.QValue <= 0.01);

                masses = psms.Select(p => Math.Log10(p.PrecursorMass)).ToList();
                var hist = new Histogram(masses, 0.01);
                hist.ExportHistogram(Path.Combine(directory, "BUPsmLogHist.csv"), true);
            }
        }

        [Test]
        public void ProteinsMassHistogram()
        {


            var directories = new List<string> { /*CalibCentroidDirectory2,*/ CalibAverageCentroidDirectory2 };
            foreach (var directory in directories)
            {
                //var path = Path.Combine(directory, "MMSearch", "Task2-SearchTask", "AllPSMs.psmtsv");
                var path =
                    @"D:\Projects\Top Down MetaMorpheus\RawSpectra\uniprot-proteome_UP000005640_HumanRevPlusUnrev_012422.xml";
                _unknownModifications = new Dictionary<string, Modification>();
                var proteins = ProteinDbLoader.LoadProteinXML(path, true, DecoyType.None, GlobalVariables.AllModsKnown,
                    false, new List<string>(), out _unknownModifications);

                var peps = proteins.SelectMany(p =>
                    p.Digest(new DigestionParams("top-down"), new List<Modification>(), new List<Modification>()))
                    .ToList();

                var masses = peps.Select(p => Math.Log10(p.MonoisotopicMass)).Where(p => p != Double.NaN);

                var hist = new Histogram(masses, 0.01);
                hist.ExportHistogram(Path.Combine(directory, "DBLogHist.csv"), true);
            }
        }
    }
}

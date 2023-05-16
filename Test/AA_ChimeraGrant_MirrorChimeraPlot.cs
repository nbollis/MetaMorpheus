using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using EngineLayer;
using GuiFunctions;
using IO.ThermoRawFileReader;
using MassSpectrometry;
using NUnit.Framework;
using OxyPlot;
using Proteomics.Fragmentation;
using ThermoFisher.CommonCore.Data.Business;
using ThermoFisher.CommonCore.Data.Interfaces;

namespace Test
{
    [TestFixture, Apartment(ApartmentState.STA)]
    [TestFixture]
    public class AA_ChimeraGrant_MirrorChimeraPlot
    {
        public static string LibraryPath =
            @"D:\DataFiles\Hela_1\Fract3and5CreateLibrary\Task2-SearchTask\spectralLibrary_2023-05-15-11-34-04.msp";

        public static string ResultsPath =
            @"D:\DataFiles\Hela_1\Fraction4GPTMDSearch\Task2-SearchTask\AllPeptides.psmtsv";

        public static string SpectraPath =
            @"D:\DataFiles\Hela_1\20100611_Velos1_TaGe_SA_Hela_4.raw";

        [Test]
        public static void TESTNAME()
        {
            // setup 
            var dataFile = new ThermoDynamicData(SpectraPath);
            var library = new SpectralLibrary(new List<string>() { LibraryPath });
            var results = PsmTsvReader.ReadTsv(ResultsPath, out List<string> warnings);
            Assert.That(!warnings.Any());
            MetaDrawSettings.ExportType = "Png";
            MetaDrawSettings.UnannotatedPeakColor = OxyColors.Black;
            MetaDrawSettings.ShowLegend = false;

            // get chimeras
            var chimeras = results
                .Where(p => p.DecoyContamTarget == "T" && p.QValue <= 0.01 && p.AmbiguityLevel == "1")
                .GroupBy(p => p.Ms2ScanNumber)
                .Where(p => p.Count() >= 2)
                .ToList();

            // get those that have library spectra
            var chimerasWithSpectra = new List<(int, List<PsmFromTsv>)>();
            foreach (var chimericGroup in chimeras)
            {
                if (chimericGroup.All(p => library.TryGetSpectrum(p.FullSequence, p.PrecursorCharge, out var spec))) 
                    chimerasWithSpectra.Add((chimericGroup.Key, chimericGroup.ToList()));
            }

            //plot each of them
            var blankPsm = new PsmFromTsv(results.First(), results.First().FullSequence);
            blankPsm.MatchedIons.Clear();
            foreach (var chimeraGroup in chimerasWithSpectra)
            {
                // get basic plot
                var scanToPlot =
                    dataFile.GetOneBasedScanFromDynamicConnection(chimeraGroup.Item2.First().Ms2ScanNumber);

                library.TryGetSpectrum(chimeraGroup.Item2.First().FullSequence, chimeraGroup.Item2.First().PrecursorCharge,
                    out var librarySpec2);

                var plotView = new OxyPlot.Wpf.PlotView() { Name = "plotView" };
                //PeptideSpectrumMatchPlot plot = new(plotView, blankPsm, scanToPlot, new List<MatchedFragmentIon>(),
                //    false, librarySpec2);
                ChimeraSpectrumMatchPlot plot = new(plotView, scanToPlot, chimeraGroup.Item2);

                // add library annotations
                double minMirroedIntensity = 10000000000;
                for (int i = 0; i < chimeraGroup.Item2.Count; i++)
                {
                    library.TryGetSpectrum(chimeraGroup.Item2[i].FullSequence, chimeraGroup.Item2[i].PrecursorCharge,
                        out var librarySpec);
                    var libraryIons = librarySpec.MatchedFragmentIons;

                    // get library ions
                    double sumOfMatchedIonIntensities = 0;
                    double sumOfLibraryIntensities = 0;
                    foreach (var libraryIon in libraryIons)
                    {
                        int j = scanToPlot.MassSpectrum.GetClosestPeakIndex(libraryIon.Mz);
                        double intensity = scanToPlot.MassSpectrum.YArray[j];
                        sumOfMatchedIonIntensities += intensity;
                        sumOfLibraryIntensities += libraryIon.Intensity;
                    }

                    double multiplier = -1 * sumOfMatchedIonIntensities / sumOfLibraryIntensities;

                    List<MatchedFragmentIon> mirroredLibraryIons = new List<MatchedFragmentIon>();

                    foreach (MatchedFragmentIon libraryIon in libraryIons)
                    {
                        var neutralProduct = new Product(libraryIon.NeutralTheoreticalProduct.ProductType, libraryIon.NeutralTheoreticalProduct.Terminus,
                            libraryIon.NeutralTheoreticalProduct.NeutralMass, libraryIon.NeutralTheoreticalProduct.FragmentNumber,
                            libraryIon.NeutralTheoreticalProduct.AminoAcidPosition, libraryIon.NeutralTheoreticalProduct.NeutralLoss);

                        mirroredLibraryIons.Add(new MatchedFragmentIon(ref neutralProduct, libraryIon.Mz, multiplier * libraryIon.Intensity, libraryIon.Charge));
                    }


                    // plot each library ions
                    foreach (var ion in mirroredLibraryIons)
                    {
                        plot.AnnotatePeak(ion, false, true,
                            ChimeraSpectrumMatchPlot.ColorByProteinDictionary[i][1]);
                    }

                    minMirroedIntensity = minMirroedIntensity > mirroredLibraryIons.Min(p => p.Intensity)
                        ? mirroredLibraryIons.Min(p => p.Intensity)
                        : minMirroedIntensity;
                }

                double min = minMirroedIntensity * 1.2;
                plot.Model.Axes[1].AbsoluteMinimum = min * 2;
                plot.Model.Axes[1].AbsoluteMaximum = -min * 2;
                plot.Model.Axes[1].Zoom(min, -min * 1.2);
                plot.Model.Axes[1].LabelFormatter = DrawnSequence.YAxisLabelFormatter;

                // export
                string outPath = $@"D:\DataFiles\Hela_1\Fraction4GPTMDSearch\Figures\Testing8_{chimeraGroup.Item1}.png";
                plot.ExportPlot(outPath, new Canvas() {Height = 1, Width = 1});
            }



      
            

        }
    }
}

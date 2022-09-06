using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chemistry;
using EngineLayer;
using IO.MzML;
using MassSpectrometry;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Complex;
using NUnit.Framework;
using Proteomics.Fragmentation;

namespace Test
{
    public class ChimeraAnalyzingTest
    {

        public static List<PsmFromTsv> AllPsms { get; set; }
        public static List<MsDataScan> AllScans { get; set; }
        public static Dictionary<int, List<PsmFromTsv>> AllFilteredChimericGroupedByMS2ScanNumber { get; set; }

        [OneTimeSetUp]
        public static void OneTimeSetUP()
        {
            //string proteoformsPath = @"";
            string psmsPath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TopDownTestData\TDGPTMDSearchResults.psmtsv");
            string scansPath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"TopDownTestData\TDGPTMDSearchSingleSpectra.mzML");
            AllScans = Mzml.LoadAllStaticData(scansPath).GetAllScansList();
            AllPsms = PsmTsvReader.ReadTsv(psmsPath, out List<string> errors).Where(p => AllScans.Select(p => p.OneBasedScanNumber).Any(m => m == p.Ms2ScanNumber)).ToList();
            Assert.That(!errors.Any());
            AllFilteredChimericGroupedByMS2ScanNumber = AllPsms.Where(p => p.QValue <= 0.01 && p.PEP <= 0.5)
                .GroupBy(p => p.Ms2ScanNumber).Where(p => p.AsEnumerable().Count() > 1)
                .OrderByDescending(p => p.AsEnumerable().Count())
                .ToDictionary(p => p.Key, p => p.AsEnumerable<PsmFromTsv>().ToList());
        }

        [Test]
        public static void TempForBuildingMethod()
        {
            var chimera = AllFilteredChimericGroupedByMS2ScanNumber.First();
            var chimericScan = AllScans.First(p => p.OneBasedScanNumber == chimera.Value.First().Ms2ScanNumber);

            Matrix<double> chimeraMatrix =
                Matrix<double>.Build.Dense(chimericScan.MassSpectrum.XArray.Length, chimera.Value.Count);
            var targetVector = Vector<double>.Build.DenseOfArray(chimericScan.MassSpectrum.YArray);

            for (var i = 0; i < chimera.Value.Count; i++)
            {
                var chimericID = chimera.Value[i];
                foreach (var matchedFragmentIon in chimericID.MatchedIons)
                {
                    int peakIndex = chimericScan.MassSpectrum.GetClosestPeakIndex(
                        matchedFragmentIon.NeutralTheoreticalProduct.NeutralMass.ToMz(matchedFragmentIon.Charge));
                    chimeraMatrix[peakIndex, i] = matchedFragmentIon.Intensity;
                }
            }



            string outpath = @"C:\Users\Nic\Downloads\chimeraMatrix.csv";
            using (StreamWriter writer = new StreamWriter(File.Create(outpath)))
            {
                for (int i = 0; i < chimeraMatrix.EnumerateRows().Count(); i++) 
                {
                    writer.WriteLine(string.Join(",", chimeraMatrix.Row(i).AsArray()) + "," + targetVector[i]);
                }
            }
        }

        [Test]
        public static void TestNumaticsIsFunctioningProperly()
        {
            Vector<double> targetVector = Vector<double>.Build.DenseOfArray(new double[] { 1.5, 1.5, 1.5 });
            Matrix<double> matrix = Matrix<double>.Build.Dense(3, 3, 0.5);
            var prefactors = matrix.Solve(targetVector);
        }
    }
}

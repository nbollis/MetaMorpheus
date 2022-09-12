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
using TempMethods;

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
            string psmsPath = @"D:\Projects\Top Down MetaMorpheus\NBReplicate\Cali_MOxAndBioMetArtModsGPTMD_SearchInternal\Task3-SearchTask\AllPSMs.psmtsv";
            string scansPath = @"D:\Projects\Top Down MetaMorpheus\For paper KHB\Calibrated Spectra\FXN6_tr1_032017-calib.mzML";
            AllScans = Mzml.LoadAllStaticData(scansPath).GetAllScansList();
            AllPsms = PsmTsvReader.ReadTsv(psmsPath, out List<string> errors).Where(p =>
                AllScans.Select(p => p.OneBasedScanNumber).Any(m => m == p.Ms2ScanNumber) &&
                p.FileNameWithoutExtension == Path.GetFileNameWithoutExtension(scansPath)).ToList();
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
                Matrix<double>.Build.Dense( chimericScan.MassSpectrum.XArray.Length, chimera.Value.Count);
            var allPeaks = Vector<double>.Build.DenseOfArray(chimericScan.MassSpectrum.YArray);

            // add matched ion peaks to matrix
            for (var i = 0; i < chimera.Value.Count; i++)
            {
                var chimericID = chimera.Value[i];
                foreach (var matchedFragmentIon in chimericID.MatchedIons)
                {
                    int peakIndex = chimericScan.MassSpectrum.GetClosestPeakIndex(
                        matchedFragmentIon.Mz);

                    int identicalMatchedIons =
                        chimera.Value.Count(p => p.MatchedIons.Any(m => m.Mz.Equals(matchedFragmentIon.Mz)));
                    chimeraMatrix[peakIndex, i] = matchedFragmentIon.Intensity / identicalMatchedIons;
                }
            }

            // add unmatched peaks to matrix
            //var columns = chimeraMatrix.EnumerateColumns().Select(p => p.ToArray()).ToArray();
            //double[] unmatchedPeaks = new double[columns[0].Length];
            //for (int i = 0; i < columns[0].Length; i++)
            //{
            //    double unaccountedPeakHeight = allPeaks[i] - columns.ColumnSum(i);
            //    unmatchedPeaks[i] = unaccountedPeakHeight < 0 ? 0 : unaccountedPeakHeight;
            //}
            //chimeraMatrix = chimeraMatrix.InsertColumn(columns.Length, Vector<double>.Build.DenseOfArray(unmatchedPeaks));

            // solve matrix
            var prefactors = chimeraMatrix.Solve(allPeaks);

            // print matrix and all peaks to csv
            string outpath = @"C:\Users\Nic\Downloads\chimeraMatrix.csv";
            using (StreamWriter writer = new StreamWriter(File.Create(outpath)))
            {
                for (int i = 0; i < chimeraMatrix.EnumerateRows().Count(); i++)
                {
                    writer.WriteLine(string.Join(",", chimeraMatrix.Row(i).AsArray()) + "," + allPeaks[i]);
                }
            }
        }

        [Test]
        public static void TestNumaticsIsFunctioningProperly()
        {
            double[,] val = { { 3, 2, -1}, { 2, -2, 4}, { -1, 0.5, -1} };
            Matrix<double> M = Matrix<double>.Build.DenseOfArray(val);
            double[] tar = new double[] { 1, -2, 0 };
            Vector<double> target = Vector<double>.Build.DenseOfArray(tar);
            var test = M.Solve(target);
            Assert.That(test.Storage.ToArray().Select(p => Math.Round(p, 6)).ToArray().SequenceEqual(new double[]{1, -2, -2}));

            Vector<double> targetVector = Vector<double>.Build.DenseOfArray(new double[] { 3, 6, 9});
            Matrix<double> matrix = Matrix<double>.Build.DenseOfArray(new double[,]{{1, 2, 3}, { 1, 2, 3 }, { 1, 2, 3 } });
            var prefactors = matrix.Solve(targetVector);
        }

        [Test]
        public static void ExplorePrefactorsForSimpleSpectraExample()
        {
            // one counts for half, the other does nothing
            double[] psm1 = new double[] { 2, 1, 1, 2, 1, 1, 4, 3, 2, 1, 2 };
            double[] psm2 = new double[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            double[] unmatched = new double[] { 2, 1, 1, 2, 1, 1, 4, 3, 2, 1, 2 };
            double[] total = new double[] { 4, 2, 2, 4, 2, 2, 8, 6, 4, 2, 4};
            Assert.That((psm1.Length == psm2.Length) == (unmatched.Length == total.Length));

            List<double[]> columns = new List<double[]>() { psm1, psm2, unmatched };
            Matrix<double> matrix = Matrix<double>.Build.DenseOfColumnArrays(columns);
            Assert.That(matrix.RowCount == psm1.Length);
            Assert.That(matrix.ColumnCount == columns.Count);
            Assert.That(matrix.Column(0).ToArray().SequenceEqual(psm1));
            Assert.That(matrix.Column(1).ToArray().SequenceEqual(psm2));
            Assert.That(matrix.Column(2).ToArray().SequenceEqual(unmatched));
            var prefactors = matrix.Solve(Vector<double>.Build.DenseOfArray(total));

            // one counts for half, the other is a small amount
            psm1 = new double[] { 2, 1, 1, 2, 1, 1, 4, 3, 2, 2, 2 };
            psm2 = new double[] { 1, 0, 0, 0, 0, 0, 3, 0, 0, 0, 1 };
            unmatched = new double[] { 1, 1, 1, 2, 1, 1, 1, 3, 2, 1, 1 };
            total = new double[] { 4, 2, 2, 4, 2, 2, 8, 6, 4, 2, 4 };
            Assert.That((psm1.Length == psm2.Length) == (unmatched.Length == total.Length));

            columns = new List<double[]>() { psm1, psm2, unmatched };
            matrix = Matrix<double>.Build.DenseOfColumnArrays(columns);
            Assert.That(matrix.RowCount == psm1.Length);
            Assert.That(matrix.ColumnCount == columns.Count);
            Assert.That(matrix.Column(0).ToArray().SequenceEqual(psm1));
            Assert.That(matrix.Column(1).ToArray().SequenceEqual(psm2));
            Assert.That(matrix.Column(2).ToArray().SequenceEqual(unmatched));
            prefactors = matrix.Solve(Vector<double>.Build.DenseOfArray(total));

            // one counts for a third, the other is a small amount
            psm1 = new double[] { 2, 1, 1, 2, 1, 1, 3, 2, 1, 1, 2 };
            psm2 = new double[] { 1, 0, 0, 0, 0, 0, 3, 0, 0, 0, 1 };
            unmatched = new double[] { 3, 2, 2, 4, 2, 2, 3, 4, 2, 2, 3};
            total = new double[] { 6, 3, 3, 6, 3, 3, 9, 6, 3, 3, 6 };
            Assert.That((psm1.Length == psm2.Length) == (unmatched.Length == total.Length));

            columns = new List<double[]>() { psm1, psm2, unmatched};
            matrix = Matrix<double>.Build.DenseOfColumnArrays(columns);
            Assert.That(matrix.RowCount == psm1.Length);
            Assert.That(matrix.ColumnCount == columns.Count);
            Assert.That(matrix.Column(0).ToArray().SequenceEqual(psm1));
            Assert.That(matrix.Column(1).ToArray().SequenceEqual(psm2));
            Assert.That(matrix.Column(2).ToArray().SequenceEqual(unmatched));
            prefactors = matrix.Solve(Vector<double>.Build.DenseOfArray(total));

            // random mixtrue of spectra
            psm1 = new double[] { 1, 1, 0, 1, 0, 0, 0, 3, 1, 0, 0 };
            psm2 = new double[] { 1, 0, 0, 1, 2, 0, 4, 0, 1, 0, 0 };
            unmatched = new double[] { 0, 0, 1, 0, 0, 1, 0, 0, 0, 1, 2 };
            total = new double[] { 2, 1, 1, 2, 2, 1, 4, 3, 2, 1, 2 };
            Assert.That((psm1.Length == psm2.Length) == (unmatched.Length == total.Length));

            columns = new List<double[]>() {psm1, psm2, unmatched };
            matrix = Matrix<double>.Build.DenseOfColumnArrays(columns);
            Assert.That(matrix.RowCount == psm1.Length);
            Assert.That(matrix.ColumnCount == columns.Count);
            Assert.That(matrix.Column(0).ToArray().SequenceEqual(psm1));
            Assert.That(matrix.Column(1).ToArray().SequenceEqual(psm2));
            Assert.That(matrix.Column(2).ToArray().SequenceEqual(unmatched));
            prefactors = matrix.Solve(Vector<double>.Build.DenseOfArray(total));
        }
    }
}

namespace TempMethods
{
    public static class DoubleExtensions
    {
        public static double RowSum(this double[][] value, int index)
        {
            double result = 0;
            for (int i = 0; i <= value.GetUpperBound(1); i++)
            {
                result += value[index][i];
            }

            return result;
        }

        public static double ColumnSum(this double[][] value, int index)
        {
            double result = 0;
            for (int i = 0; i <= value.GetUpperBound(0); i++)
            {
                result += value[i][index];
            }

            return result;
        }
    }
}



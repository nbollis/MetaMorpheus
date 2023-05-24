using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using IO.MzML;
using MassSpectrometry;
using MzLibUtil;
using Nett;
using SpectralAveraging;
using TaskLayer;
using GuiFunctions;
using Microsoft.VisualBasic.CompilerServices;
using Easy.Common.Extensions;
using EngineLayer;
using Math = System.Math;

namespace Test.AveragingPaper
{
    public class AveragingMassAccuracyTestRunner
    {

        // TODO: Add bool overwrite to constructor

        /// <summary>
        /// Will use original file path as location to place directories
        /// </summary>
        /// <param name="originalFilePath"></param>
        /// <param name="averagingParameters"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public AveragingMassAccuracyTestRunner(string originalFilePath,
            (int Charge, double Mz)[] chargeMzPairs, List<SpectralAveragingParameters> averagingParameters,
            double ppmTolerance = 10, double relativeIntensityCutoff = 5, bool overwrite = false)
        {
            this.originalFilePath = originalFilePath;
            this.chargeMzPairs = chargeMzPairs;
            this.relativeIntensityCutoff = relativeIntensityCutoff;
            overWrite = overwrite;
            tolerance = new PpmTolerance(ppmTolerance);

            deconvoluter = new Deconvoluter(DeconvolutionType.ClassicDeconvolution,
                new ClassicDeconvolutionParameters(1, 30, 10, 3));
            mainOutDirectory = Path.GetDirectoryName(originalFilePath);
            originalFileName = Path.GetFileNameWithoutExtension(originalFilePath);
            AllFileResults = new();
            // create averaging name dictionary
            allParameters = new Dictionary<string, SpectralAveragingParameters>();
            foreach (var parameter in averagingParameters)
            {
                string name = GetParameterString(parameter);
                allParameters.Add(name, parameter);
            }
        }



        public AveragingMassAccuracyTestRunner(string originalFilePath, (int Charge, double Mz)[] chargeMzPairs,
            bool loadIndividualResults = false, bool loadScanResults = true,
            double ppmTolerance = 10, double relativeIntensityCutoff = 5)
        {
            this.originalFilePath = originalFilePath;
            this.chargeMzPairs = chargeMzPairs;
            this.relativeIntensityCutoff = relativeIntensityCutoff;
            tolerance = new PpmTolerance(ppmTolerance);

            deconvoluter = new Deconvoluter(DeconvolutionType.ClassicDeconvolution,
                new ClassicDeconvolutionParameters(1, 30, 10, 3));
            mainOutDirectory = Path.GetDirectoryName(originalFilePath);
            originalFileName = Path.GetFileNameWithoutExtension(originalFilePath);

            AllFileResults = new();
            allParameters = new();
            foreach (var directory in Directory.GetDirectories(mainOutDirectory))
            {
                var files = Directory.GetFiles(directory).ToList();

                // only load in if all processing has been performed
                if (files.Count(p => p.Contains(".mzML")) != 1 ||
                    files.Count(p => p.Contains(".toml")) != 1 ||
                    files.Count(p => p.Contains(".tsv")) != 2)
                    continue;

                var parameters = Toml.ReadFile<SpectralAveragingParameters>(files.First(p => p.Contains(".toml")), MetaMorpheusTask.tomlConfig);
                allParameters.Add(GetParameterString(parameters), parameters);



                if (loadScanResults)
                {
                    var scansPath = files.First(p => p.Contains("ScanMass"));
                    var scansLines = File.ReadAllLines(scansPath);
                    List<ScanMassAccuracyResults> scanResults = new();
                    for (int i = 1; i < scansLines.Length; i++)
                    {
                        scanResults.Add(new ScanMassAccuracyResults(scansLines[i]));
                    }

                    if (loadIndividualResults)
                    {
                        // load all in
                        var individualPeakPath = files.First(p => p.Contains("IndividualPeak"));
                        var individualPeakLines = File.ReadAllLines(individualPeakPath);
                        List<IndividualPeakResult> individualPeakResults = new();
                        for (int i = 1; i < individualPeakLines.Length; i++)
                        {
                            individualPeakResults.Add(new IndividualPeakResult(individualPeakLines[i]));
                        }

                        // assign to correct scan result
                        foreach (var group in individualPeakResults.GroupBy(p => p.ScanNumber))
                        {
                            var targetResult = scanResults.First(p => p.ScanNumber == group.Key);
                            foreach (var individualResult in individualPeakResults.Where(p => p.ScanNumber == targetResult.ScanNumber))
                            {
                                targetResult.IndividualPeakResults.Add(individualResult.TheoreticalMz, individualResult);
                            }
                        }
                    }

                    AllFileResults.Add(new FileMassAccuracyResults(scanResults, parameters));

                }
                else
                {
                    //TODO: Implement tsv reading to filemass accuracy results
                }


            }
        }

        internal List<FileMassAccuracyResults> AllFileResults { get; private set; }

        #region Private Properties

        internal string originalFilePath;
        internal string originalFileName;
        internal string mainOutDirectory;

        internal (int Charge, double Mz)[] chargeMzPairs;
        internal Dictionary<string, SpectralAveragingParameters> allParameters;

        internal Deconvoluter deconvoluter;
        internal PpmTolerance tolerance;
        internal double relativeIntensityCutoff = 5;
        internal bool overWrite = false;

        #endregion


        #region Public Methods

        public void Run()
        {
            SetUpOutputDirectories();
            AverageSpectra();
            MatchAll();

        }

        /// <summary>
        /// Creates a folder for each averaging parameter if it did not exist already
        /// Will then create a toml in each folder
        /// </summary>
        public void SetUpOutputDirectories()
        {
            foreach (var parameter in allParameters)
            {
                SetUpOutputDirectory(parameter.Key, parameter.Value);
            }
        }

        /// <summary>
        /// averages spectra and outputs to output directory
        /// </summary>
        public void AverageSpectra()
        {
            // load in Instance Data
            var scansToAverage = SpectraFileHandler.LoadAllScansFromFile(originalFilePath);
            var sourceFile = SpectraFileHandler.GetSourceFile(originalFilePath);

            // foreach parameter tested
            foreach (var parameter in allParameters)
            {
                string outputDirectory = Path.Combine(mainOutDirectory, parameter.Key);
                string averagedScansPath = Path.Combine(outputDirectory, $"{originalFileName}-averaged.mzML");
                MsDataScan[] averagedScans;

                if (overWrite)
                {
                    if (Directory.Exists(outputDirectory))
                    {
                        Directory.Delete(outputDirectory, true);
                        SetUpOutputDirectory(parameter.Key, parameter.Value);
                    }
                    averagedScans = SpectraFileAveraging.AverageSpectraFile(scansToAverage, parameter.Value);
                }
                else
                {

                    if (File.Exists(averagedScansPath)) continue;
                    if (!Directory.Exists(outputDirectory))
                    {
                        SetUpOutputDirectory(parameter.Key, parameter.Value);
                    }
                    averagedScans = SpectraFileAveraging.AverageSpectraFile(scansToAverage, parameter.Value);
                }

                // output averaged scans
                MsDataFile msDataFile = new MsDataFile(averagedScans, sourceFile);
                MzmlMethods.CreateAndWriteMyMzmlWithCalibratedSpectra(msDataFile, averagedScansPath, true);
            }
        }

        /// <summary>
        /// Goes through each parameter and performs the matching
        /// </summary>
        public void MatchAll()
        {
            // match original
            string outputDirectory = Path.Combine(mainOutDirectory, "0riginal");
            if (!Directory.Exists(outputDirectory) || overWrite)
            {
                Directory.CreateDirectory(outputDirectory);
                var fileResults = MatchAllSpectraInFile(originalFilePath, null);
                AllFileResults.Add(fileResults);
                OutputFileResults(fileResults, outputDirectory);
            }

            // match all averaged
            foreach (var parameter in allParameters)
            {
                outputDirectory = Path.Combine(mainOutDirectory, parameter.Key);
                var directoryFiles = Directory.GetFiles(outputDirectory);

                // if this processing has already been done
                if (directoryFiles.Count(p => p.Contains(".tsv")) == 2 && !overWrite) continue;

                string averagedScansPath = Path.Combine(outputDirectory, $"{originalFileName}-averaged.mzML");
                var fileResults = MatchAllSpectraInFile(averagedScansPath, null);
                AllFileResults.Add(fileResults);
                OutputFileResults(fileResults, outputDirectory);
            }
        }

        public void SearchData(string databasePath, string searchToml)
        {
            var searchTask = Toml.ReadFile<SearchTask>(searchToml, MetaMorpheusTask.tomlConfig);
            foreach (var parameter in allParameters)
            {
                var outputDirectory = Path.Combine(mainOutDirectory, parameter.Key);
                var averagedSpectraPath = Path.Combine(outputDirectory, $"{originalFileName}-averaged.mzML");

                if (!File.Exists(averagedSpectraPath)) continue;
                if (Directory.Exists(Path.Combine(outputDirectory, parameter.Key))) continue;

                var engine = new EverythingRunnerEngine
                (
                    new List<(string, MetaMorpheusTask)>() { ($"{parameter.Key}", searchTask) },
                    new List<string>() { averagedSpectraPath },
                    new List<DbForTask>() { new DbForTask(databasePath, false) },
                    outputDirectory
                );
                engine.Run();

                // TODO: Add psm count to all file results

                //var psmCount = 2;
                //AllFileResults.First(p => p.Parameters.DeepEquals(parameter.Value)).PsmCount = psmCount;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Creates a folder for a averaging parameter if it did not exist already
        /// Will then create a toml in each folder
        /// </summary>
        private void SetUpOutputDirectory(string paramShorthand, SpectralAveragingParameters parameters)
        {
            string directoryPath = Path.Combine(mainOutDirectory, paramShorthand);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);

                string averagingTomlPath = Path.Combine(directoryPath, "AveragingParameters.toml");
                Toml.WriteFile(parameters, averagingTomlPath, MetaMorpheusTask.tomlConfig);
            }
        }

        /// <summary>
        /// Goes through each spectra file in a mzml or raw and matches the peaks
        /// </summary>
        /// <param name="filepath"></param>
        internal FileMassAccuracyResults MatchAllSpectraInFile(string filepath, SpectralAveragingParameters parameters)
        {
            List<ScanMassAccuracyResults> scanResults = new();
            foreach (var scan in SpectraFileHandler.LoadAllScansFromFile(filepath)
                         .Where(p => p.MsnOrder == 1))
            {
                scanResults.Add(MatchPeaks(scan));
            }
            return new FileMassAccuracyResults(scanResults, parameters);
        }

        /// <summary>
        /// Matches peaks for a single spectrum
        /// </summary>
        /// <param name="scan"></param>
        internal ScanMassAccuracyResults MatchPeaks(MsDataScan scan)
        {
            double yCutoff = scan.MassSpectrum.YArray.Max() * relativeIntensityCutoff / 100.0;
            List<IndividualPeakResult> peakResults = new List<IndividualPeakResult>();
            foreach (var chargeMz in chargeMzPairs)
            {
                int experimentalCharge = 0;
                double experimentalMz = 0;
                double intensity = 0;
                bool found = false;
                bool resolvable = false;
                int peakCount = 0;

                // peaks w/n tolerance above cutoff
                var peaksWithinTolerance = scan.MassSpectrum
                    .Extract(tolerance.GetMinimumValue(chargeMz.Mz), tolerance.GetMaximumValue(chargeMz.Mz))
                    .Where(p => p.Intensity >= yCutoff)
                    .ToList();

                // if any peaks, get their mz and intensity
                if (peaksWithinTolerance.Any())
                {
                    found = true;
                    var mostAbundantPeak = peaksWithinTolerance.MaxBy(p => p.Intensity);
                    experimentalMz = mostAbundantPeak.Mz;
                    intensity = mostAbundantPeak.Intensity / scan.MassSpectrum.YArray.Max();
                }

                // deconvolute +- 5 within region, take top scoring result
                var deconvolutionResults =
                    deconvoluter.Deconvolute(scan, new MzRange(chargeMz.Mz - 5, chargeMz.Mz + 5))
                        .ToList();
                if (deconvolutionResults.Any())
                {
                    var topDeconResult = deconvolutionResults.MaxBy(p => p.Score);
                    experimentalCharge = topDeconResult.Charge;
                    resolvable = topDeconResult.Charge == chargeMz.Charge;
                    peakCount = topDeconResult.Peaks.Count;
                }


                IndividualPeakResult result = new IndividualPeakResult(scan.OneBasedScanNumber, chargeMz.Charge,
                    chargeMz.Mz, experimentalCharge, experimentalMz, intensity, found, resolvable,
                    peakCount);
                peakResults.Add(result);
            }

            return new ScanMassAccuracyResults(chargeMzPairs, scan, peakResults);
        }

        internal void OutputFileResults(FileMassAccuracyResults fileResults, string outputDirectory)
        {
            // create individual scan tsv
            string individualScanTsvPath = Path.Combine(outputDirectory, "ScanMassAccuracyResults.tsv");
            var scanResults = fileResults.AllResults.Select(p => (ITsv)p);
            scanResults.ExportAsTsv(individualScanTsvPath);

            // create all peaks tsv
            string allPeaksTsvPath = Path.Combine(outputDirectory, "IndividualPeakMassAccuracyResults.tsv");
            var peakResults = fileResults.AllResults
                .SelectMany(p => p.IndividualPeakResults
                    .Select(m => m.Value as ITsv));
            peakResults.ExportAsTsv(allPeaksTsvPath);
        }

        private string GetParameterString(SpectralAveragingParameters parameter)
        {
            string name = $"{parameter.NumberOfScansToAverage}Scans_" +
                          $"{parameter.BinSize}_" +
                          $"{parameter.NormalizationType}_" +
                          $"{parameter.SpectralWeightingType}_" +
                          $"{parameter.OutlierRejectionType}";
            switch (parameter.OutlierRejectionType)
            {
                case OutlierRejectionType.PercentileClipping:
                    name += $"_{parameter.Percentile}";
                    break;
                case OutlierRejectionType.SigmaClipping:
                case OutlierRejectionType.WinsorizedSigmaClipping:
                case OutlierRejectionType.AveragedSigmaClipping:
                    name += $"_{parameter.MinSigmaValue}_{parameter.MaxSigmaValue}";
                    break;
                case OutlierRejectionType.NoRejection:
                    break;
                case OutlierRejectionType.MinMaxClipping:
                    break;
                case OutlierRejectionType.BelowThresholdRejection:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return name;
        }

        #endregion

    }

    internal static class AveragingMassAccuracyExtensions
    {
        public static IEnumerable<double> GetAverageValues(this IEnumerable<FileMassAccuracyResults> results,
            ResultType type)
        {
            foreach (var result in results)
            {
                yield return result.GetAverageValue(type);
            }
        }

        public static double GetAverageValue(this FileMassAccuracyResults results, ResultType type)
        {
            switch (type)
            {
                case ResultType.TheoreticalPeaksFound:
                    return results.AverageMzFoundPerScan;

                case ResultType.DeconvolutedFeatures:
                    return results.AverageChargeStateResolvablePerScan;

                case ResultType.PpmError:
                    return results.AverageMzPpmError;

                case ResultType.MedOverDev:
                    return results.AverageMedOverStandardDevOfPeaks;

                case ResultType.PsmCount:
                    return results.PsmCount;

                case ResultType.IsotopicPeakCount:
                    return results.AverageIsotopicPeakCount;

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public static IEnumerable<double> GetStandardDeviationValues(this IEnumerable<FileMassAccuracyResults> results,
            ResultType type)
        {
            foreach (var result in results)
            {
                yield return result.GetStandardDeviationValue(type);
            }
        }

        public static double GetStandardDeviationValue(this FileMassAccuracyResults results, ResultType type)
        {
            switch (type)
            {
                case ResultType.TheoreticalPeaksFound:
                    return results.StdMzFoundPerScan;

                case ResultType.DeconvolutedFeatures:
                    return results.StdChargeStateResolvablePerScan;

                case ResultType.PpmError:
                    return results.StdMzPpmError;

                case ResultType.MedOverDev:
                    return results.StdMedOverStandardDeviationOfPeaks;

                case ResultType.IsotopicPeakCount:
                    return results.AverageIsotopicPeakCount;

                case ResultType.PsmCount:
                    return results.PsmCount;

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public static bool DeepEquals(this SpectralAveragingParameters first, SpectralAveragingParameters second)
        {
            if (Math.Abs(first.BinSize - second.BinSize) > 0.00001)
                return false;
            if (Math.Abs(first.NumberOfScansToAverage - second.NumberOfScansToAverage) > 0.00001)
                return false;
            if (first.OutlierRejectionType != second.OutlierRejectionType)
                return false;
            if (first.SpectralWeightingType != second.SpectralWeightingType)
                return false;
            if (first.NormalizationType != second.NormalizationType)
                return false;
            if (Math.Abs(first.MinSigmaValue - second.MinSigmaValue) > 0.00001)
                return false;
            if (Math.Abs(first.MaxSigmaValue - second.MaxSigmaValue) > 0.00001)
                return false;

            return !(Math.Abs(first.Percentile - second.Percentile) > 0.00001);
        }
    }
}

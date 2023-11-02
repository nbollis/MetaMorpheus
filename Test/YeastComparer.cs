using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Easy.Common.Extensions;
using EngineLayer;
using MassSpectrometry;
using MathNet.Numerics.LinearAlgebra.Factorization;
using MathNet.Numerics.Statistics;
using Microsoft.ML.Trainers.FastTree;
using Nett;
using NUnit.Framework;
using Readers;
using TaskLayer;

namespace Test
{
    [TestFixture]
    public class TestScalfYeastComparer
    {
        public static string DirectoryPath = @"B:\Users\Scalf\10-09-23_yeast-stnds_Lumos-issues";
        public static string DirectoryPathSecond = @"B:\Users\Scalf\11-01-23_YL-stnds";
        public static string ResultDirectoryPath = @"B:\Users\Scalf\YeastParsingResults_NB";


        public static string TomlPath =
            @"B:\Users\Scalf\10-09-23_yeast-stnds_Lumos-issues\Task2-SearchTaskconfig.toml";

        public static string DbPath =
            @"B:\Users\Scalf\10-09-23_yeast-stnds_Lumos-issues\uniprot-yeast_4932_reviewed_ubiqu_cytoC_hemoglobin_RNaseA.fasta";


        [Test]
        public static void RunSearches()
        {

            foreach (var file in Directory.GetFiles(DataPaths.First().Value, "*.raw"))
            {
                SearchTask task = Toml.ReadFile<SearchTask>(TomlPath, MetaMorpheusTask.tomlConfig);
                DbForTask dbForTask = new DbForTask(DbPath, false);

                string outputFolder = Path.Combine(Path.GetDirectoryName(file),
                    Path.GetFileNameWithoutExtension(file));

                EverythingRunnerEngine a = new EverythingRunnerEngine(
                    new List<(string, MetaMorpheusTask)>() { ("Task1-SearchTask", task) }, new List<string>() { file },
                    new List<DbForTask>() { dbForTask }, outputFolder);
                a.Run();

            }
        }

        public static Dictionary<string, string> DataPaths => new()
        {
            {"Control", @"B:\Users\Scalf\10-09-23_yeast-stnds_Lumos-issues\Control"},
            {"FirstBad", @"B:\Users\Scalf\10-09-23_yeast-stnds_Lumos-issues\FirstBad"},
            {"10-30-23",@"B:\Users\Scalf\10-31-23_YL-stnds" },
            {"11-1-23", @"B:\Users\Scalf\11-01-23_YL-stnds"},
            {"07-05-23", @"B:\Users\Scalf\07-05-23_YL-std"},
        };

        [Test]
        public static void RunAllInDataPaths()
        {
            Dictionary<string, List<SearchSet>> sets = new();
            foreach (var dataSet in DataPaths)
            {
                foreach (var file in Directory.GetFiles(dataSet.Value, "*.raw"))
                {
                    string outputFolder = Path.Combine(Path.GetDirectoryName(file),
                        Path.GetFileNameWithoutExtension(file));

                    List<PsmFromTsv> psms = new();
                    MsDataFile dataFile = MsDataFileReader.GetDataFile(file).LoadAllStaticData();
                    if (Directory.Exists(outputFolder) &&
                        Directory.GetFiles(outputFolder).Any(p => p.Contains("allResults.txt")))
                    {
                        var psmsPath = Directory.GetFiles(outputFolder, "*AllPSMs.psmtsv", SearchOption.AllDirectories).First();
                        psms = PsmTsvReader.ReadTsv(psmsPath, out var warnings).ToList();
                    }
                    else
                    {
                        SearchTask task = Toml.ReadFile<SearchTask>(TomlPath, MetaMorpheusTask.tomlConfig);
                        DbForTask dbForTask = new DbForTask(DbPath, false);

                        EverythingRunnerEngine a = new EverythingRunnerEngine(
                            new List<(string, MetaMorpheusTask)>() { ("Task1-SearchTask", task) }, new List<string>() { file },
                            new List<DbForTask>() { dbForTask }, outputFolder);
                        a.Run();

                        var psmsPath = Directory.GetFiles(outputFolder, "*AllPSMs.psmtsv", SearchOption.AllDirectories).First();
                        psms = PsmTsvReader.ReadTsv(psmsPath, out var warnings).ToList();
                    }

                    var set = new SearchSet(dataFile, psms);
                    set.ParseStats();
                    set.Export(dataSet.Value);
                    sets.AddOrUpdate(dataSet.Key, set);
                }
            }

            //export individual and combined values
            foreach (var condition in sets)
            {
                SearchSet.CombineAndExport(condition.Value, condition.Key);
            }

            ExportBasePeaksAndTics(sets);
        }

        public static void ExportBasePeaksAndTics(Dictionary<string, List<SearchSet>> sets)
        {
            var outpath = Path.Combine(DirectoryPath, "BasePeakAndTicIntensities.tsv");
            
            using (var sw = new StreamWriter(outpath))
            {
                sw.WriteLine("Condition\tFile\tPsmScore\tPsmQValue\tScanNum\tBasePeakIntensity\tTIC\tTime(s)");
                foreach (var condition in sets)
                {
                    foreach (var set in condition.Value)
                    {
                        set.DataFile.InitiateDynamicConnection();
                        foreach (var psm in set.Psms)
                        {
                            var scan = set.DataFile.GetOneBasedScanFromDynamicConnection(psm.Ms2ScanNumber);
                            var previousScan = set.DataFile.GetOneBasedScanFromDynamicConnection(psm.Ms2ScanNumber - 1);
                            var deltaRetentionTime = scan.RetentionTime - previousScan.RetentionTime;
                            
                            sw.WriteLine($"{condition.Key}\t{set.DataFile.FilePath}\t{psm.Score}\t{psm.QValue}\t{scan.OneBasedScanNumber}\t{scan.SelectedIonIntensity}\t{scan.TotalIonCurrent}\t{deltaRetentionTime}");
                        }
                        set.DataFile.CloseDynamicConnection();
                    }
                }
            }
        }
    }

    public static class Extensions
    {
        public static void AddOrUpdate<T>(this Dictionary<string, List<T>> dict, string key, T value)
        {
            if (dict.TryGetValue(key, out var value1))
            {
                value1.Add(value);
            }
            else
            {
                dict.Add(key, new List<T>() { value });
            }
        }
    }

    public class SearchSet
    {
        public MsDataFile DataFile { get; set; }
        public List<PsmFromTsv> Psms { get; set; }
        public List<StatItem> StatItems { get; set; }
        public Histogram TicValue { get; set; }
        public Histogram BasePeakValue { get; set; }

        public SearchSet(MsDataFile dataFile, List<PsmFromTsv> psms)
        {
            if (psms.Select(p => p.FileNameWithoutExtension).Distinct().Count() != 1)
            {
                throw new Exception("Multiple data files in psms");
            }
            if (psms.Select(p => p.FileNameWithoutExtension).First() !=
                Path.GetFileNameWithoutExtension(dataFile.FilePath))
            {
                throw new Exception("Data file and psms do not match");
            }

            DataFile = dataFile;
            Psms = psms;
            StatItems = new List<StatItem>();
        }


        public void ParseStats()
        {
            var time = GetScanTimes();
            StatItems.Add(new StatItem(StatItem.Ms1Times, time.ms1));
            StatItems.Add(new StatItem(StatItem.Ms2Times, time.ms2));

            var scanInfo = GetMs2Information();
            StatItems.Add(new StatItem(StatItem.Ms2BasePeak, scanInfo.basePeak));
            StatItems.Add(new StatItem(StatItem.Ms2Tic, scanInfo.Tics));

            TicValue = new Histogram(scanInfo.Tics, scanInfo.Tics.Max() / 20);
            BasePeakValue = new Histogram(scanInfo.basePeak, scanInfo.basePeak.Max() / 20);
        }

        public void Export(string outputPath)
        {
            var outputFolder = Path.Combine(outputPath, Path.GetFileNameWithoutExtension(DataFile.FilePath));
            if (!Directory.Exists(outputFolder))
            {
                throw new ArgumentException();
            }

            var statPath = Path.Combine(outputFolder, "Stats.tsv");
            using (var sw = new StreamWriter(statPath))
            {
                sw.WriteLine("Name\tValue\tMean\tStDev\tZScore");
                foreach (var statItem in StatItems)
                {
                    sw.WriteLine(statItem);
                }
            }

            var ticPath = Path.Combine(outputFolder, "TicHistogram.tsv");
            TicValue.ExportHistogram(ticPath);

            var basePeakPath = Path.Combine(outputFolder, "BasePeakHistogram.tsv");
            BasePeakValue.ExportHistogram(basePeakPath);
        }

        public static void CombineAndExport(List<SearchSet> set, string name)
        {
            var outputFolder = Path.Combine(Path.GetDirectoryName(set.First().DataFile.FilePath), name);
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            var statPath = Path.Combine(outputFolder, "Stats.tsv");
            using (var sw = new StreamWriter(statPath))
            {
                sw.WriteLine("Name\tValue\tMean\tStDev\tZScore");
                foreach (var statItem in set.SelectMany(p => p.StatItems).GroupBy(p => p.Name)
                             .Select(p => new StatItem(p.Key, p.Select(v => v.Value).ToList())))
                {
                    sw.WriteLine(statItem);
                }
            }

            var ticPath = Path.Combine(outputFolder, "TicHistogram.tsv");
            var gram = set.Select(p => p.TicValue).Aggregate((a, b) => a + b);
            gram.ExportHistogram(ticPath);

            var basePeakPath = Path.Combine(outputFolder, "BasePeakHistogram.tsv");
            var gram2 = set.Select(p => p.BasePeakValue).Aggregate((a, b) => a + b);
            gram2.ExportHistogram(basePeakPath);
        }

        
    private (double[] ms1, double[] ms2) GetScanTimes()
        {
            var scans = DataFile.Scans;
            List<double> ms1Times = new List<double>();
            List<double> ms2Times = new List<double>();

            double previousTime = 0;
            for (int i = 1; i < scans.Length; i++)
            {
                bool isMs1 = scans[i - 1].MsnOrder == 1;
                if (isMs1)
                {
                    ms1Times.Add(scans[i - 1].RetentionTime - previousTime);
                }
                else
                {
                    ms2Times.Add(scans[i - 1].RetentionTime - previousTime);
                }

                previousTime = scans[i - 1].RetentionTime;
            }
            return (ms1Times.ToArray(), ms2Times.ToArray());
        }

        private (double[] basePeak, double[] Tics) GetMs2Information()
        {
            var scans = DataFile.Scans;
            var psms = Psms;
            List<double> intensities = new List<double>();
            List<double> tics = new List<double>();

            foreach (var psm in psms)
            {
                var scan = scans[psm.Ms2ScanNumber - 1];
                intensities.Add(scan.SelectedIonIntensity ?? 0);
                tics.Add(scan.TotalIonCurrent);
            }

            return (intensities.ToArray(), tics.ToArray());
        }
    }

    public class StatItem
    {

        #region Names

        public static string Ms1Times = "Ms1 Times";
        public static string Ms2Times = "Ms2 Times";
        public static string Ms2BasePeak = "Ms2 Base Peak Intensity";
        public static string Ms2Tic = "Ms2 TIC";

        #endregion


        public string Name { get; set; }
        public double Value { get; set; }
        public double Mean { get; set; }
        public double StDev { get; set; }
        public double ZScore { get; set; }

        public StatItem(string name, double value, double mean, double stDev)
        {
            Name = name;
            Value = value;
            Mean = mean;
            StDev = stDev;
            ZScore = (value - mean) / stDev;
        }

        public StatItem(string name, List<double> values)
        {
            Name = name;
            Value = values.Average();
            Mean = values.Mean();
            StDev = values.StandardDeviation();
            ZScore = (Value - Mean) / StDev;
        }

        public StatItem(string name, double[] values)
        {
            Name = name;
            Value = values.Average();
            Mean = values.Mean();
            StDev = values.StandardDeviation();
            ZScore = (Value - Mean) / StDev;
        }

        public override string ToString()
        {
            return $"{Name}\t{Value}\t{Mean}\t{StDev}\t{ZScore}";
        }
    }
}

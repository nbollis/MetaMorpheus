using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

namespace Test
{
    public class FlashDeconMs1TsvFile
    {
        public string FilePath { get; }
        public List<FlashDeconMs1TsvEntry> Entries { get; }

        public FlashDeconMs1TsvFile(string filePath)
        {
            FilePath = filePath;
            using var csv = new CsvReader(new StreamReader(FilePath), FlashDeconMs1TsvEntry.CsvConfiguration);
            Entries = csv.GetRecords<FlashDeconMs1TsvEntry>().ToList();
        }

        #region Utility Methods

        public static TargetDecoyCurve GetTargetDecoyCurve(List<FlashDeconMs1TsvFile> featureFiles, double qValueBinSize)
        {
            TargetDecoyCurve curve = new(qValueBinSize);
            foreach (var file in featureFiles)
            {
                curve.AddCurve(GetTargetDecoyCurve(file, qValueBinSize));
            }

            return curve;
        }

        public static TargetDecoyCurve GetTargetDecoyCurve(FlashDeconMs1TsvFile featureFile, double qValueBinSize)
        {
            TargetDecoyCurve curve = new(qValueBinSize);
            var targetsQ = featureFile.Entries.Where(p => p.Decoy == 0)
                .Select(p => p.QScore).ToList();
            var decoysQ = featureFile.Entries.Where(p => p.Decoy != 0)
                .Select(p => p.QScore).ToList();
            curve.AddTargets(targetsQ);
            curve.AddDecoys(decoysQ);

            return curve;
        }

        #endregion

    }

    public class TargetDecoyCurve
    {
        private double qValueBinSize;

        internal class TargetDecoyBin
        {
            internal double Qmin;
            internal double Qmax;
            internal int TargetCount { get; set; }
            internal int DecoyCount;
        }

        private List<TargetDecoyBin> targetDecoyCurve;

        public TargetDecoyCurve(double qValueBinSize)
        {
            this.qValueBinSize = qValueBinSize;
            targetDecoyCurve = new();

            for (double i = 0; i < 1; i+=qValueBinSize)
            {
                targetDecoyCurve.Add(new()
                    { Qmin = i, Qmax = i + this.qValueBinSize, DecoyCount = 0, TargetCount = 0 });
            }
        }

        #region Add Targets and Decoys

        public void AddCurve(TargetDecoyCurve curveToAdd)
        {
            if (Math.Abs(qValueBinSize - curveToAdd.qValueBinSize) > 0.0001 && targetDecoyCurve.Count != curveToAdd.targetDecoyCurve.Count)
                throw new Exception("Bin Sizes Must Be Equal To Add Curves");

            for (int i = 0; i < targetDecoyCurve.Count; i++)
            {
                var targetDecoyBin = targetDecoyCurve[i];
                targetDecoyBin.TargetCount += curveToAdd.targetDecoyCurve[i].TargetCount;
                targetDecoyBin.DecoyCount += curveToAdd.targetDecoyCurve[i].DecoyCount;
            }
        }

        public void AddTargets(List<double> targetQValues)
        {
            targetQValues.ForEach(p => AddValueToCurve(p, true));
        }

        public void AddDecoys(List<double> decoyQValues)
        {
            decoyQValues.ForEach(p => AddValueToCurve(p, false));
        }

        private void AddValueToCurve(double targetQValue, bool isTarget)
        {
            var binOfInterest = targetDecoyCurve.First(p => p.Qmin <= targetQValue && p.Qmax > targetQValue);
            if (isTarget)
                binOfInterest.TargetCount++;
            else
                binOfInterest.DecoyCount++;
        }

        #endregion

        #region Export Options

        public void ExportAsCsv(string outpath)
        {
            using var sw = new StreamWriter(File.Create(outpath));
            sw.WriteLine("QMin,QMax,Targets,Decoys");
            foreach (var bin in targetDecoyCurve)
            {
                sw.WriteLine($"{bin.Qmin},{bin.Qmax},{bin.TargetCount},{bin.DecoyCount}");
            }
        }

        #endregion

    }

    public class FlashDeconMs1TsvEntry
    {
        [Ignore]
        public static CsvConfiguration CsvConfiguration => new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Encoding = Encoding.UTF8,
            HasHeaderRecord = true,
            Delimiter = "\t",
        };

        [Index(0)]
        public double Index { get; set; }
        [Index(1)]
        public string FileName { get; set; }
        [Index(2)]
        public double ScanNum { get; set; }
        [Index(3)]
        public double Decoy { get; set; }
        [Index(4)]
        public double RetentionTime { get; set; }
        [Index(5)]
        public double MassCountInSpec { get; set; }
        [Index(6)]
        public double AverageMass { get; set; }
        [Index(7)]
        public double MonoisotopicMass { get; set; }
        [Index(8)]
        public double SumIntensity { get; set; }
        [Index(9)]
        public double MinCharge { get; set; }
        [Index(10)]
        public double MaxCharge { get; set; }
        [Index(11)]
        public double PeakCount { get; set; }
        [Index(12)]
        public double IsotopeCosine { get; set; }
        [Index(13)]
        public double ChargeScore { get; set; }
        [Index(14)]
        public double MassSNR { get; set; }
        [Index(15)]
        public double ChargeSNR { get; set; }
        [Index(16)]
        public double RepresentativeCharge { get; set; }
        [Index(17)]
        public double RepresentativeMzStart { get; set; }
        [Index(18)]
        public double RepresentativeMzEnd { get; set; }
        [Index(19)]
        public double QScore { get; set; }
        [Index(20)]
        public double Qvalue { get; set; }
        [Index(21)]
        public double QvalueWithIsotopeDecoyOnly { get; set; }
        [Index(22)]
        public double QvalueWithNoiseDecoyOnly { get; set; }
        [Index(23)]
        public double QvalueWithChargeDecoyOnly { get; set; }
        [Index(24)]
        public string PerChargeIntensity { get; set; }
        [Index(25)]
        public string PerIsotopeIntensity { get; set; }
    }
}

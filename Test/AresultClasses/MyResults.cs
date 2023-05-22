using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

namespace Test
{
    public class MyResults
    {
        public string FilePath { get; }
        public List<MyResultEntry> Entries { get; }

        public MyResults(List<MyResultEntry> entries)
        {
            Entries = entries;
        }

        /// <summary>
        /// read in from completed
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="completeOnly"></param>
        public MyResults(string filePath, double completeOnly = 2)
        {
            FilePath = filePath;
            using var csv = new CsvReader(new StreamReader(FilePath), MyResultEntry.CsvConfiguration);
            Entries = csv.GetRecords<MyResultEntry>().ToList();
        }

        /// <summary>
        /// read in from mzlib output
        /// </summary>
        /// <param name="filePath"></param>
        public MyResults(string filePath)
        {
            FilePath = filePath;
            Entries = new();

            var lines = File.ReadAllLines(FilePath);
            for (int i = 1; i < lines.Length; i++)
            {
                Entries.Add(new MyResultEntry(lines[i]));
            }
        }
    }

    public class MyResultEntry
    {
        [Ignore]
        private Ms1FeatureFile _featureFile;
        [Ignore]
        private FlashDeconMs1TsvFile _flashDeconFile;

        [Ignore]
        public static CsvConfiguration CsvConfiguration => new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Encoding = Encoding.UTF8,
            HasHeaderRecord = true,
            Delimiter = ","
        };

        public string DirPath { get; }
        public string Dataset { get; }
        public string Rep { get; }
        public string Fraction { get; }
        public string Processing { get; }
        public string DeconSoftware { get; }
        public double MsAlignMassCount { get; }
        public double FeatureCount => _featureFile.FeatureCount;
        public double ArtifactCount => _featureFile.ArtifactCount;
        public double HighHarmonicCount => _featureFile.HighHarmonicCount;
        public double LowHarmonicCount => _featureFile.LowHarmonicCount;
        public double IsotopologueCount => _featureFile.IsotopologueCount;
        public double OffByOneCount => _featureFile.ChargeOffByOneCount;
        public double ValidCount => _featureFile.FeatureCount - _featureFile.ArtifactCount;
        public double AllPeakCount => _flashDeconFile.Entries
            .Average(p => p.PeakCount);
        public double AllPeakMassSNR => _flashDeconFile.Entries
            .Average(p => p.MassSNR);
        public double AllPeakChargeSNR => _flashDeconFile.Entries
            .Average(p => p.ChargeSNR);
        public double TargetsPeakCount => _flashDeconFile.Entries
            .Where(p => p.Decoy == 0)
            .Average(p => p.PeakCount);
        public double TargetsPeakMassSNR => _flashDeconFile.Entries
            .Where(p => p.Decoy == 0)
            .Average(p => p.MassSNR);
        public double TargetsPeakChargeSNR => _flashDeconFile.Entries
            .Where(p => p.Decoy == 0)
            .Average(p => p.ChargeSNR);
        public double FilteredTargetsPeakCount => _flashDeconFile.Entries
            .Where(p => p.Decoy == 0 && p.Qvalue <= 0.01)
            .Average(p => p.PeakCount);
        public double FilteredTargetsPeakMassSNR => _flashDeconFile.Entries
            .Where(p => p.Decoy == 0 && p.Qvalue <= 0.01)
            .Average(p => p.MassSNR);
        public double FilteredTargetsPeakChargeSNR => _flashDeconFile.Entries
            .Where(p => p.Decoy == 0 && p.Qvalue <= 0.01)
            .Average(p => p.ChargeSNR);

        public MyResultEntry(string entryLine)
        {
            var splits = entryLine.Split(',');
            DirPath = splits[0];
            Dataset = splits[1];
            Rep = splits[2];
            Fraction = splits[3];
            Processing = splits[4];
            DeconSoftware = splits[5];
            MsAlignMassCount = double.Parse(splits[6]);
        }

        public void AddFeatureFile(Ms1FeatureFile featureFile)
        {
            if (featureFile.Features.Any(p => !p.PerformedArtifactDetection))
                MsFeature.PerformArtifactDetection(featureFile.Features);
            _featureFile = featureFile;
        }

        public void AddFlashDeconMs1Tsv(FlashDeconMs1TsvFile ms1TsvFile)
        {
            _flashDeconFile = ms1TsvFile;
        }




    }
}

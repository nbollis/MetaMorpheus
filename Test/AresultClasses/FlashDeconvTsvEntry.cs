using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper.Configuration.Attributes;

namespace Test
{
    public class FlashDeconTsvFile
    {
        public string FilePath { get; }
        public List<FlashDeconvTsvEntry> Entries { get; }

        public FlashDeconTsvFile(string filePath)
        {
            FilePath = filePath;
            using var csv = new CsvReader(new StreamReader(FilePath), FlashDeconvTsvEntry.CsvConfiguration);
            Entries = csv.GetRecords<FlashDeconvTsvEntry>().ToList();
        }
    }

    public class FlashDeconvTsvEntry
    {
        [Ignore]
        public static CsvConfiguration CsvConfiguration => new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Encoding = Encoding.UTF8,
            HasHeaderRecord = true,
            Delimiter = "\t",
        };
        public int FeatureIndex { get; set; }
        public string FileName { get; set; }
        public double MonoisotopicMass { get; set; }
        public double AverageMass { get; set; }
        public int MassCount { get; set; }
        public double StartRetentionTime { get; set; }
        public double EndRetentionTime { get; set; }
        public double RetentionTimeDuration { get; set; }
        public double ApexRetentionTime { get; set; }
        public double SumIntensity { get; set; }
        public double MaxIntensity { get; set; }
        public double FeatureQuantity { get; set; }
        public int MinCharge { get; set; }
        public int MaxCharge { get; set; }
        public int ChargeCount { get; set; }
        public double IsotopeCosineScore { get; set; }
        public double MaxQScore { get; set; }
        public double PerChargeIntensity { get; set;}
        public double PerIsotopeIntensity { get; set; }
    }
}

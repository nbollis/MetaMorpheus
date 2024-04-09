using CsvHelper;
using CsvHelper.Configuration;
using Readers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper.Configuration.Attributes;

namespace Test.ChimeraPaper.ResultFiles
{
    public class BulkResultCountComparison
    {

        public static CsvConfiguration CsvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = ",",
            BadDataFound = null,
            MissingFieldFound = null
        };

        public string DatasetName { get; set; }
        [Optional] public string FileName { get; set; }
        public string Condition { get; set; }
        public int PsmCount { get; set; }
        public int PeptideCount { get; set; }
        public int ProteinGroupCount { get; set; }
        public int OnePercentPsmCount { get; set; }
        public int OnePercentPeptideCount { get; set; }
        public int OnePercentProteinGroupCount { get; set; }
        [Optional] public int OnePercentUnambiguousPsmCount { get; set; }
        [Optional] public int OnePercentUnambiguousPeptideCount { get; set; }
    }


    public class BulkResultCountComparisonFile : ResultFile<BulkResultCountComparison>, IResultFile
    {
        public override void LoadResults()
        {
            using (var csv = new CsvReader(new StreamReader(FilePath), BulkResultCountComparison.CsvConfig))
            {
                Results = csv.GetRecords<BulkResultCountComparison>().ToList();
            }
        }

        public override void WriteResults(string outputPath)
        {
            using var csv = new CsvWriter(new StreamWriter(outputPath), BulkResultCountComparison.CsvConfig);

            csv.WriteHeader<BulkResultCountComparison>();
            foreach (var result in Results)
            {
                csv.NextRecord();
                csv.WriteRecord(result);
            }
        }

        public override SupportedFileType FileType { get; }
        public override Software Software { get; set; }
    }
}

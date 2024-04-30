using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper.Configuration.Attributes;
using Readers;

namespace Test.ChimeraPaper.ResultFiles
{
    public enum ChimeraBreakdownType
    {
        Psm,
        Peptide,
    }

    public class ChimeraBreakdownRecord
    {
        public static CsvConfiguration CsvConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
            HasHeaderRecord = true,

        };

        // identifiers
        public string Dataset { get; set; }
        public string FileName { get; set; }
        public string Condition { get; set; }
        public int Ms2ScanNumber { get; set; }
        public ChimeraBreakdownType Type { get; set; }

        // results
        [Optional] public double IsolationMz { get; set; } = -1;
        public int IdsPerSpectra { get; set; }
        public int Parent { get; set; } = 1;
        public int UniqueForms { get; set; }
        public int UniqueProteins { get; set; }
        [Optional] public int TargetCount { get; set; }
        [Optional] public int DecoyCount { get; set; }

        public ChimeraBreakdownRecord()
        {
        }
    }

    public class ChimeraBreakdownFile : ResultFile<ChimeraBreakdownRecord>, IResultFile
    {

        public ChimeraBreakdownFile(string filePath) : base(filePath)
        {
        }

        public ChimeraBreakdownFile()
        {
        }

        public override void LoadResults()
        {
            using var csv = new CsvHelper.CsvReader(new System.IO.StreamReader(FilePath), ChimeraBreakdownRecord.CsvConfiguration);
            Results = csv.GetRecords<ChimeraBreakdownRecord>().ToList();
        }

        public override void WriteResults(string outputPath)
        {
            using var csv = new CsvHelper.CsvWriter(new System.IO.StreamWriter(outputPath), ChimeraBreakdownRecord.CsvConfiguration);

            csv.WriteHeader<ChimeraBreakdownRecord>();
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

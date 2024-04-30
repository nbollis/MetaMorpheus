using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Readers;

namespace Test.ChimeraPaper.ResultFiles
{
    public class PSPDInputFileRecord
    {
        public static CsvConfiguration CsvConfiguration => new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
        {
            Delimiter = "\t",
            HasHeaderRecord = true,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim,
            BadDataFound = null,
            MissingFieldFound = null,
        };

        [Name("File ID")]
        public string FileID { get; set; }

        [Name("File Name")]
        public string FileName { get; set; }

        [Name("Creation Date")]
        public DateTime CreationDate { get; set; }

        [Name("Instrument Name")]
        public string InstrumentName { get; set; }

        [Name("Software Revision")]
        public string SoftwareRevision { get; set; }

        [Name("Max. Mass [Da]")]
        public double MaxMass { get; set; }

    }

    public class PSPDInputFileFile : ResultFile<PSPDInputFileRecord>, IResultFile
    {
        public PSPDInputFileFile(string filePath) : base(filePath)
        {
        }

        public PSPDInputFileFile()
        {
        }
        public override SupportedFileType FileType => SupportedFileType.Tsv_FlashDeconv;
        public override Software Software { get; set; }

        public override void LoadResults()
        {
            using var csv = new CsvReader(new StreamReader(FilePath), PSPDInputFileRecord.CsvConfiguration);
            Results = csv.GetRecords<PSPDInputFileRecord>().ToList();
        }

        public override void WriteResults(string outputPath)
        {
            using var csv = new CsvWriter(new StreamWriter(File.Create(outputPath)), PSPDInputFileRecord.CsvConfiguration);

            csv.WriteHeader<PSPDInputFileRecord>();
            foreach (var result in Results)
            {
                csv.NextRecord();
                csv.WriteRecord(result);
            }
        }
    }
}

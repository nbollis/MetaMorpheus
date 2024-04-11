using System;
using System.Collections.Generic;
using System.Globalization;
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
    public class ChimeraCountingResult
    {
        public static CsvConfiguration CsvConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
            HasHeaderRecord = true,
            
        };

        [Name("Dataset")]
        public string Dataset { get; set; }

        [Name("Software")]
        public string Software { get; set; }

        [Name("IdsPerSpectra")]
        public int IdsPerSpectra { get; set; }

        [Name("Count")]
        public int IdCount { get; set; }

        [Name("1% FDR Count")]
        public int OnePercentIdCount { get; set; }



        public ChimeraCountingResult(int idsPerSpectra, int idCount, int onePercentIdCount, string dataset, string software)
        {
            IdsPerSpectra = idsPerSpectra;
            IdCount = idCount;
            OnePercentIdCount = onePercentIdCount;
            Software = software;
            Dataset = dataset;
        }

        public ChimeraCountingResult()
        {
        }
    }

    public class ChimeraCountingFile : ResultFile<ChimeraCountingResult>, IResultFile
    {
        public override void LoadResults()
        {
            using var csv = new CsvReader(new StreamReader(FilePath), ChimeraCountingResult.CsvConfiguration);
            Results = csv.GetRecords<ChimeraCountingResult>().ToList();
        }

        public override void WriteResults(string outputPath)
        {
            using var csv = new CsvWriter(new StreamWriter(outputPath), ChimeraCountingResult.CsvConfiguration);

            csv.WriteHeader<ChimeraCountingResult>();
            foreach (var result in Results)
            {
                csv.NextRecord();
                csv.WriteRecord(result);
            }
        }

        public ChimeraCountingFile(string filePath) : base(filePath, Software.Unspecified) { }
        public ChimeraCountingFile() : base() { }

        public override SupportedFileType FileType { get; }
        public override Software Software { get; set; }
    }
}

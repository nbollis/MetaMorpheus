using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using Readers;

namespace Test.ChimeraPaper
{

    public class ChimeraCountingResult
    {

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

        

        public ChimeraCountingResult(int idsPerSpectra, int idCount, int onePercentIdCount,string dataset, string software)
        {
            IdsPerSpectra = idsPerSpectra;
            IdCount = idCount;
            OnePercentIdCount = onePercentIdCount;
            Software = software;
            Dataset = dataset;
        }
    }

    public class ChimeraCountingFile : ResultFile<ChimeraCountingResult>, IResultFile
    {
        public override void LoadResults()
        {
            using var csv = new CsvReader(new StreamReader(FilePath), new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture));
            Results = csv.GetRecords<ChimeraCountingResult>().ToList();
        }

        public override void WriteResults(string outputPath)
        {
            using var csv = new CsvWriter(new StreamWriter(outputPath), new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture));

            csv.WriteHeader<ChimeraCountingResult>();
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

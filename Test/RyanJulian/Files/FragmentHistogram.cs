using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Readers;

namespace Test.RyanJulian
{ 
    public class FragmentHistogramRecord
    {
        public static CsvConfiguration CsvConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            MissingFieldFound = null,
            Delimiter = ",",
            HasHeaderRecord = true,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim,
            BadDataFound = null
        };

        public string Species { get; set; }
        public int NumberOfMods { get; set; }
        public int MaxFragments { get; set; }
        public string AnalysisType { get; set; }
        public int AmbiguityLevel { get; set; }

        public int FragmentCount { get; set; }
        public int ProteinCount { get; set; }
    }

    public class FragmentHistogramFile : ResultFile<FragmentHistogramRecord>, IResultFile
    {
        public FragmentHistogramFile() : base() { }
        public FragmentHistogramFile(string filePath) : base(filePath) { }

        public override SupportedFileType FileType { get; }
        public override Software Software { get; set; }

        public override void LoadResults()
        {
            using (var csv = new CsvReader(new StreamReader(FilePath), FragmentHistogramRecord.CsvConfiguration))
            {
                Results = csv.GetRecords<FragmentHistogramRecord>().ToList();
            }
        }

        public override void WriteResults(string outputPath)
        {
            var csv = new CsvWriter(new StreamWriter(outputPath), FragmentHistogramRecord.CsvConfiguration);

            csv.WriteHeader<FragmentHistogramRecord>();
            foreach (var result in Results)
            {
                csv.NextRecord();
                csv.WriteRecord(result);
            }

            csv.Dispose();
        }
    }
}

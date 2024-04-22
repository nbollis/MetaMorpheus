using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Readers;

namespace Test.RyanJulian;
public class FragmentsToDistinguishRecord
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


    public string Accession { get; set; }
    public int NumberInPrecursorGroup { get; set; }
    public int FragmentsAvailable { get; set; }
    public int FragmentCountNeededToDifferentiate { get; set; }
}
public class FragmentsToDistinguishFile : ResultFile<FragmentsToDistinguishRecord>, IResultFile
{
    public FragmentsToDistinguishFile() : base() { }
    public FragmentsToDistinguishFile(string filePath) : base(filePath) { }

    public override SupportedFileType FileType { get; }
    public override Software Software { get; set; }
    public override void LoadResults()
    {
        using (var csv = new CsvReader(new StreamReader(FilePath), FragmentsToDistinguishRecord.CsvConfiguration))
        {
            Results = csv.GetRecords<FragmentsToDistinguishRecord>().ToList();
        }
    }

    public override void WriteResults(string outputPath)
    {
        var csv = new CsvWriter(new StreamWriter(outputPath), FragmentsToDistinguishRecord.CsvConfiguration);

        csv.WriteHeader<FragmentsToDistinguishRecord>();
        foreach (var result in Results)
        {
            csv.NextRecord();
            csv.WriteRecord(result);
        }

        csv.Dispose();
    }
}
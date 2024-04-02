using System.IO;
using System.Linq;
using CsvHelper;
using Readers;

namespace Test.RyanJulain;

public class PrecursorFragmentMassFile : ResultFile<PrecursorFragmentMassSet>, IResultFile
{
    public override void LoadResults()
    {
        var csv = new CsvReader(new StreamReader(FilePath), PrecursorFragmentMassSet.CsvConfiguration);
        Results = csv.GetRecords<PrecursorFragmentMassSet>().ToList();
    }

    public override void WriteResults(string outputPath)
    {
        var csv = new CsvWriter(new StreamWriter(outputPath), PrecursorFragmentMassSet.CsvConfiguration);

        csv.WriteHeader<PrecursorFragmentMassSet>();
        foreach (var result in Results)
        {
            csv.NextRecord();
            csv.WriteRecord(result);
        }

        csv.Dispose();
    }

    public override SupportedFileType FileType { get; }
    public override Software Software { get; set; }
}
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Readers;
using System.IO;
using System.Linq;

namespace Test.ChimeraPaper.ResultFiles;

public class PSPDProteinRecord
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

    [Name("Accession")]
    public string Accession { get; set; }

    [Name("Description")]
    public string Description { get; set; }

    [Name("# of Isoforms")]
    public int IsoformCount { get; set; }

    [Name("# of Isoforms with Characterized Proteoforms")]
    public int IsoformsWithCharacterizedProteoforms { get; set; }

    [Name("# of Proteoforms")]
    public int ProteoformCount { get; set; }

    [Name("Proteoform Characterization Confidence")]
    public string ProteoformCharacterizationConfidence { get; set; }

    [Name("# Characterized Proteoforms")]
    public int CharacterizedProteoformCount { get; set; }

    [Name("# of PrSMs")]
    public int PrsmCount { get; set; }

    [Name("Q-value")]
    public double QValue { get; set; }
}

public class PSPSProteinFile : ResultFile<PSPDProteinRecord>, IResultFile
{
    public PSPSProteinFile(string filePath) : base(filePath)
    {
    }

    public PSPSProteinFile()
    {
    }
    public override SupportedFileType FileType => SupportedFileType.Tsv_FlashDeconv;
    public override Software Software { get; set; }

    public override void LoadResults()
    {
        using var csv = new CsvReader(new StreamReader(FilePath), PSPDProteinRecord.CsvConfiguration);
        Results = csv.GetRecords<PSPDProteinRecord>().ToList();
    }

    public override void WriteResults(string outputPath)
    {
        using var csv = new CsvWriter(new StreamWriter(File.Create(outputPath)), PSPDProteinRecord.CsvConfiguration);

        csv.WriteHeader<PSPDProteinRecord>();
        foreach (var result in Results)
        {
            csv.NextRecord();
            csv.WriteRecord(result);
        }
    }
}

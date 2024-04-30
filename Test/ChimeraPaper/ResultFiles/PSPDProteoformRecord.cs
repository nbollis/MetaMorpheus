using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Readers;
using System.IO;
using System.Linq;

namespace Test.ChimeraPaper.ResultFiles;

public class PSPDProteoformRecord
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

    [Name("Proteoform Characterization Confidence")]
    public string ProteoformCharacterizationConfidence { get; set; }

    [Name("Protein Description")]
    public string ProteinDescription { get; set; }

    [Name("Confidence")]
    public string Confidence { get; set; }

    [Name("Sequence")]
    public string Sequence { get; set; }

    [Name("# PrSMs")]
    public int PrsmCount { get; set; }

    [Name("Protein Accessions")]
    public string ProteinAccessions { get; set; }

    [Name("Theo. Mass [Da]")]
    public double TheoreticalMass { get; set; }

    [Name("Best PrSM C-Score")]
    public double BestPrsmCScore { get; set; }

    [Name("Average PrSM Detected Neutral Mass")]
    public double AveragePrsmDetectedNeutralMass { get; set; }

    [Name("Q-value")]
    public double QValue { get; set; }

    [Name("Modifications")]
    public string Modifications { get; set; }

    [Name("Proforma")]
    public string Proforma { get; set; }

    [Name("% Residue Cleavages")]
    public double PercentResidueCleavages { get; set; }
}

public class PSPSProteoformFile : ResultFile<PSPDProteoformRecord>, IResultFile
{
    public PSPSProteoformFile(string filePath) : base(filePath)
    {
    }

    public PSPSProteoformFile()
    {
    }
    public override SupportedFileType FileType => SupportedFileType.Tsv_FlashDeconv;
    public override Software Software { get; set; }

    public override void LoadResults()
    {
        using var csv = new CsvReader(new StreamReader(FilePath), PSPDProteoformRecord.CsvConfiguration);
        Results = csv.GetRecords<PSPDProteoformRecord>().ToList();
    }

    public override void WriteResults(string outputPath)
    {
        using var csv = new CsvWriter(new StreamWriter(File.Create(outputPath)), PSPDProteoformRecord.CsvConfiguration);

        csv.WriteHeader<PSPDProteoformRecord>();
        foreach (var result in Results)
        {
            csv.NextRecord();
            csv.WriteRecord(result);
        }
    }
}
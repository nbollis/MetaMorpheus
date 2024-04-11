using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using Test.ChimeraPaper.ResultFiles.Converters;

namespace Test.ChimeraPaper.ResultFiles
{
    public class MsFraggerProtein
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
      
        [Name("Protein")]
        public string Protein { get; set; }

        [Name("Protein ID")]
        public string Accesion { get; set; }

        [Name("Entry Name")]
        public string AccessionOrganism { get; set; }

        [Name("Gene")]
        public string Gene { get; set; }

        [Name("Protein Length")]
        public int Length { get; set; }

        [Name("Organism")]
        public string Organism { get; set; }

        [Name("Protein Existence")]
        public string ProteinExistence { get; set; }

        [Name("Description")]
        public string Description { get; set; }

        public double Coverage { get; set; }

        [Name("Protein Probability")]
        public double ProteinProbability { get; set; }

        [Name("Total Peptides")]
        public int TotalPeptides { get; set; }

        [Name("Unique Peptides")]
        public int UniquePeptides { get; set; }

        [Name("Razor Peptides")]
        public int RazorPeptides { get; set; }

        [Name("Total Spectral Count")]
        public int TotalSpectralCount { get; set; }

        [Name("Unique Spectral Count")]
        public int UniqueSpectralCount { get; set; }

        [Name("Razor Spectral Count")]
        public int RazorSpectralCount { get; set; }

        [Name("Total Intensity")]
        public double TotalIntensity { get; set; }

        [Name("Unique Intensity")]
        public double UniqueIntensity { get; set; }

        [Name("Razor Intensity")]
        public double RazorIntensity { get; set; }

        [Name("Razor Assigned Modifications")]
        [TypeConverter(typeof(CommaDelimitedToStringArrayTypeConverter))]
        public string[] RazorAssignedModifications { get; set; }

        [Name("Razor Observed Modifications")]
        [TypeConverter(typeof(CommaDelimitedToStringArrayTypeConverter))]
        public string[] RazorObservedModifications { get; set; }

        [Name("Indistinguishable Proteins")]
        [TypeConverter(typeof(CommaDelimitedToStringArrayTypeConverter))]
        public string[] IndistinguishableProteins { get; set; }


        public MsFraggerProtein()
        {
            RazorAssignedModifications = new string[0];
            RazorObservedModifications = new string[0];
            IndistinguishableProteins = new string[0];
        }
    }

    public class MsFraggerProteinFile : ResultFile<MsFraggerProtein>, IResultFile
    {
        public MsFraggerProteinFile(string filePath) : base(filePath)
        {
        }

        public override SupportedFileType FileType => SupportedFileType.Tsv_FlashDeconv;
        public override Software Software { get; set; }

        public override void LoadResults()
        {
            var csv = new CsvReader(new StreamReader(FilePath), MsFraggerProtein.CsvConfiguration);
            Results = csv.GetRecords<MsFraggerProtein>().ToList();
        }

        public override void WriteResults(string outputPath)
        {
            if (!CanRead(outputPath))
                outputPath += FileType.GetFileExtension();

            using var csv = new CsvWriter(new StreamWriter(File.Create(outputPath)), MsFraggerProtein.CsvConfiguration);

            csv.WriteHeader<MsFraggerProtein>();
            foreach (var result in Results)
            {
                csv.NextRecord();
                csv.WriteRecord(result);
            }
        }
    }

}

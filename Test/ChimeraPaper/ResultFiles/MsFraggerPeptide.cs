using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Proteomics.ProteolyticDigestion;
using Test.ChimeraPaper.ResultFiles.Converters;

namespace Test.ChimeraPaper.ResultFiles
{
    public class MsFraggerPeptide
    {
        public static CsvConfiguration CsvConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = "\t",
            HasHeaderRecord = true,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim,
            BadDataFound = null,
            MissingFieldFound = null,
        };

        [Name("Peptide")]
        public string BaseSequence { get; set; }

        [Name("Prev AA")]
        public char PreviousAminoAcid { get; set; }

        [Name("Next AA")]
        public char NextAminoAcid { get; set; }

        [Name("Peptide Length")]
        public int PeptideLength { get; set; }

        [Name("Protein Start")]
        public int OneBasedStartResidueInProtein { get; set; }

        [Name("Protein End")]
        public int OneBasedEndResidueInProtein { get; set; }

        [Name("Charges")]
        [TypeConverter(typeof(CommaDelimitedToIntegerArrayTypeConverter))]
        public int[] Charge { get; set; }

        [Name("Probability")]
        public double Probability { get; set; }

        [Name("Spectral Count")]
        public int SpectralCount { get; set; }

        [Name("Intensity")]
        public double Intensity { get; set; }

        [Name("Assigned Modifications")]
        [TypeConverter(typeof(CommaDelimitedToStringArrayTypeConverter))]
        public string[] AssignedModifications { get; set; }

        [Name("Observed Modifications")]
        [TypeConverter(typeof(CommaDelimitedToStringArrayTypeConverter))]
        public string[] ObservedModifications { get; set; }

        [Name("Protein")]
        public string Protein { get; set; }

        [Name("Protein ID")]
        public string ProteinAccession { get; set; }

        [Name("Entry Name")]
        public string ProteinName { get; set; }

        [Name("Gene")]
        public string Gene { get; set; }

        [Name("Protein Description")]
        public string ProteinDescription { get; set; }

        [Name("Mapped Genes")]
        [TypeConverter(typeof(CommaDelimitedToStringArrayTypeConverter))]
        public string[] MappedGenes { get; set; }

        [Name("Mapped Proteins")]
        [TypeConverter(typeof(CommaDelimitedToStringArrayTypeConverter))]
        public string[] MappedProteins { get; set; }
    }
}

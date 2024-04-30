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
using Test.ChimeraPaper.ResultFiles.Converters;

namespace Test.ChimeraPaper.ResultFiles
{
    public class PSPDPrSMRecord
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

        [Name("Confidence")]
        public string Confidence { get; set; }

        [Name("Detected Ion Count")]
        public int DetectedIonCount { get; set; }

        [Name("Proteoform Level")]
        public int ProteoformLevel { get; set; }

        [Name("PTMs Localized")]
        public string PTMsLocalized { get; set; }

        [Name("PTMs Identified")]
        public string PTMsIdentified { get; set; }

        [Name("Sequence Defined")]
        public string SequenceDefined { get; set; }

        [Name("Gene Identified")]
        public string GeneIdentified { get; set; }

        [Name("Identifying Node")]
        public string IdentifyingNode { get; set; }

        [Name("Annotated Sequence")]
        public string AnnotatedSequence { get; set; }

        [Name("Modifications")]
        public string Modifications { get; set; }

        [Name("# Proteins")]
        public int ProteinCount { get; set; }

        [Name("Protein Accessions")]
        public string ProteinAccessions { get; set; }

        [Name("Original Precursor Charge")]
        public int Charge { get; set; }

        [Name("Rank")]
        public int Rank { get; set; }

        [Name("Search Engine Rank")]
        public int SearchEngineRank { get; set; }

        [Name("m/z [Da]")]
        public double Mz { get; set; }

        [Name("Mass [Da]")]
        public double Mass { get; set; }

        [Name("Theo. Mass [Da]")]
        public double TheoreticalMass { get; set; }

        [Name("DeltaMass [ppm]")]
        public double DeltaMassPpm { get; set; }

        [Name("DeltaMass [Da]")]
        public double DeltaMassDa { get; set; }

        [Name("Matched Ions")]
        public string MatchedIons { get; set; }

        [Name("Activation Type")]
        public string ActivationType { get; set; }

        [Name("NCE [%]")]
        public double NCE { get; set; }

        [Name("MS Order")]
        [TypeConverter(typeof(PSPDMsOrderConverter))]
        public int MSOrder { get; set; }

        [Name("Ion Inject Time [ms]")]
        public double IonInjectTime { get; set; }

        [Name("RT [min]")]
        public double RT { get; set; }

        [Name("Fragmentation Scan(s)")]
        public string Ms2ScanNumber { get; set; }

        [Name("# Fragmentation Scans")]
        public int FragmentationScans { get; set; }

        [Name("# Precursor Scans")]
        public int PrecursorScans { get; set; }

        [Name("File ID")]
        public string FileID { get; set; }

        [Name("-Log P-Score")]
        public double NegativeLogPScore { get; set; }

        [Name("-Log E-Value")]
        public double NegativeLogEValue { get; set; }

        [Name("C-Score")]
        public double CScore { get; set; }

        [Name("Q-value")]
        public double QValue { get; set; }

        [Name("% Residue Cleavages")]
        public double PercentResidueCleavages { get; set; }

        [Name("Corrected Delta Mass (Da)")]
        public double CorrectedDeltaMassDa { get; set; }

        [Name("Corrected Delta Mass (ppm)")]
        public double CorrectedDeltaMassPpm { get; set; }

        [Name("Compensation Voltage")]
        public double CompensationVoltage { get; set; }
    }

    public class PSPSPrSMFile : ResultFile<PSPDPrSMRecord>, IResultFile
    {
        public PSPSPrSMFile(string filePath) : base(filePath)
        {
        }

        public PSPSPrSMFile()
        {
        }
        public override SupportedFileType FileType => SupportedFileType.Tsv_FlashDeconv;
        public override Software Software { get; set; }

        public override void LoadResults()
        {
            using var csv = new CsvReader(new StreamReader(FilePath), PSPDPrSMRecord.CsvConfiguration);
            Results = csv.GetRecords<PSPDPrSMRecord>().ToList();
        }

        public override void WriteResults(string outputPath)
        {
            using var csv = new CsvWriter(new StreamWriter(File.Create(outputPath)), PSPDPrSMRecord.CsvConfiguration);

            csv.WriteHeader<PSPDPrSMRecord>();
            foreach (var result in Results)
            {
                csv.NextRecord();
                csv.WriteRecord(result);
            }
        }
    }
}

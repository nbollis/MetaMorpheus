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

namespace Test.ChimeraPaper.ResultFiles
{
    public class RetentionTimePredictionEntry
    {
        public static CsvConfiguration CsvConfiguration = new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
        {
            Delimiter = "\t",
            HasHeaderRecord = true,
            IgnoreBlankLines = true,
            TrimOptions = CsvHelper.Configuration.TrimOptions.Trim,
            BadDataFound = null,
            HeaderValidated = null,
            MissingFieldFound = null,
            ReadingExceptionOccurred = null,
            ShouldSkipRecord = (record) => false,
        };

        public string FileNameWithoutExtension { get; set; }
        public double ScanNumber { get; set; }
        public double PrecursorScanNumber { get; set; }
        public bool IsChimeric { get; set; }
        public double RetentionTime { get; set; }
        public string BaseSequence { get; set; }
        public string FullSequence { get; set; }
        public double QValue { get; set; }
        public double PEP_QValue { get; set; }
        public double PEP { get; set; }
        public double SpectralAngle { get; set; }
        public double SSRCalcPrediction { get; set; }
        [Optional] public double ChronologerPrediction { get; set; }
        public string PeptideModSeq { get; set; }

        public RetentionTimePredictionEntry(string fileNameWithoutExtension, double scanNumber,
            double precursorScanNumber, double retentionTime, string baseSequence, string fullSequence, string peptideModSeq,
            double qValue, double pepQValue, double pep, double spectralAngle, bool isChimeric)
        {
            FileNameWithoutExtension = fileNameWithoutExtension;
            ScanNumber = scanNumber;
            PrecursorScanNumber = precursorScanNumber;
            RetentionTime = retentionTime;
            BaseSequence = baseSequence;
            FullSequence = fullSequence;
            PeptideModSeq = peptideModSeq;
            QValue = qValue;
            PEP_QValue = pepQValue;
            IsChimeric = isChimeric;
            PEP = pep;
            SpectralAngle = spectralAngle;
        }

        public RetentionTimePredictionEntry()
        {
        }
    }


    public class RetentionTimePredictionFile : ResultFile<RetentionTimePredictionEntry>, IResultFile
    {
        public override void LoadResults()
        {
            using var csv = new CsvReader(new StreamReader(FilePath), RetentionTimePredictionEntry.CsvConfiguration);
            Results = csv.GetRecords<RetentionTimePredictionEntry>().ToList();
        }

        public override void WriteResults(string outputPath)
        {
            if (!CanRead(outputPath))
                outputPath += FileType.GetFileExtension();

            using var csv = new CsvWriter(new StreamWriter(File.Create(outputPath)), RetentionTimePredictionEntry.CsvConfiguration);

            csv.WriteHeader<RetentionTimePredictionEntry>();
            foreach (var result in Results)
            {
                csv.NextRecord();
                csv.WriteRecord(result);
            }
        }

        public override SupportedFileType FileType => SupportedFileType.Tsv_FlashDeconv;
        public override Software Software { get; set; }
    }
}

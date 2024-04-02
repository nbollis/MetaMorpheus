#nullable enable
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

namespace Test.ChimeraPaper
{
    internal class ChimeraSpecFileMap
    {
        public static CsvConfiguration CsvConfiguration = new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
        {
            Delimiter = "\t",
            HasHeaderRecord = true,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim,
            BadDataFound = null,
            MissingFieldFound = null,
        };


        public int OriginalMs1ScanNumber { get; set; }
        public int OriginalMs2ScanNumber { get; set; }
        public string OriginalPeptideBaseSequence { get; set; }
        public string OriginalPeptideFullSequence { get; set; }
        public string OriginalPeptideAccession { get; set; }


        public int NewMs1ScanNumber { get; set; }
        public int NewMs2ScanNumber { get; set; }
        [Optional] public string? NewPeptideBaseSequence { get; set; }
        [Optional] public string? NewPeptideFullSequence { get; set; }
        [Optional] public string? NewPeptideAccession { get; set; }

        public ChimeraSpecFileMap(int originalMs1ScanNumber, int originalMs2ScanNumber, int newMs1ScanNumber,
            int newMs2ScanNumber, string originalPeptideBaseSequence, string? newPeptideBaseSequence,
            string originalPeptideFullSequence, string? newPeptideFullSequence, string originalPeptideAccession,
            string? newPeptideAccession)
        {
            OriginalMs1ScanNumber = originalMs1ScanNumber;
            OriginalMs2ScanNumber = originalMs2ScanNumber;
            NewMs1ScanNumber = newMs1ScanNumber;
            NewMs2ScanNumber = newMs2ScanNumber;
            OriginalPeptideBaseSequence = originalPeptideBaseSequence;
            NewPeptideBaseSequence = newPeptideBaseSequence;
            OriginalPeptideFullSequence = originalPeptideFullSequence;
            NewPeptideFullSequence = newPeptideFullSequence;
            OriginalPeptideAccession = originalPeptideAccession;
            NewPeptideAccession = newPeptideAccession;
        }

        public ChimeraSpecFileMap()
        {
        }
    }

    internal class ChimeraSpecFileMapFile : ResultFile<ChimeraSpecFileMap>, IResultFile
    {
        public override void LoadResults()
        {
            var csv = new CsvHelper.CsvReader(new StreamReader(FilePath), ChimeraSpecFileMap.CsvConfiguration);
            Results = csv.GetRecords<ChimeraSpecFileMap>().ToList();
        }

        public override void WriteResults(string outputPath)
        {
            if (!CanRead(outputPath))
                outputPath += FileType.GetFileExtension();

            using var csv = new CsvWriter(new StreamWriter(File.Create(outputPath)), MsFraggerPsm.CsvConfiguration);

            csv.WriteHeader<ChimeraSpecFileMap>();
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

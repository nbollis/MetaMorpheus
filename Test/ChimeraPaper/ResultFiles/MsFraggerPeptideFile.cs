using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using Readers;

namespace Test.ChimeraPaper.ResultFiles
{
    public class MsFraggerPeptideFile : ResultFile<MsFraggerPeptide>, IResultFile
    {

        public MsFraggerPeptideFile(string filePath) : base(filePath, Software.Unspecified) { }

        /// <summary>
        /// Constructor used to initialize from the factory method
        /// </summary>
        public MsFraggerPeptideFile() : base() { }

        public override void LoadResults()
        {
            using var csv = new CsvReader(new StreamReader(FilePath), MsFraggerPeptide.CsvConfiguration);
            Results = csv.GetRecords<MsFraggerPeptide>().ToList();
        }

        public override void WriteResults(string outputPath)
        {
            if (!CanRead(outputPath))
                outputPath += FileType.GetFileExtension();

            using (var csv = new CsvWriter(new StreamWriter(File.Create(outputPath)),
                       MsFraggerPeptide.CsvConfiguration))
            {

                csv.WriteHeader<MsFraggerPeptide>();
                foreach (var result in Results)
                {
                    csv.NextRecord();
                    csv.WriteRecord(result);
                }
            }
        }

        public override SupportedFileType FileType => SupportedFileType.Tsv_FlashDeconv;
        public override Software Software { get; set; }
    }
    
}

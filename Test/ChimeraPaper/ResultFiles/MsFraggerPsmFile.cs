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
    public class MsFraggerPsmFile : ResultFile<MsFraggerPsm>, IResultFile
    {
        public override SupportedFileType FileType => SupportedFileType.Tsv_FlashDeconv;
        public override Software Software { get; set; }
        public MsFraggerPsmFile(string filePath) : base(filePath, Software.Unspecified) { }

        /// <summary>
        /// Constructor used to initialize from the factory method
        /// </summary>
        public MsFraggerPsmFile() : base() { }

        public override void LoadResults()
        {
            using var csv = new CsvReader(new StreamReader(FilePath), MsFraggerPsm.CsvConfiguration);
            Results = csv.GetRecords<MsFraggerPsm>().ToList();

            try
            {
                string dirName = Path.GetDirectoryName(FilePath);
                Results.ForEach(p => p.FileNameWithoutExtension = Path.GetFileNameWithoutExtension(dirName));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public override void WriteResults(string outputPath)
        {
            if (!CanRead(outputPath))
                outputPath += FileType.GetFileExtension();

            using ( var csv = new CsvWriter(new StreamWriter(File.Create(outputPath)), MsFraggerPsm.CsvConfiguration))
            {
                csv.WriteHeader<MsFraggerPsm>();
                foreach (var result in Results)
                {
                    csv.NextRecord();
                    csv.WriteRecord(result);
                }
            }
        }
    }
}

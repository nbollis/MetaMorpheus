using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using Easy.Common.Extensions;
using TaskLayer;

namespace GuiFunctions
{
    public interface ITsv
    {
        string TabSeparatedHeader { get; }
        string ToTsvString();
    }

    public static class TsvExtensions
    {
        public static string GetTsvString(this IEnumerable<ITsv> iTsvs)
        {
            var sb = new StringBuilder();

            sb.AppendLine(iTsvs.First().TabSeparatedHeader);
            foreach (var tsv in iTsvs)
            {
                sb.AppendLine(tsv.ToTsvString());
            }
            return sb.ToString();
        }

        public static void ExportAsTsv(this IEnumerable<ITsv> iTsvs, string outPath)
        {
            if (!outPath.EndsWith(".tsv"))
                outPath += ".tsv";

            List<string> headers = new();
            List<Dictionary<string, string>> tsvStringDictionary = new();
            // merge all headers into a single header
            foreach (var run in iTsvs)
            {
                var tsv = new Dictionary<string, string>();
                var headerSplits = run.TabSeparatedHeader.Split('\t');
                var stringSplits = run.ToTsvString().Split('\t');
                headers.AddRange(headerSplits);
                for (int i = 0; i < headerSplits.Length; i++)
                {
                    tsv.TryAdd(headerSplits[i], stringSplits[i]);
                }
                tsvStringDictionary.Add(tsv);
            }

            var distinctHeaders = headers.Distinct().ToList();
            var finalHeader = string.Join("\t", distinctHeaders);
            List<string> finalTsvStrings = new();
            // recreate each Tsv string, filling in blanks where one did not have the header
            foreach (var tsvResult in tsvStringDictionary)
            {
                string tsvString = "";
                foreach (var header in distinctHeaders)
                {
                    if (tsvResult.TryGetValue(header, out string field))
                    {
                        tsvString += field + '\t';
                    }
                    else
                    {
                        tsvString += '\t';
                    }
                }
                finalTsvStrings.Add(tsvString);
            }

            using (var sw = new StreamWriter(File.Create(outPath)))
            {
                sw.WriteLine(finalHeader);
                foreach (var tsv in finalTsvStrings)
                {
                    sw.WriteLine(tsv);
                }
            }
        }


        public static Type GetTaskType(this MyTask task)
        {
            switch (task)
            {
                case MyTask.Averaging: return typeof(AveragingTaskResult);
                case MyTask.Calibrate: return typeof(CalibrationTaskResult);
                case MyTask.Gptmd: return typeof(GptmdTaskResult);
                case MyTask.Search: return typeof(SearchTaskResult);
                default: Debugger.Break();
                    break;
            }

            return null;
        }
    }

}

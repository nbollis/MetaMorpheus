#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace TaskLayer.ParallelSearch.Analysis.Collectors;

public sealed class ProteinGroupTsvBackfillService
{
    public bool BackfillIfNeeded(string outputFolder, List<TransientDatabaseMetrics> metricsList)
    {
        bool backfilledAny = false;
        if (string.IsNullOrEmpty(outputFolder) || metricsList == null || metricsList.Count == 0)
            return backfilledAny;

        foreach (var metric in metricsList)
        {
            if (metric.AllPeptidesPerProteinGroup.Length > 0)
                continue;

            var dbDir = Path.Combine(outputFolder, metric.DatabaseName);
            if (!Directory.Exists(dbDir))
                continue;

            var tsvPath = Path.Combine(dbDir, $"{metric.DatabaseName}_AllProteinGroups.tsv");
            var gzPath = tsvPath + ".gz";

            string? filePath = null;
            bool isCompressed = false;

            if (File.Exists(tsvPath))
            {
                filePath = tsvPath;
                isCompressed = false;
            }
            else if (File.Exists(gzPath))
            {
                filePath = gzPath;
                isCompressed = true;
            }

            if (filePath == null)
                continue;

            try
            {
                var data = ParseProteinGroupTsv(filePath, isCompressed);
                metric.AllPeptidesPerProteinGroup = data.Peptides.ToArray();
                metric.AllUniquePeptidesPerProteinGroup = data.UniquePeptides.ToArray();
                metric.AllPsmsPerProteinGroup = data.Psms.ToArray();
                metric.MedianPeptidesPerProteinGroup = data.Peptides.Count > 0
                    ? ComputeMedian(data.Peptides) : 0.0;
                metric.MedianUniquePeptidesPerProteinGroup = data.UniquePeptides.Count > 0
                    ? ComputeMedian(data.UniquePeptides) : 0.0;
                metric.MedianPsmsPerProteinGroup = data.Peptides.Count > 0
                    ? (int)Math.Round(ComputeMedian(data.Psms)) : 0;
                metric.PopulateResultsFromProperties();
                backfilledAny = true;
            }
            catch
            {
                // If parsing fails, leave metrics at defaults
            }
        }
        return backfilledAny;
    }

    private static (List<double> Peptides, List<double> UniquePeptides, List<double> Psms)
        ParseProteinGroupTsv(string filePath, bool isCompressed)
    {
        var peptides = new List<double>();
        var uniquePeptides = new List<double>();
        var psms = new List<double>();

        IEnumerable<string> lines = isCompressed
            ? ReadLinesGzip(filePath)
            : File.ReadLines(filePath);

        using var enumerator = lines.GetEnumerator();

        if (!enumerator.MoveNext())
            return (peptides, uniquePeptides, psms);

        string? header = enumerator.Current;
        if (header == null)
            return (peptides, uniquePeptides, psms);

        var columns = header.Split('\t');
        int peptideCol = Array.IndexOf(columns, "Number of Peptides");
        int uniquePeptideCol = Array.IndexOf(columns, "Number of Unique Peptides");
        int psmCol = Array.IndexOf(columns, "Number of PSMs");
        int decoyCol = Array.IndexOf(columns, "Protein Decoy/Contaminant/Target");

        if (peptideCol < 0 || psmCol < 0 || decoyCol < 0)
            return (peptides, uniquePeptides, psms);

        while (enumerator.MoveNext())
        {
            var row = enumerator.Current;
            if (string.IsNullOrEmpty(row))
                continue;

            var fields = row.Split('\t');

            if (decoyCol < fields.Length)
            {
                var label = fields[decoyCol].Trim();
                if (label != "T")
                    continue;
            }

            if (peptideCol < fields.Length && int.TryParse(fields[peptideCol], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pepCount))
                peptides.Add(pepCount);

            if (uniquePeptideCol >= 0 && uniquePeptideCol < fields.Length
                && int.TryParse(fields[uniquePeptideCol], NumberStyles.Integer, CultureInfo.InvariantCulture, out var uniqueCount))
                uniquePeptides.Add(uniqueCount);

            if (psmCol < fields.Length && int.TryParse(fields[psmCol], NumberStyles.Integer, CultureInfo.InvariantCulture, out var psmCount))
                psms.Add(psmCount);
        }

        return (peptides, uniquePeptides, psms);
    }

    private static IEnumerable<string> ReadLinesGzip(string path)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzipStream);

        string? line;
        while ((line = reader.ReadLine()) != null)
            yield return line;
    }

    private static double ComputeMedian(List<double> values)
    {
        if (values.Count == 0)
            return double.NaN;

        var sorted = values.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;

        if (sorted.Count % 2 == 1)
            return sorted[mid];

        return (sorted[mid - 1] + sorted[mid]) / 2.0;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TaskLayer.ParallelSearchTask.Util;

internal class FastaStreamReader 
{
    internal static IEnumerable<(string Header, string Sequence)> ReadFasta(string filePath, bool replaceIWithL)
    {
        using var reader = new StreamReader(filePath);
        string? line;
        string? currentHeader = null;
        var sequenceBuilder = new System.Text.StringBuilder();
        while ((line = reader.ReadLine()) != null)
        {
            if (line.StartsWith(">"))
            {
                if (currentHeader != null)
                {
                    yield return (currentHeader, sequenceBuilder.ToString());
                    sequenceBuilder.Clear();
                }
                currentHeader = line.Substring(1).Trim();
            }
            else
            {
                sequenceBuilder.Append(line.Trim());
            }
        }
        if (currentHeader != null)
        {
            yield return (currentHeader, sequenceBuilder.ToString());
        }
    }

    /// <summary>
    /// Streams fasta entries in fixed-size chunks. Each yielded list contains up to <paramref name="chunkSize"/> protein entries.
    /// This preserves streaming semantics (the file is read lazily) while allowing callers to process chunks in parallel.
    /// Note: the returned enumerable is an iterator that keeps the underlying file open until enumeration completes or is disposed.
    /// </summary>
    /// <param name="filePath">Path to fasta file.</param>
    /// <param name="chunkSize">Maximum number of proteins per yielded chunk (must be &gt; 0).</param>
    internal static IEnumerable<List<(string Header, string Sequence)>> ReadFastaChunks(string filePath, int chunkSize)
    {
        if (chunkSize <= 0) throw new System.ArgumentOutOfRangeException(nameof(chunkSize));

        using var reader = new StreamReader(filePath);
        string? line;
        string? currentHeader = null;
        var sequenceBuilder = new System.Text.StringBuilder();
        var chunk = new List<(string Header, string Sequence)>(chunkSize);

        while ((line = reader.ReadLine()) != null)
        {
            if (line.StartsWith(">"))
            {
                if (currentHeader != null)
                {
                    chunk.Add((currentHeader, sequenceBuilder.ToString()));
                    sequenceBuilder.Clear();

                    if (chunk.Count >= chunkSize)
                    {
                        yield return chunk;
                        chunk = new List<(string Header, string Sequence)>(chunkSize);
                    }
                }
                currentHeader = line.Substring(1).Trim();
            }
            else
            {
                sequenceBuilder.Append(line.Trim());
            }
        }

        if (currentHeader != null)
        {
            chunk.Add((currentHeader, sequenceBuilder.ToString()));
        }

        if (chunk.Count > 0)
            yield return chunk;
    }

    /// <summary>
    /// Combine multiple FASTA files into a single FASTA, skipping duplicate headers.
    /// Streams input files and writes unique entries to the output file to preserve memory efficiency.
    /// </summary>
    /// <param name="inputFastaPaths">Enumerable of input FASTA file paths (order preserved).</param>
    /// <param name="outputFilePath">Path to write the combined FASTA.</param>
    /// <returns>Number of unique proteins written to the combined FASTA.</returns>
    internal static int CombineFastas(IEnumerable<string> inputFastaPaths, string outputFilePath)
    {
        var seen = new HashSet<string>();
        int written = 0;

        using var writer = new StreamWriter(outputFilePath);

        foreach (var input in inputFastaPaths)
        {
            if (!File.Exists(input))
                continue;

            using var reader = new StreamReader(input);
            string? line;
            string? currentHeader = null;
            var seqBuilder = new System.Text.StringBuilder();
            bool includeCurrent = false;

            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith(">"))
                {
                    if (includeCurrent && currentHeader != null)
                    {
                        writer.WriteLine(">" + currentHeader);
                        var seq = seqBuilder.ToString();
                        for (int i = 0; i < seq.Length; i += 60)
                        {
                            int len = Math.Min(60, seq.Length - i);
                            writer.WriteLine(seq.Substring(i, len));
                        }
                        written++;
                    }

                    currentHeader = line.Substring(1).Trim();
                    seqBuilder.Clear();
                    includeCurrent = !seen.Contains(currentHeader);
                    if (includeCurrent)
                        seen.Add(currentHeader);
                }
                else if (includeCurrent)
                {
                    seqBuilder.Append(line.Trim());
                }
            }

            if (includeCurrent && currentHeader != null)
            {
                writer.WriteLine(">" + currentHeader);
                var seq = seqBuilder.ToString();
                for (int i = 0; i < seq.Length; i += 60)
                {
                    int len = Math.Min(60, seq.Length - i);
                    writer.WriteLine(seq.Substring(i, len));
                }
                written++;
            }
        }

        return written;
    }
}
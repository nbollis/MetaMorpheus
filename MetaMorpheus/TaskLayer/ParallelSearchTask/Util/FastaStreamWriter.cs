using System;
using System.Collections.Generic;
using System.IO;

namespace TaskLayer.ParallelSearchTask.Util;

/// <summary>
/// Instance-based FASTA writer that supports continuous appending to a growing FASTA file.
/// This class wraps a StreamWriter and automatically handles duplicate checking and proper FASTA formatting.
/// Thread-safe: Multiple threads can safely call WriteProtein/WriteProteins concurrently.
/// </summary>
internal class FastaStreamWriter : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly HashSet<string> _writtenHeaders;
    private readonly bool _checkDuplicates;
    private readonly object _writerLock = new object();
    private readonly object _headersLock = new object();
    private bool _disposed;

    /// <summary>
    /// Gets the number of unique proteins written to the FASTA file.
    /// Thread-safe property access.
    /// </summary>
    public int ProteinsWritten
    {
        get
        {
            lock (_headersLock)
            {
                return _writtenHeaders.Count;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of FastaStreamWriter.
    /// </summary>
    /// <param name="outputFilePath">Path to the output FASTA file.</param>
    /// <param name="append">If true, appends to existing file; otherwise creates new file.</param>
    /// <param name="checkDuplicates">If true, tracks written headers and skips duplicates.</param>
    public FastaStreamWriter(string outputFilePath, bool append = false, bool checkDuplicates = true)
    {
        _writer = new StreamWriter(outputFilePath, append);
        _checkDuplicates = checkDuplicates;
        _writtenHeaders = checkDuplicates ? new HashSet<string>() : new HashSet<string>();
        
        // If appending and checking duplicates, we should ideally scan the existing file
        // However, for performance, we assume the caller manages this or accepts potential duplicates
        // when append=true and the file already exists
    }

    /// <summary>
    /// Writes a single protein entry to the FASTA file.
    /// Thread-safe: Can be called concurrently from multiple threads.
    /// </summary>
    /// <param name="header">Protein header (without the '>' prefix).</param>
    /// <param name="sequence">Protein sequence.</param>
    /// <returns>True if the protein was written; false if it was skipped (duplicate or invalid).</returns>
    public bool WriteProtein(string header, string sequence)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FastaStreamWriter));

        if (string.IsNullOrWhiteSpace(header) || string.IsNullOrWhiteSpace(sequence))
            return false;

        // Check for duplicates if enabled (lock the headers collection)
        if (_checkDuplicates)
        {
            lock (_headersLock)
            {
                if (!_writtenHeaders.Add(header))
                    return false; // Duplicate, skip
            }
        }

        // Write to file (lock the writer to ensure sequential writes)
        lock (_writerLock)
        {
            // Double-check disposal state after acquiring lock
            if (_disposed)
                throw new ObjectDisposedException(nameof(FastaStreamWriter));

            // Write header
            _writer.WriteLine(">" + header);

            // Write sequence in 60-character lines (standard FASTA format)
            for (int i = 0; i < sequence.Length; i += 60)
            {
                int length = Math.Min(60, sequence.Length - i);
                _writer.WriteLine(sequence.Substring(i, length));
            }
        }

        return true;
    }

    /// <summary>
    /// Writes multiple protein entries to the FASTA file.
    /// Thread-safe: Can be called concurrently from multiple threads.
    /// </summary>
    /// <param name="proteins">Collection of (header, sequence) tuples.</param>
    /// <returns>Number of proteins actually written (excludes duplicates if checking is enabled).</returns>
    public int WriteProteins(IEnumerable<(string Header, string Sequence)> proteins)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FastaStreamWriter));

        int written = 0;
        foreach (var (header, sequence) in proteins)
        {
            if (WriteProtein(header, sequence))
                written++;
        }
        return written;
    }

    /// <summary>
    /// Flushes the underlying StreamWriter buffer to ensure all data is written to disk.
    /// Thread-safe: Can be called concurrently from multiple threads.
    /// </summary>
    public void Flush()
    {
        lock (_writerLock)
        {
            if (!_disposed)
                _writer.Flush();
        }
    }

    /// <summary>
    /// Checks if a header has already been written (only meaningful if checkDuplicates is enabled).
    /// Thread-safe: Can be called concurrently from multiple threads.
    /// </summary>
    public bool HasWritten(string header)
    {
        if (!_checkDuplicates)
            return false;

        lock (_headersLock)
        {
            return _writtenHeaders.Contains(header);
        }
    }

    /// <summary>
    /// Disposes the writer and releases resources.
    /// Thread-safe: Can be called concurrently (subsequent calls are no-ops).
    /// </summary>
    public void Dispose()
    {
        lock (_writerLock)
        {
            if (!_disposed)
            {
                _writer.Dispose();
                _disposed = true;
            }
        }
    }
}
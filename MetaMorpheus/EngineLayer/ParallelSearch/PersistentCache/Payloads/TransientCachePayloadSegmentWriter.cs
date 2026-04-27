using System;
using System.IO;
using System.Text;

namespace EngineLayer.ParallelSearch.PersistentCache.Payloads;

public sealed class TransientCachePayloadSegmentWriter
{
    public TransientCachePayloadWriteResult AppendShard(string segmentPath, TransientCachePayloadKind payloadKind, byte[] payloadBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(segmentPath);
        ArgumentNullException.ThrowIfNull(payloadBytes);

        string directory = Path.GetDirectoryName(segmentPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string sha256 = TransientCacheHashing.ComputeSha256Hex(payloadBytes);
        TransientCachePayloadHeader header = new(payloadKind, payloadBytes.LongLength, sha256);

        using FileStream stream = new(segmentPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        long offsetBytes = stream.Length;
        stream.Seek(offsetBytes, SeekOrigin.Begin);

        using BinaryWriter writer = new(stream, Encoding.ASCII, leaveOpen: true);
        header.Write(writer);
        writer.Write(payloadBytes);
        writer.Flush();
        stream.Flush(true);

        return new TransientCachePayloadWriteResult(
            offsetBytes,
            TransientCachePayloadHeader.SerializedLength + payloadBytes.LongLength,
            payloadBytes.LongLength,
            sha256);
    }
}

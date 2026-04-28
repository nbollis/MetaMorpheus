using System;
using System.IO;
using System.Text;
using EngineLayer.ParallelSearch.PersistentCache.Manifest;

namespace EngineLayer.ParallelSearch.PersistentCache.Payloads;

internal sealed class TransientCachePayloadSegmentReader
{
    public byte[] ReadShard(string segmentPath, TransientCacheResolvedShardReference shardReference)
    {
        ArgumentNullException.ThrowIfNull(shardReference);

        return ReadShard(
            segmentPath,
            shardReference.OffsetBytes,
            shardReference.StoredLengthBytes,
            shardReference.PayloadKind,
            shardReference.LogicalLengthBytes,
            shardReference.Sha256);
    }

    public byte[] ReadShard(
        string segmentPath,
        long offsetBytes,
        long storedLengthBytes,
        TransientCachePayloadKind expectedPayloadKind,
        long expectedLogicalLengthBytes,
        string expectedSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(segmentPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSha256);

        using FileStream stream = new(segmentPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        stream.Seek(offsetBytes, SeekOrigin.Begin);

        using BinaryReader reader = new(stream, Encoding.ASCII, leaveOpen: true);
        TransientCachePayloadHeader header = TransientCachePayloadHeader.Read(reader);

        if (header.PayloadKind != expectedPayloadKind)
        {
            throw new InvalidDataException($"Transient cache payload kind mismatch. Expected {expectedPayloadKind}, found {header.PayloadKind}.");
        }

        if (header.LogicalLengthBytes != expectedLogicalLengthBytes)
        {
            throw new InvalidDataException($"Transient cache payload logical length mismatch. Expected {expectedLogicalLengthBytes}, found {header.LogicalLengthBytes}.");
        }

        if (!string.Equals(header.Sha256, expectedSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Transient cache payload checksum mismatch between header and manifest.");
        }

        long expectedStoredLengthBytes = TransientCachePayloadHeader.SerializedLength + header.LogicalLengthBytes;
        if (storedLengthBytes != expectedStoredLengthBytes)
        {
            throw new InvalidDataException($"Transient cache payload stored length mismatch. Expected {expectedStoredLengthBytes}, found {storedLengthBytes}.");
        }

        if (header.LogicalLengthBytes > int.MaxValue)
        {
            throw new NotSupportedException("Transient cache payloads larger than 2GB are not supported by the current byte-array reader.");
        }

        byte[] payloadBytes = reader.ReadBytes((int)header.LogicalLengthBytes);
        if (payloadBytes.LongLength != header.LogicalLengthBytes)
        {
            throw new InvalidDataException("Transient cache payload is truncated.");
        }

        string actualSha256 = TransientCacheHashing.ComputeSha256Hex(payloadBytes);
        if (!string.Equals(actualSha256, expectedSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Transient cache payload checksum validation failed.");
        }

        return payloadBytes;
    }
}

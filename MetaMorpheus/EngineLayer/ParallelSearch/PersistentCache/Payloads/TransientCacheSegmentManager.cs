using System;
using System.IO;
using EngineLayer.ParallelSearch.PersistentCache.Manifest;

namespace EngineLayer.ParallelSearch.PersistentCache.Payloads;

public sealed class TransientCacheSegmentManager
{
    public const long DefaultOccurrenceSegmentMaxBytes = 128L * 1024 * 1024;
    public const long DefaultFragmentSegmentMaxBytes = 512L * 1024 * 1024;

    private readonly TransientCacheManifestStore _manifestStore;
    private readonly TransientCacheStorageLayout _storageLayout;
    private readonly TransientCachePayloadSegmentWriter _writer = new();

    public TransientCacheSegmentManager(
        TransientCacheManifestStore manifestStore,
        TransientCacheStorageLayout storageLayout)
    {
        _manifestStore = manifestStore ?? throw new ArgumentNullException(nameof(manifestStore));
        _storageLayout = storageLayout ?? throw new ArgumentNullException(nameof(storageLayout));
    }

    public TransientCacheSegmentAppendResult AppendPayloadShard(TransientCachePayloadKind payloadKind, byte[] payloadBytes)
    {
        ArgumentNullException.ThrowIfNull(payloadBytes);

        TransientCachePayloadKind segmentFamily = GetSegmentFamily(payloadKind);
        long segmentCapBytes = GetSegmentCapBytes(segmentFamily);
        long storedLengthBytes = TransientCachePayloadHeader.SerializedLength + payloadBytes.LongLength;

        // Segment rows track append families, while shard rows still track the actual payload kind.
        long maxExistingSegmentLength = storedLengthBytes > segmentCapBytes
            ? -1
            : segmentCapBytes - storedLengthBytes;

        TransientCachePayloadSegmentRecord? existingSegment = maxExistingSegmentLength < 0
            ? null
            : _manifestStore.TryGetLatestPayloadSegment(segmentFamily, maxExistingSegmentLength);

        TransientCachePayloadSegmentRecord segment = existingSegment ?? CreateNextSegment(segmentFamily);

        string segmentPath = _storageLayout.GetSegmentPath(segment.RelativePath);
        TransientCachePayloadWriteResult writeResult = _writer.AppendShard(segmentPath, payloadKind, payloadBytes);

        long trueSegmentLength = writeResult.OffsetBytes + writeResult.StoredLengthBytes;
        _manifestStore.UpdatePayloadSegmentLength(segment.SegmentId, trueSegmentLength);

        return new TransientCacheSegmentAppendResult(
            new TransientCachePayloadSegmentRecord(
                segment.SegmentId,
                segment.PayloadKind,
                segment.RelativePath,
                trueSegmentLength,
                segment.CreatedUtc),
            writeResult);
    }

    public static TransientCachePayloadKind GetSegmentFamily(TransientCachePayloadKind payloadKind)
    {
        return payloadKind == TransientCachePayloadKind.Fragment
            ? TransientCachePayloadKind.Fragment
            : TransientCachePayloadKind.Occurrence;
    }

    public static long GetSegmentCapBytes(TransientCachePayloadKind segmentFamily)
    {
        return segmentFamily == TransientCachePayloadKind.Fragment
            ? DefaultFragmentSegmentMaxBytes
            : DefaultOccurrenceSegmentMaxBytes;
    }

    private TransientCachePayloadSegmentRecord CreateNextSegment(TransientCachePayloadKind segmentFamily)
    {
        TransientCachePayloadSegmentRecord? latestSegment = _manifestStore.TryGetLatestPayloadSegment(segmentFamily);
        int nextSequence = latestSegment is null ? 1 : ParseSegmentSequence(latestSegment.Value.RelativePath) + 1;

        string relativePath = Path.Combine(
            GetSegmentDirectoryName(segmentFamily),
            $"segment-{nextSequence:D6}.bin");

        return _manifestStore.UpsertPayloadSegment(segmentFamily, relativePath, lengthBytes: 0);
    }

    private static int ParseSegmentSequence(string relativePath)
    {
        string fileName = Path.GetFileNameWithoutExtension(relativePath);
        string[] parts = fileName.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[1], out int sequence))
        {
            throw new InvalidOperationException($"Unrecognized transient cache segment name '{relativePath}'.");
        }

        return sequence;
    }

    private static string GetSegmentDirectoryName(TransientCachePayloadKind segmentFamily)
    {
        return segmentFamily == TransientCachePayloadKind.Fragment
            ? "fragment"
            : "occurrence";
    }
}

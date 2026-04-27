using System;
using EngineLayer.ParallelSearch.PersistentCache.Payloads;

namespace EngineLayer.ParallelSearch.PersistentCache.Manifest;

public sealed class TransientCacheManifestEntry
{
    public TransientCacheKey Key { get; }
    public TransientCachePublishState PublishState { get; }
    public string Detail { get; init; }
    public int? ProteinCount { get; init; }
    public int? PeptideCount { get; init; }
    public string EntryChecksum { get; init; }
    public DateTimeOffset? CreatedUtc { get; init; }
    public DateTimeOffset? PublishedUtc { get; init; }

    public TransientCacheManifestEntry(TransientCacheKey key, TransientCachePublishState publishState)
    {
        Key = key;
        PublishState = publishState;
    }
}

public sealed class TransientCacheResolvedShardReference
{
    public long ShardId { get; }
    public TransientCachePayloadKind PayloadKind { get; }
    public int Ordinal { get; }
    public string RelativePath { get; }
    public long OffsetBytes { get; }
    public long StoredLengthBytes { get; }
    public long LogicalLengthBytes { get; }
    public string Sha256 { get; }
    public int ReferenceCount { get; }

    public TransientCacheResolvedShardReference(
        long shardId,
        TransientCachePayloadKind payloadKind,
        int ordinal,
        string relativePath,
        long offsetBytes,
        long storedLengthBytes,
        long logicalLengthBytes,
        string sha256,
        int referenceCount)
    {
        ShardId = shardId;
        PayloadKind = payloadKind;
        Ordinal = ordinal;
        RelativePath = relativePath;
        OffsetBytes = offsetBytes;
        StoredLengthBytes = storedLengthBytes;
        LogicalLengthBytes = logicalLengthBytes;
        Sha256 = sha256;
        ReferenceCount = referenceCount;
    }
}

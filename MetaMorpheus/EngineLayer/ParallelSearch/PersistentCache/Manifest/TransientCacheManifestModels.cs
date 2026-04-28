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

public readonly record struct TransientCacheEntrySequenceReference(
    long SequenceId,
    int LocalOrdinal);

public sealed class TransientCacheSharedSequenceRecord
{
    public long SequenceId { get; }
    public string CacheSettingsId { get; }
    public string SequenceHash { get; }
    public string FullSequence { get; }
    public long? FragmentShardId { get; }
    public bool IsQuarantined { get; }
    public string QuarantineReason { get; }
    public DateTimeOffset CreatedUtc { get; }
    public DateTimeOffset? QuarantinedUtc { get; }

    public TransientCacheSharedSequenceRecord(
        long sequenceId,
        string cacheSettingsId,
        string sequenceHash,
        string fullSequence,
        long? fragmentShardId,
        bool isQuarantined,
        string quarantineReason,
        DateTimeOffset createdUtc,
        DateTimeOffset? quarantinedUtc)
    {
        SequenceId = sequenceId;
        CacheSettingsId = cacheSettingsId;
        SequenceHash = sequenceHash;
        FullSequence = fullSequence;
        FragmentShardId = fragmentShardId;
        IsQuarantined = isQuarantined;
        QuarantineReason = quarantineReason;
        CreatedUtc = createdUtc;
        QuarantinedUtc = quarantinedUtc;
    }
}

public sealed class TransientCacheResolvedSequenceReference
{
    public int LocalOrdinal { get; }
    public long SequenceId { get; }
    public string SequenceHash { get; }
    public string FullSequence { get; }
    public long? FragmentShardId { get; }
    public bool IsQuarantined { get; }
    public string QuarantineReason { get; }

    public TransientCacheResolvedSequenceReference(
        int localOrdinal,
        long sequenceId,
        string sequenceHash,
        string fullSequence,
        long? fragmentShardId,
        bool isQuarantined,
        string quarantineReason)
    {
        LocalOrdinal = localOrdinal;
        SequenceId = sequenceId;
        SequenceHash = sequenceHash;
        FullSequence = fullSequence;
        FragmentShardId = fragmentShardId;
        IsQuarantined = isQuarantined;
        QuarantineReason = quarantineReason;
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

public readonly record struct TransientCacheGrowthSummary(
    long EntryCount,
    long PublishedEntryCount,
    long SharedSequenceCount,
    long QuarantinedSharedSequenceCount,
    long OccurrenceSegmentCount,
    long FragmentSegmentCount,
    long OccurrenceShardCount,
    long FragmentShardCount,
    long OccurrencePayloadBytes,
    long FragmentPayloadBytes,
    long TotalPayloadBytes);

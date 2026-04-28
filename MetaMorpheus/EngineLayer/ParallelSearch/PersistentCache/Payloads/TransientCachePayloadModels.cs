using System;

namespace EngineLayer.ParallelSearch.PersistentCache.Payloads;

internal enum TransientCachePayloadKind
{
    // Preserve the live persisted values used by schema v2 even though the old
    // V1 payload kinds were retired during the redesign.
    Occurrence = 2,
    Fragment = 4,
}

internal readonly record struct TransientCacheEntryShardReference(
    long ShardId,
    TransientCachePayloadKind PayloadKind,
    int Ordinal);

internal readonly record struct TransientCachePayloadSegmentRecord(
    long SegmentId,
    TransientCachePayloadKind PayloadKind,
    string RelativePath,
    long LengthBytes,
    DateTimeOffset CreatedUtc);

internal readonly record struct TransientCachePayloadShardRecord(
    long ShardId,
    long SegmentId,
    TransientCachePayloadKind PayloadKind,
    long OffsetBytes,
    long StoredLengthBytes,
    long LogicalLengthBytes,
    string Sha256,
    int ReferenceCount,
    DateTimeOffset CreatedUtc);

internal readonly record struct TransientCachePayloadWriteResult(
    long OffsetBytes,
    long StoredLengthBytes,
    long LogicalLengthBytes,
    string Sha256);

internal readonly record struct TransientCacheSegmentAppendResult(
    TransientCachePayloadSegmentRecord Segment,
    TransientCachePayloadWriteResult WriteResult);

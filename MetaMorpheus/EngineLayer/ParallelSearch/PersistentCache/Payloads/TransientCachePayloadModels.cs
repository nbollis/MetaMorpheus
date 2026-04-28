using System;

namespace EngineLayer.ParallelSearch.PersistentCache.Payloads;

public enum TransientCachePayloadKind
{
    ProteinDigest = 1,
    Occurrence = 2,
    Peptidoform = 3,
    Fragment = 4,
}

public readonly record struct TransientCacheEntryShardReference(
    long ShardId,
    TransientCachePayloadKind PayloadKind,
    int Ordinal);

public readonly record struct TransientCachePayloadSegmentRecord(
    long SegmentId,
    TransientCachePayloadKind PayloadKind,
    string RelativePath,
    long LengthBytes,
    DateTimeOffset CreatedUtc);

public readonly record struct TransientCachePayloadShardRecord(
    long ShardId,
    long SegmentId,
    TransientCachePayloadKind PayloadKind,
    long OffsetBytes,
    long StoredLengthBytes,
    long LogicalLengthBytes,
    string Sha256,
    int ReferenceCount,
    DateTimeOffset CreatedUtc);

public readonly record struct TransientCachePayloadWriteResult(
    long OffsetBytes,
    long StoredLengthBytes,
    long LogicalLengthBytes,
    string Sha256);

public readonly record struct TransientCacheSegmentAppendResult(
    TransientCachePayloadSegmentRecord Segment,
    TransientCachePayloadWriteResult WriteResult);

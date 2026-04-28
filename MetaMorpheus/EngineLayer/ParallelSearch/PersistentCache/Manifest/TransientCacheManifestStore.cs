using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using EngineLayer.ParallelSearch.PersistentCache.Payloads;

namespace EngineLayer.ParallelSearch.PersistentCache.Manifest;

public sealed class TransientCacheManifestStore
{
    public string ManifestPath { get; }

    public TransientCacheManifestStore(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ManifestPath = manifestPath;
    }

    public void Initialize()
    {
        string directory = Path.GetDirectoryName(ManifestPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using SQLiteConnection connection = OpenConnection();
        using SQLiteTransaction transaction = connection.BeginTransaction();

        foreach (string statement in TransientCacheManifestSchema.CreateStatements)
        {
            using SQLiteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void UpsertSourceDatabase(string databaseContentHash, string originalPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseContentHash);

        using SQLiteConnection connection = OpenConnection();
        using SQLiteCommand command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO SourceDatabases (DatabaseContentHash, OriginalPath, CreatedUtc)
VALUES (@DatabaseContentHash, @OriginalPath, @CreatedUtc)
ON CONFLICT(DatabaseContentHash) DO UPDATE SET
    OriginalPath = COALESCE(excluded.OriginalPath, SourceDatabases.OriginalPath);";
        command.Parameters.AddWithValue("@DatabaseContentHash", databaseContentHash);
        command.Parameters.AddWithValue("@OriginalPath", (object)originalPath ?? DBNull.Value);
        command.Parameters.AddWithValue("@CreatedUtc", FormatTimestamp(DateTimeOffset.UtcNow));
        command.ExecuteNonQuery();
    }

    public void UpsertCacheSettings(string cacheSettingsId, string canonicalSettingsPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheSettingsId);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalSettingsPayload);

        using SQLiteConnection connection = OpenConnection();
        using SQLiteCommand command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO CacheSettings (CacheSettingsId, CanonicalSettingsPayload, CreatedUtc)
VALUES (@CacheSettingsId, @CanonicalSettingsPayload, @CreatedUtc)
ON CONFLICT(CacheSettingsId) DO UPDATE SET
    CanonicalSettingsPayload = excluded.CanonicalSettingsPayload;";
        command.Parameters.AddWithValue("@CacheSettingsId", cacheSettingsId);
        command.Parameters.AddWithValue("@CanonicalSettingsPayload", canonicalSettingsPayload);
        command.Parameters.AddWithValue("@CreatedUtc", FormatTimestamp(DateTimeOffset.UtcNow));
        command.ExecuteNonQuery();
    }

    public TransientCacheSharedSequenceRecord UpsertSharedSequence(
        string cacheSettingsId,
        string sequenceHash,
        string fullSequence,
        long? fragmentShardId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheSettingsId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sequenceHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullSequence);

        using SQLiteConnection connection = OpenConnection();
        using SQLiteCommand command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO SharedSequences (
    CacheSettingsId,
    SequenceHash,
    FullSequence,
    FragmentShardId,
    IsQuarantined,
    QuarantineReason,
    CreatedUtc,
    QuarantinedUtc)
VALUES (
    @CacheSettingsId,
    @SequenceHash,
    @FullSequence,
    @FragmentShardId,
    0,
    NULL,
    @CreatedUtc,
    NULL)
ON CONFLICT(CacheSettingsId, SequenceHash, FullSequence) DO UPDATE SET
    FragmentShardId = COALESCE(excluded.FragmentShardId, SharedSequences.FragmentShardId);";
        command.Parameters.AddWithValue("@CacheSettingsId", cacheSettingsId);
        command.Parameters.AddWithValue("@SequenceHash", sequenceHash);
        command.Parameters.AddWithValue("@FullSequence", fullSequence);
        command.Parameters.AddWithValue("@FragmentShardId", (object)fragmentShardId ?? DBNull.Value);
        command.Parameters.AddWithValue("@CreatedUtc", FormatTimestamp(DateTimeOffset.UtcNow));
        command.ExecuteNonQuery();

        return TryGetSharedSequence(cacheSettingsId, sequenceHash, fullSequence, includeQuarantined: true)
               ?? throw new InvalidOperationException($"Failed to round-trip shared transient cache sequence '{fullSequence}'.");
    }

    public void UpdateSharedSequenceFragmentShard(long sequenceId, long fragmentShardId)
    {
        using SQLiteConnection connection = OpenConnection();
        using SQLiteCommand command = connection.CreateCommand();
        command.CommandText = @"
UPDATE SharedSequences
SET FragmentShardId = @FragmentShardId,
    IsQuarantined = 0,
    QuarantineReason = NULL,
    QuarantinedUtc = NULL
WHERE SequenceId = @SequenceId;";
        command.Parameters.AddWithValue("@SequenceId", sequenceId);
        command.Parameters.AddWithValue("@FragmentShardId", fragmentShardId);

        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException($"Shared transient cache sequence '{sequenceId}' was not found.");
        }
    }

    public int QuarantineSharedSequencesByFragmentShard(long fragmentShardId, string quarantineReason)
    {
        using SQLiteConnection connection = OpenConnection();
        using SQLiteCommand command = connection.CreateCommand();
        command.CommandText = @"
UPDATE SharedSequences
SET IsQuarantined = 1,
    QuarantineReason = @QuarantineReason,
    QuarantinedUtc = @QuarantinedUtc
WHERE FragmentShardId = @FragmentShardId
  AND IsQuarantined = 0;";
        command.Parameters.AddWithValue("@FragmentShardId", fragmentShardId);
        command.Parameters.AddWithValue("@QuarantineReason", (object)quarantineReason ?? DBNull.Value);
        command.Parameters.AddWithValue("@QuarantinedUtc", FormatTimestamp(DateTimeOffset.UtcNow));
        return command.ExecuteNonQuery();
    }

    public void QuarantineSharedSequence(long sequenceId, string quarantineReason)
    {
        using SQLiteConnection connection = OpenConnection();
        using SQLiteCommand command = connection.CreateCommand();
        command.CommandText = @"
UPDATE SharedSequences
SET IsQuarantined = 1,
    QuarantineReason = @QuarantineReason,
    QuarantinedUtc = @QuarantinedUtc
WHERE SequenceId = @SequenceId;";
        command.Parameters.AddWithValue("@SequenceId", sequenceId);
        command.Parameters.AddWithValue("@QuarantineReason", (object)quarantineReason ?? DBNull.Value);
        command.Parameters.AddWithValue("@QuarantinedUtc", FormatTimestamp(DateTimeOffset.UtcNow));

        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException($"Shared transient cache sequence '{sequenceId}' was not found.");
        }
    }

    public TransientCacheSharedSequenceRecord? TryGetSharedSequence(
        string cacheSettingsId,
        string sequenceHash,
        string fullSequence,
        bool includeQuarantined = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheSettingsId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sequenceHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullSequence);

        using SQLiteConnection connection = OpenConnection();
        using SQLiteCommand command = connection.CreateCommand();
        command.CommandText = @"
SELECT SequenceId, CacheSettingsId, SequenceHash, FullSequence, FragmentShardId, IsQuarantined, QuarantineReason, CreatedUtc, QuarantinedUtc
FROM SharedSequences
WHERE CacheSettingsId = @CacheSettingsId
  AND SequenceHash = @SequenceHash
  AND FullSequence = @FullSequence" +
            (includeQuarantined ? string.Empty : " AND IsQuarantined = 0") +
            @";";
        command.Parameters.AddWithValue("@CacheSettingsId", cacheSettingsId);
        command.Parameters.AddWithValue("@SequenceHash", sequenceHash);
        command.Parameters.AddWithValue("@FullSequence", fullSequence);

        using SQLiteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadSharedSequence(reader) : null;
    }

    public TransientCachePayloadSegmentRecord? TryGetLatestPayloadSegment(TransientCachePayloadKind payloadKind, long maxLengthBytes = long.MaxValue)
    {
        using SQLiteConnection connection = OpenConnection();
        using SQLiteCommand command = connection.CreateCommand();
        command.CommandText = @"
SELECT SegmentId, PayloadKind, RelativePath, LengthBytes, CreatedUtc
FROM PayloadSegments
WHERE PayloadKind = @PayloadKind
  AND LengthBytes <= @MaxLengthBytes
ORDER BY SegmentId DESC
LIMIT 1;";
        command.Parameters.AddWithValue("@PayloadKind", (int)payloadKind);
        command.Parameters.AddWithValue("@MaxLengthBytes", maxLengthBytes);

        using SQLiteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadPayloadSegment(reader) : null;
    }

    public TransientCachePayloadSegmentRecord UpsertPayloadSegment(TransientCachePayloadKind payloadKind, string relativePath, long lengthBytes = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        using SQLiteConnection connection = OpenConnection();
        using SQLiteCommand upsertCommand = connection.CreateCommand();
        upsertCommand.CommandText = @"
INSERT INTO PayloadSegments (PayloadKind, RelativePath, LengthBytes, CreatedUtc)
VALUES (@PayloadKind, @RelativePath, @LengthBytes, @CreatedUtc)
ON CONFLICT(RelativePath) DO UPDATE SET
    PayloadKind = excluded.PayloadKind,
    LengthBytes = MAX(PayloadSegments.LengthBytes, excluded.LengthBytes);";
        upsertCommand.Parameters.AddWithValue("@PayloadKind", (int)payloadKind);
        upsertCommand.Parameters.AddWithValue("@RelativePath", relativePath);
        upsertCommand.Parameters.AddWithValue("@LengthBytes", lengthBytes);
        upsertCommand.Parameters.AddWithValue("@CreatedUtc", FormatTimestamp(DateTimeOffset.UtcNow));
        upsertCommand.ExecuteNonQuery();

        using SQLiteCommand selectCommand = connection.CreateCommand();
        selectCommand.CommandText = @"
SELECT SegmentId, PayloadKind, RelativePath, LengthBytes, CreatedUtc
FROM PayloadSegments
WHERE RelativePath = @RelativePath;";
        selectCommand.Parameters.AddWithValue("@RelativePath", relativePath);

        using SQLiteDataReader reader = selectCommand.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Failed to round-trip transient cache payload segment '{relativePath}'.");
        }

        return ReadPayloadSegment(reader);
    }

    public void UpdatePayloadSegmentLength(long segmentId, long lengthBytes)
    {
        using SQLiteConnection connection = OpenConnection();
        using SQLiteCommand command = connection.CreateCommand();
        command.CommandText = @"
UPDATE PayloadSegments
SET LengthBytes = @LengthBytes
WHERE SegmentId = @SegmentId;";
        command.Parameters.AddWithValue("@SegmentId", segmentId);
        command.Parameters.AddWithValue("@LengthBytes", lengthBytes);

        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException($"Transient cache payload segment '{segmentId}' was not found.");
        }
    }

    public TransientCachePayloadShardRecord InsertPayloadShard(
        long segmentId,
        TransientCachePayloadKind payloadKind,
        long offsetBytes,
        long storedLengthBytes,
        long logicalLengthBytes,
        string sha256,
        int referenceCount = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);

        using SQLiteConnection connection = OpenConnection();
        using SQLiteCommand command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO PayloadShards (SegmentId, PayloadKind, OffsetBytes, StoredLengthBytes, LogicalLengthBytes, Sha256, ReferenceCount, CreatedUtc)
VALUES (@SegmentId, @PayloadKind, @OffsetBytes, @StoredLengthBytes, @LogicalLengthBytes, @Sha256, @ReferenceCount, @CreatedUtc);
SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("@SegmentId", segmentId);
        command.Parameters.AddWithValue("@PayloadKind", (int)payloadKind);
        command.Parameters.AddWithValue("@OffsetBytes", offsetBytes);
        command.Parameters.AddWithValue("@StoredLengthBytes", storedLengthBytes);
        command.Parameters.AddWithValue("@LogicalLengthBytes", logicalLengthBytes);
        command.Parameters.AddWithValue("@Sha256", sha256);
        command.Parameters.AddWithValue("@ReferenceCount", referenceCount);
        command.Parameters.AddWithValue("@CreatedUtc", FormatTimestamp(DateTimeOffset.UtcNow));

        long shardId = (long)command.ExecuteScalar();
        return GetPayloadShard(shardId) ?? throw new InvalidOperationException($"Failed to round-trip transient cache payload shard '{shardId}'.");
    }

    public TransientCachePayloadShardRecord? TryGetPayloadShardByFingerprint(TransientCachePayloadKind payloadKind, string sha256, long logicalLengthBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);

        using SQLiteConnection connection = OpenConnection();
        using SQLiteCommand command = connection.CreateCommand();
        command.CommandText = @"
SELECT ShardId, SegmentId, PayloadKind, OffsetBytes, StoredLengthBytes, LogicalLengthBytes, Sha256, ReferenceCount, CreatedUtc
FROM PayloadShards
WHERE PayloadKind = @PayloadKind
  AND Sha256 = @Sha256
  AND LogicalLengthBytes = @LogicalLengthBytes
  AND NOT EXISTS (
      SELECT 1
      FROM SharedSequences ss
      WHERE ss.FragmentShardId = PayloadShards.ShardId
        AND ss.IsQuarantined = 1)
ORDER BY ShardId
LIMIT 1;";
        command.Parameters.AddWithValue("@PayloadKind", (int)payloadKind);
        command.Parameters.AddWithValue("@Sha256", sha256);
        command.Parameters.AddWithValue("@LogicalLengthBytes", logicalLengthBytes);

        using SQLiteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadPayloadShard(reader) : null;
    }

    public TransientCachePayloadShardRecord? GetPayloadShard(long shardId)
    {
        using SQLiteConnection connection = OpenConnection();
        using SQLiteCommand command = connection.CreateCommand();
        command.CommandText = @"
SELECT ShardId, SegmentId, PayloadKind, OffsetBytes, StoredLengthBytes, LogicalLengthBytes, Sha256, ReferenceCount, CreatedUtc
FROM PayloadShards
WHERE ShardId = @ShardId;";
        command.Parameters.AddWithValue("@ShardId", shardId);

        using SQLiteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadPayloadShard(reader) : null;
    }

    public TransientCacheResolvedShardReference? GetResolvedPayloadShard(long shardId)
    {
        using SQLiteConnection connection = OpenConnection();
        using SQLiteCommand command = connection.CreateCommand();
        command.CommandText = @"
SELECT
    psh.ShardId,
    psh.PayloadKind,
    0,
    ps.RelativePath,
    psh.OffsetBytes,
    psh.StoredLengthBytes,
    psh.LogicalLengthBytes,
    psh.Sha256,
    psh.ReferenceCount
FROM PayloadShards psh
INNER JOIN PayloadSegments ps ON ps.SegmentId = psh.SegmentId
WHERE psh.ShardId = @ShardId;";
        command.Parameters.AddWithValue("@ShardId", shardId);

        using SQLiteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadResolvedPayloadShard(reader) : null;
    }

    public int AdjustPayloadShardReferenceCount(long shardId, int delta)
    {
        using SQLiteConnection connection = OpenConnection();
        using SQLiteTransaction transaction = connection.BeginTransaction();

        using SQLiteCommand updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText = @"
UPDATE PayloadShards
SET ReferenceCount = ReferenceCount + @Delta
WHERE ShardId = @ShardId
  AND ReferenceCount + @Delta >= 0;";
        updateCommand.Parameters.AddWithValue("@ShardId", shardId);
        updateCommand.Parameters.AddWithValue("@Delta", delta);
        int updatedRows = updateCommand.ExecuteNonQuery();
        if (updatedRows != 1)
        {
            throw new InvalidOperationException($"Transient cache payload shard '{shardId}' was not found or the requested refcount delta would make it negative.");
        }

        using SQLiteCommand selectCommand = connection.CreateCommand();
        selectCommand.Transaction = transaction;
        selectCommand.CommandText = @"
SELECT ReferenceCount
FROM PayloadShards
WHERE ShardId = @ShardId;";
        selectCommand.Parameters.AddWithValue("@ShardId", shardId);

        int referenceCount = Convert.ToInt32(selectCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
        transaction.Commit();
        return referenceCount;
    }

    public void UpsertCacheEntry(TransientCacheManifestEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        DateTimeOffset createdUtc = entry.CreatedUtc ?? DateTimeOffset.UtcNow;
        DateTimeOffset? publishedUtc = entry.PublishedUtc;
        if (entry.PublishState == TransientCachePublishState.Published && publishedUtc is null)
        {
            publishedUtc = DateTimeOffset.UtcNow;
        }

        using SQLiteConnection connection = OpenConnection();
        using SQLiteCommand command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO CacheEntries (
    DatabaseContentHash,
    CacheSettingsId,
    PublishState,
    Detail,
    ProteinCount,
    PeptideCount,
    EntryChecksum,
    CreatedUtc,
    PublishedUtc)
VALUES (
    @DatabaseContentHash,
    @CacheSettingsId,
    @PublishState,
    @Detail,
    @ProteinCount,
    @PeptideCount,
    @EntryChecksum,
    @CreatedUtc,
    @PublishedUtc)
ON CONFLICT(DatabaseContentHash, CacheSettingsId) DO UPDATE SET
    PublishState = excluded.PublishState,
    Detail = excluded.Detail,
    ProteinCount = excluded.ProteinCount,
    PeptideCount = excluded.PeptideCount,
    EntryChecksum = excluded.EntryChecksum,
    CreatedUtc = excluded.CreatedUtc,
    PublishedUtc = excluded.PublishedUtc;";
        command.Parameters.AddWithValue("@DatabaseContentHash", entry.Key.DatabaseContentHash);
        command.Parameters.AddWithValue("@CacheSettingsId", entry.Key.CacheSettingsId);
        command.Parameters.AddWithValue("@PublishState", (int)entry.PublishState);
        command.Parameters.AddWithValue("@Detail", (object)entry.Detail ?? DBNull.Value);
        command.Parameters.AddWithValue("@ProteinCount", (object)entry.ProteinCount ?? DBNull.Value);
        command.Parameters.AddWithValue("@PeptideCount", (object)entry.PeptideCount ?? DBNull.Value);
        command.Parameters.AddWithValue("@EntryChecksum", (object)entry.EntryChecksum ?? DBNull.Value);
        command.Parameters.AddWithValue("@CreatedUtc", FormatTimestamp(createdUtc));
        command.Parameters.AddWithValue("@PublishedUtc", publishedUtc is null ? DBNull.Value : FormatTimestamp(publishedUtc.Value));
        command.ExecuteNonQuery();
    }

    public void ReplaceEntryShards(TransientCacheKey key, IReadOnlyCollection<TransientCacheEntryShardReference> shardReferences)
    {
        ArgumentNullException.ThrowIfNull(shardReferences);

        using SQLiteConnection connection = OpenConnection();
        using SQLiteTransaction transaction = connection.BeginTransaction();

        using (SQLiteCommand deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = @"
DELETE FROM CacheEntryShards
WHERE DatabaseContentHash = @DatabaseContentHash
  AND CacheSettingsId = @CacheSettingsId;";
            deleteCommand.Parameters.AddWithValue("@DatabaseContentHash", key.DatabaseContentHash);
            deleteCommand.Parameters.AddWithValue("@CacheSettingsId", key.CacheSettingsId);
            deleteCommand.ExecuteNonQuery();
        }

        foreach (TransientCacheEntryShardReference shardReference in shardReferences)
        {
            using SQLiteCommand insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = @"
INSERT INTO CacheEntryShards (DatabaseContentHash, CacheSettingsId, PayloadKind, Ordinal, ShardId)
VALUES (@DatabaseContentHash, @CacheSettingsId, @PayloadKind, @Ordinal, @ShardId);";
            insertCommand.Parameters.AddWithValue("@DatabaseContentHash", key.DatabaseContentHash);
            insertCommand.Parameters.AddWithValue("@CacheSettingsId", key.CacheSettingsId);
            insertCommand.Parameters.AddWithValue("@PayloadKind", (int)shardReference.PayloadKind);
            insertCommand.Parameters.AddWithValue("@Ordinal", shardReference.Ordinal);
            insertCommand.Parameters.AddWithValue("@ShardId", shardReference.ShardId);
            insertCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void ReplaceEntrySequences(TransientCacheKey key, IReadOnlyCollection<TransientCacheEntrySequenceReference> sequenceReferences)
    {
        ArgumentNullException.ThrowIfNull(sequenceReferences);

        using SQLiteConnection connection = OpenConnection();
        using SQLiteTransaction transaction = connection.BeginTransaction();

        using (SQLiteCommand deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = @"
DELETE FROM CacheEntrySequences
WHERE DatabaseContentHash = @DatabaseContentHash
  AND CacheSettingsId = @CacheSettingsId;";
            deleteCommand.Parameters.AddWithValue("@DatabaseContentHash", key.DatabaseContentHash);
            deleteCommand.Parameters.AddWithValue("@CacheSettingsId", key.CacheSettingsId);
            deleteCommand.ExecuteNonQuery();
        }

        foreach (TransientCacheEntrySequenceReference sequenceReference in sequenceReferences)
        {
            using SQLiteCommand insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = @"
INSERT INTO CacheEntrySequences (DatabaseContentHash, CacheSettingsId, LocalOrdinal, SequenceId)
VALUES (@DatabaseContentHash, @CacheSettingsId, @LocalOrdinal, @SequenceId);";
            insertCommand.Parameters.AddWithValue("@DatabaseContentHash", key.DatabaseContentHash);
            insertCommand.Parameters.AddWithValue("@CacheSettingsId", key.CacheSettingsId);
            insertCommand.Parameters.AddWithValue("@LocalOrdinal", sequenceReference.LocalOrdinal);
            insertCommand.Parameters.AddWithValue("@SequenceId", sequenceReference.SequenceId);
            insertCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public TransientCacheManifestEntry? TryGetCacheEntry(TransientCacheKey key)
    {
        return TryGetCacheEntry(key, null);
    }

    public TransientCacheManifestEntry? TryGetPublishedCacheEntry(TransientCacheKey key)
    {
        return TryGetCacheEntry(key, TransientCachePublishState.Published);
    }

    public IReadOnlyList<TransientCacheResolvedShardReference> GetResolvedEntryShardReferences(TransientCacheKey key)
    {
        using SQLiteConnection connection = OpenConnection();
        using SQLiteCommand command = connection.CreateCommand();
        command.CommandText = @"
SELECT
    ces.ShardId,
    ces.PayloadKind,
    ces.Ordinal,
    ps.RelativePath,
    psh.OffsetBytes,
    psh.StoredLengthBytes,
    psh.LogicalLengthBytes,
    psh.Sha256,
    psh.ReferenceCount
FROM CacheEntryShards ces
INNER JOIN PayloadShards psh ON psh.ShardId = ces.ShardId
INNER JOIN PayloadSegments ps ON ps.SegmentId = psh.SegmentId
WHERE ces.DatabaseContentHash = @DatabaseContentHash
  AND ces.CacheSettingsId = @CacheSettingsId
ORDER BY ces.PayloadKind, ces.Ordinal;";
        command.Parameters.AddWithValue("@DatabaseContentHash", key.DatabaseContentHash);
        command.Parameters.AddWithValue("@CacheSettingsId", key.CacheSettingsId);

        using SQLiteDataReader reader = command.ExecuteReader();
        List<TransientCacheResolvedShardReference> results = [];
        while (reader.Read())
        {
            results.Add(new TransientCacheResolvedShardReference(
                reader.GetInt64(0),
                (TransientCachePayloadKind)reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetInt64(4),
                reader.GetInt64(5),
                reader.GetInt64(6),
                reader.GetString(7),
                reader.GetInt32(8)));
        }

        return results;
    }

    public IReadOnlyList<TransientCacheResolvedSequenceReference> GetResolvedEntrySequenceReferences(TransientCacheKey key)
    {
        using SQLiteConnection connection = OpenConnection();
        using SQLiteCommand command = connection.CreateCommand();
        command.CommandText = @"
SELECT
    ces.LocalOrdinal,
    ss.SequenceId,
    ss.SequenceHash,
    ss.FullSequence,
    ss.FragmentShardId,
    ss.IsQuarantined,
    ss.QuarantineReason
FROM CacheEntrySequences ces
INNER JOIN SharedSequences ss ON ss.SequenceId = ces.SequenceId
WHERE ces.DatabaseContentHash = @DatabaseContentHash
  AND ces.CacheSettingsId = @CacheSettingsId
ORDER BY ces.LocalOrdinal;";
        command.Parameters.AddWithValue("@DatabaseContentHash", key.DatabaseContentHash);
        command.Parameters.AddWithValue("@CacheSettingsId", key.CacheSettingsId);

        using SQLiteDataReader reader = command.ExecuteReader();
        List<TransientCacheResolvedSequenceReference> results = [];
        while (reader.Read())
        {
            results.Add(new TransientCacheResolvedSequenceReference(
                reader.GetInt32(0),
                reader.GetInt64(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetInt64(4),
                reader.GetInt32(5) != 0,
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return results;
    }

    private TransientCacheManifestEntry? TryGetCacheEntry(TransientCacheKey key, TransientCachePublishState? requiredState)
    {
        using SQLiteConnection connection = OpenConnection();
        using SQLiteCommand command = connection.CreateCommand();
        command.CommandText = @"
SELECT DatabaseContentHash, CacheSettingsId, PublishState, Detail, ProteinCount, PeptideCount, EntryChecksum, CreatedUtc, PublishedUtc
FROM CacheEntries
WHERE DatabaseContentHash = @DatabaseContentHash
  AND CacheSettingsId = @CacheSettingsId" +
            (requiredState is null ? string.Empty : " AND PublishState = @PublishState") +
            ";";
        command.Parameters.AddWithValue("@DatabaseContentHash", key.DatabaseContentHash);
        command.Parameters.AddWithValue("@CacheSettingsId", key.CacheSettingsId);
        if (requiredState is not null)
        {
            command.Parameters.AddWithValue("@PublishState", (int)requiredState.Value);
        }

        using SQLiteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new TransientCacheManifestEntry(
            new TransientCacheKey(reader.GetString(0), reader.GetString(1)),
            (TransientCachePublishState)reader.GetInt32(2))
        {
            Detail = reader.IsDBNull(3) ? null : reader.GetString(3),
            ProteinCount = reader.IsDBNull(4) ? null : reader.GetInt32(4),
            PeptideCount = reader.IsDBNull(5) ? null : reader.GetInt32(5),
            EntryChecksum = reader.IsDBNull(6) ? null : reader.GetString(6),
            CreatedUtc = ParseTimestamp(reader.GetString(7)),
            PublishedUtc = reader.IsDBNull(8) ? null : ParseTimestamp(reader.GetString(8)),
        };
    }

    private SQLiteConnection OpenConnection()
    {
        SQLiteConnection connection = new($"Data Source={ManifestPath};");
        connection.Open();

        foreach (string pragma in new[]
                 {
                     "PRAGMA foreign_keys = ON;",
                     "PRAGMA busy_timeout = 5000;",
                     "PRAGMA journal_mode = WAL;"
                 })
        {
            using SQLiteCommand command = connection.CreateCommand();
            command.CommandText = pragma;
            command.ExecuteNonQuery();
        }

        return connection;
    }

    private static string FormatTimestamp(DateTimeOffset timestamp)
        => timestamp.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string timestamp)
        => DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public (int EntryCount, int PublishedEntryCount, long TotalShardCount, long TotalPayloadBytes) GetCacheGrowthSummary()
    {
        using SQLiteConnection connection = OpenConnection();

        using SQLiteCommand entryCommand = connection.CreateCommand();
        entryCommand.CommandText = @"
SELECT COUNT(*), SUM(CASE WHEN PublishState = @Published THEN 1 ELSE 0 END)
FROM CacheEntries;";
        entryCommand.Parameters.AddWithValue("@Published", (int)TransientCachePublishState.Published);
        using SQLiteDataReader entryReader = entryCommand.ExecuteReader();
        entryReader.Read();
        int entryCount = entryReader.GetInt32(0);
        int publishedEntryCount = entryReader.IsDBNull(1) ? 0 : entryReader.GetInt32(1);

        using SQLiteCommand shardCommand = connection.CreateCommand();
        shardCommand.CommandText = @"
SELECT COUNT(*), COALESCE(SUM(StoredLengthBytes), 0)
FROM PayloadShards;";
        using SQLiteDataReader shardReader = shardCommand.ExecuteReader();
        shardReader.Read();
        long shardCount = shardReader.GetInt64(0);
        long payloadBytes = shardReader.GetInt64(1);

        return (entryCount, publishedEntryCount, shardCount, payloadBytes);
    }

    private static TransientCachePayloadSegmentRecord ReadPayloadSegment(SQLiteDataReader reader)
        => new(
            reader.GetInt64(0),
            (TransientCachePayloadKind)reader.GetInt32(1),
            reader.GetString(2),
            reader.GetInt64(3),
            ParseTimestamp(reader.GetString(4)));

    private static TransientCacheSharedSequenceRecord ReadSharedSequence(SQLiteDataReader reader)
        => new(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetInt64(4),
            reader.GetInt32(5) != 0,
            reader.IsDBNull(6) ? null : reader.GetString(6),
            ParseTimestamp(reader.GetString(7)),
            reader.IsDBNull(8) ? null : ParseTimestamp(reader.GetString(8)));

    private static TransientCachePayloadShardRecord ReadPayloadShard(SQLiteDataReader reader)
        => new(
            reader.GetInt64(0),
            reader.GetInt64(1),
            (TransientCachePayloadKind)reader.GetInt32(2),
            reader.GetInt64(3),
            reader.GetInt64(4),
            reader.GetInt64(5),
            reader.GetString(6),
            reader.GetInt32(7),
            ParseTimestamp(reader.GetString(8)));

    private static TransientCacheResolvedShardReference ReadResolvedPayloadShard(SQLiteDataReader reader)
        => new(
            reader.GetInt64(0),
            (TransientCachePayloadKind)reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetString(3),
            reader.GetInt64(4),
            reader.GetInt64(5),
            reader.GetInt64(6),
            reader.GetString(7),
            reader.GetInt32(8));
}

using System.Collections.Generic;

namespace EngineLayer.ParallelSearch.PersistentCache.Manifest;

internal static class TransientCacheManifestSchema
{
    public static IReadOnlyList<string> CreateStatements { get; } =
    [
        @"CREATE TABLE IF NOT EXISTS SourceDatabases (
            DatabaseContentHash TEXT PRIMARY KEY,
            OriginalPath TEXT NULL,
            CreatedUtc TEXT NOT NULL
        );",

        @"CREATE TABLE IF NOT EXISTS CacheSettings (
            CacheSettingsId TEXT PRIMARY KEY,
            CanonicalSettingsPayload TEXT NOT NULL,
            CreatedUtc TEXT NOT NULL
        );",

        @"CREATE TABLE IF NOT EXISTS CacheEntries (
            DatabaseContentHash TEXT NOT NULL,
            CacheSettingsId TEXT NOT NULL,
            PublishState INTEGER NOT NULL,
            Detail TEXT NULL,
            ProteinCount INTEGER NULL,
            PeptideCount INTEGER NULL,
            EntryChecksum TEXT NULL,
            CreatedUtc TEXT NOT NULL,
            PublishedUtc TEXT NULL,
            PRIMARY KEY (DatabaseContentHash, CacheSettingsId),
            FOREIGN KEY (DatabaseContentHash) REFERENCES SourceDatabases(DatabaseContentHash) ON DELETE RESTRICT,
            FOREIGN KEY (CacheSettingsId) REFERENCES CacheSettings(CacheSettingsId) ON DELETE RESTRICT
        );",

        @"CREATE TABLE IF NOT EXISTS SharedSequences (
            SequenceId INTEGER PRIMARY KEY AUTOINCREMENT,
            CacheSettingsId TEXT NOT NULL,
            SequenceHash TEXT NOT NULL,
            FullSequence TEXT NOT NULL,
            FragmentShardId INTEGER NULL,
            IsQuarantined INTEGER NOT NULL DEFAULT 0,
            QuarantineReason TEXT NULL,
            CreatedUtc TEXT NOT NULL,
            QuarantinedUtc TEXT NULL,
            UNIQUE (CacheSettingsId, SequenceHash, FullSequence),
            FOREIGN KEY (CacheSettingsId) REFERENCES CacheSettings(CacheSettingsId) ON DELETE RESTRICT,
            FOREIGN KEY (FragmentShardId) REFERENCES PayloadShards(ShardId) ON DELETE RESTRICT
        );",

        @"CREATE TABLE IF NOT EXISTS PayloadSegments (
            SegmentId INTEGER PRIMARY KEY AUTOINCREMENT,
            PayloadKind INTEGER NOT NULL,
            RelativePath TEXT NOT NULL UNIQUE,
            LengthBytes INTEGER NOT NULL,
            CreatedUtc TEXT NOT NULL
        );",

        @"CREATE TABLE IF NOT EXISTS PayloadShards (
            ShardId INTEGER PRIMARY KEY AUTOINCREMENT,
            SegmentId INTEGER NOT NULL,
            PayloadKind INTEGER NOT NULL,
            OffsetBytes INTEGER NOT NULL,
            StoredLengthBytes INTEGER NOT NULL,
            LogicalLengthBytes INTEGER NOT NULL,
            Sha256 TEXT NOT NULL,
            ReferenceCount INTEGER NOT NULL,
            CreatedUtc TEXT NOT NULL,
            FOREIGN KEY (SegmentId) REFERENCES PayloadSegments(SegmentId) ON DELETE RESTRICT
        );",

        @"CREATE TABLE IF NOT EXISTS CacheEntryShards (
            DatabaseContentHash TEXT NOT NULL,
            CacheSettingsId TEXT NOT NULL,
            PayloadKind INTEGER NOT NULL,
            Ordinal INTEGER NOT NULL,
            ShardId INTEGER NOT NULL,
            PRIMARY KEY (DatabaseContentHash, CacheSettingsId, PayloadKind, Ordinal),
            FOREIGN KEY (DatabaseContentHash, CacheSettingsId) REFERENCES CacheEntries(DatabaseContentHash, CacheSettingsId) ON DELETE CASCADE,
            FOREIGN KEY (ShardId) REFERENCES PayloadShards(ShardId) ON DELETE RESTRICT
        );",

        @"CREATE TABLE IF NOT EXISTS CacheEntrySequences (
            DatabaseContentHash TEXT NOT NULL,
            CacheSettingsId TEXT NOT NULL,
            LocalOrdinal INTEGER NOT NULL,
            SequenceId INTEGER NOT NULL,
            PRIMARY KEY (DatabaseContentHash, CacheSettingsId, LocalOrdinal),
            FOREIGN KEY (DatabaseContentHash, CacheSettingsId) REFERENCES CacheEntries(DatabaseContentHash, CacheSettingsId) ON DELETE CASCADE,
            FOREIGN KEY (SequenceId) REFERENCES SharedSequences(SequenceId) ON DELETE RESTRICT
        );",

        @"CREATE INDEX IF NOT EXISTS IX_CacheEntries_PublishState
            ON CacheEntries(PublishState);",

        @"CREATE INDEX IF NOT EXISTS IX_SharedSequences_CacheSettingsId_SequenceHash
            ON SharedSequences(CacheSettingsId, SequenceHash);",

        @"CREATE INDEX IF NOT EXISTS IX_SharedSequences_FragmentShardId
            ON SharedSequences(FragmentShardId);",

        @"CREATE INDEX IF NOT EXISTS IX_PayloadShards_PayloadKind_Sha256_LogicalLengthBytes
            ON PayloadShards(PayloadKind, Sha256, LogicalLengthBytes);",

        @"CREATE INDEX IF NOT EXISTS IX_CacheEntryShards_ShardId
            ON CacheEntryShards(ShardId);",

        @"CREATE INDEX IF NOT EXISTS IX_CacheEntrySequences_SequenceId
            ON CacheEntrySequences(SequenceId);"
    ];
}

using System;
using System.IO;
using EngineLayer.ParallelSearch.PersistentCache;
using EngineLayer.ParallelSearch.PersistentCache.Manifest;
using EngineLayer.ParallelSearch.PersistentCache.Payloads;
using NUnit.Framework;

namespace Test.ParallelSearchTask.PersistentCache;

[TestFixture]
public class TransientCacheManifestStoreTests
{
    [Test]
    public void InitializeAndUpserts_RoundTripPublishedEntryAndShardMappings()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            string manifestPath = Path.Combine(tempDirectory, "manifest.sqlite");
            TransientCacheManifestStore store = new(manifestPath);
            store.Initialize();

            TransientCacheKey key = new(new string('d', 64), new string('s', 64));
            store.UpsertSourceDatabase(key.DatabaseContentHash, "small-db.fasta");
            store.UpsertCacheSettings(key.CacheSettingsId, "canonical-settings");

            TransientCachePayloadSegmentRecord segment = store.UpsertPayloadSegment(
                TransientCachePayloadKind.Fragment,
                Path.Combine("fragments", "segment-000001.bin"));

            string shardChecksum = new string('a', 64);
            TransientCachePayloadShardRecord shard = store.InsertPayloadShard(
                segment.SegmentId,
                TransientCachePayloadKind.Fragment,
                offsetBytes: 128,
                storedLengthBytes: 192,
                logicalLengthBytes: 100,
                shardChecksum);

            store.UpsertCacheEntry(new TransientCacheManifestEntry(key, TransientCachePublishState.Published)
            {
                ProteinCount = 12,
                PeptideCount = 45,
                EntryChecksum = new string('b', 64),
                Detail = "ready"
            });

            store.ReplaceEntryShards(key,
            [
                new TransientCacheEntryShardReference(shard.ShardId, TransientCachePayloadKind.Fragment, 0)
            ]);

            TransientCacheManifestEntry? roundTripEntry = store.TryGetPublishedCacheEntry(key);
            var roundTripShard = store.TryGetPayloadShardByFingerprint(TransientCachePayloadKind.Fragment, shardChecksum, 100);
            var resolvedReferences = store.GetResolvedEntryShardReferences(key);

            Assert.Multiple(() =>
            {
                Assert.That(roundTripEntry, Is.Not.Null);
                Assert.That(roundTripEntry!.ProteinCount, Is.EqualTo(12));
                Assert.That(roundTripEntry.PeptideCount, Is.EqualTo(45));
                Assert.That(roundTripEntry.Detail, Is.EqualTo("ready"));
                Assert.That(roundTripShard, Is.Not.Null);
                Assert.That(roundTripShard!.Value.ShardId, Is.EqualTo(shard.ShardId));
                Assert.That(resolvedReferences, Has.Count.EqualTo(1));
                Assert.That(resolvedReferences[0].RelativePath, Is.EqualTo(Path.Combine("fragments", "segment-000001.bin")));
                Assert.That(resolvedReferences[0].Sha256, Is.EqualTo(shardChecksum));
            });
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Test]
    public void SharedSequences_RoundTripLocalOrdinalsAndQuarantineState()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            string manifestPath = Path.Combine(tempDirectory, "manifest.sqlite");
            TransientCacheManifestStore store = new(manifestPath);
            store.Initialize();

            TransientCacheKey key = new(new string('d', 64), new string('s', 64));
            store.UpsertSourceDatabase(key.DatabaseContentHash, "small-db.fasta");
            store.UpsertCacheSettings(key.CacheSettingsId, "canonical-settings");
            store.UpsertCacheEntry(new TransientCacheManifestEntry(key, TransientCachePublishState.Published));

            TransientCachePayloadSegmentRecord fragmentSegment = store.UpsertPayloadSegment(
                TransientCachePayloadKind.Fragment,
                Path.Combine("fragments", "segment-000001.bin"));

            TransientCachePayloadShardRecord fragmentShard = store.InsertPayloadShard(
                fragmentSegment.SegmentId,
                TransientCachePayloadKind.Fragment,
                offsetBytes: 48,
                storedLengthBytes: 144,
                logicalLengthBytes: 80,
                new string('f', 64));

            string sequenceHash = new string('q', 64);
            TransientCacheSharedSequenceRecord sequence = store.UpsertSharedSequence(
                key.CacheSettingsId,
                sequenceHash,
                "PEPTIDE",
                fragmentShard.ShardId);

            store.ReplaceEntrySequences(key,
            [
                new TransientCacheEntrySequenceReference(sequence.SequenceId, 0)
            ]);

            store.QuarantineSharedSequence(sequence.SequenceId, "checksum mismatch");

            TransientCacheSharedSequenceRecord? exactSequence = store.TryGetSharedSequence(key.CacheSettingsId, sequenceHash, "PEPTIDE", includeQuarantined: true);
            var resolvedSequences = store.GetResolvedEntrySequenceReferences(key);

            Assert.Multiple(() =>
            {
                Assert.That(exactSequence, Is.Not.Null);
                Assert.That(exactSequence!.IsQuarantined, Is.True);
                Assert.That(exactSequence.QuarantineReason, Is.EqualTo("checksum mismatch"));
                Assert.That(exactSequence.FragmentShardId, Is.EqualTo(fragmentShard.ShardId));
                Assert.That(resolvedSequences, Has.Count.EqualTo(1));
                Assert.That(resolvedSequences[0].LocalOrdinal, Is.EqualTo(0));
                Assert.That(resolvedSequences[0].FullSequence, Is.EqualTo("PEPTIDE"));
                Assert.That(resolvedSequences[0].FragmentShardId, Is.EqualTo(fragmentShard.ShardId));
                Assert.That(resolvedSequences[0].IsQuarantined, Is.True);
            });
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Test]
    public void TryGetLatestPayloadSegment_ReturnsNewestSegmentWithinSizeLimit()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            string manifestPath = Path.Combine(tempDirectory, "manifest.sqlite");
            TransientCacheManifestStore store = new(manifestPath);
            store.Initialize();

            store.UpsertPayloadSegment(TransientCachePayloadKind.Occurrence, Path.Combine("occurrence", "segment-000001.bin"), lengthBytes: 64);
            store.UpsertPayloadSegment(TransientCachePayloadKind.Occurrence, Path.Combine("occurrence", "segment-000002.bin"), lengthBytes: 96);
            store.UpsertPayloadSegment(TransientCachePayloadKind.Occurrence, Path.Combine("occurrence", "segment-000003.bin"), lengthBytes: 160);

            TransientCachePayloadSegmentRecord? unrestricted = store.TryGetLatestPayloadSegment(TransientCachePayloadKind.Occurrence);
            TransientCachePayloadSegmentRecord? capped = store.TryGetLatestPayloadSegment(TransientCachePayloadKind.Occurrence, maxLengthBytes: 100);

            Assert.Multiple(() =>
            {
                Assert.That(unrestricted, Is.Not.Null);
                Assert.That(unrestricted!.Value.RelativePath, Does.EndWith("segment-000003.bin"));
                Assert.That(capped, Is.Not.Null);
                Assert.That(capped!.Value.RelativePath, Does.EndWith("segment-000002.bin"));
            });
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Test]
    public void AdjustPayloadShardReferenceCount_TracksAndRejectsNegativeCounts()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            string manifestPath = Path.Combine(tempDirectory, "manifest.sqlite");
            TransientCacheManifestStore store = new(manifestPath);
            store.Initialize();

            TransientCachePayloadSegmentRecord segment = store.UpsertPayloadSegment(
                TransientCachePayloadKind.Occurrence,
                Path.Combine("occurrence", "segment-000001.bin"));

            TransientCachePayloadShardRecord shard = store.InsertPayloadShard(
                segment.SegmentId,
                TransientCachePayloadKind.Occurrence,
                offsetBytes: 0,
                storedLengthBytes: 96,
                logicalLengthBytes: 4,
                new string('c', 64));

            int afterIncrement = store.AdjustPayloadShardReferenceCount(shard.ShardId, 3);
            int afterDecrement = store.AdjustPayloadShardReferenceCount(shard.ShardId, -2);

            Assert.Multiple(() =>
            {
                Assert.That(afterIncrement, Is.EqualTo(3));
                Assert.That(afterDecrement, Is.EqualTo(1));
                Assert.That(() => store.AdjustPayloadShardReferenceCount(shard.ShardId, -2), Throws.TypeOf<InvalidOperationException>());
            });
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Test]
    public void StorageLayout_CreateDefaultAndCreate_AreSchemaAware()
    {
        TransientCacheStorageLayout defaultLayout = TransientCacheStorageLayout.CreateDefault();
        TransientCacheStorageLayout customLayout = TransientCacheStorageLayout.Create(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.Multiple(() =>
        {
            Assert.That(defaultLayout.RootDirectory, Does.Contain(TransientCacheSchema.GetSchemaTag()));
            Assert.That(defaultLayout.ManifestPath, Does.EndWith(TransientCacheSchema.ManifestFileName));
            Assert.That(customLayout.PayloadDirectory, Does.EndWith(TransientCacheSchema.PayloadDirectoryName));
        });
    }

    private static string CreateTempDirectory()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }
}

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
    public void AdjustPayloadShardReferenceCount_TracksAndRejectsNegativeCounts()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            string manifestPath = Path.Combine(tempDirectory, "manifest.sqlite");
            TransientCacheManifestStore store = new(manifestPath);
            store.Initialize();

            TransientCachePayloadSegmentRecord segment = store.UpsertPayloadSegment(
                TransientCachePayloadKind.Peptidoform,
                Path.Combine("peptidoforms", "segment-000001.bin"));

            TransientCachePayloadShardRecord shard = store.InsertPayloadShard(
                segment.SegmentId,
                TransientCachePayloadKind.Peptidoform,
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

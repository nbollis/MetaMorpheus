using System;
using System.IO;
using System.Text;
using EngineLayer.ParallelSearch.PersistentCache;
using EngineLayer.ParallelSearch.PersistentCache.Manifest;
using EngineLayer.ParallelSearch.PersistentCache.Payloads;
using NUnit.Framework;

namespace Test.ParallelSearchTask.PersistentCache;

[TestFixture]
public class TransientCachePayloadSegmentTests
{
    [Test]
    public void AppendAndReadShard_RoundTripsPayloadsAndOffsets()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            string segmentPath = Path.Combine(tempDirectory, "segment.bin");
            TransientCachePayloadSegmentWriter writer = new();
            TransientCachePayloadSegmentReader reader = new();

            byte[] firstPayload = Encoding.UTF8.GetBytes("PEPTIDE");
            byte[] secondPayload = Encoding.UTF8.GetBytes("FRAGMENTS");

            TransientCachePayloadWriteResult firstWrite = writer.AppendShard(segmentPath, TransientCachePayloadKind.Occurrence, firstPayload);
            TransientCachePayloadWriteResult secondWrite = writer.AppendShard(segmentPath, TransientCachePayloadKind.Fragment, secondPayload);

            byte[] firstRoundTrip = reader.ReadShard(
                segmentPath,
                firstWrite.OffsetBytes,
                firstWrite.StoredLengthBytes,
                TransientCachePayloadKind.Occurrence,
                firstWrite.LogicalLengthBytes,
                firstWrite.Sha256);

            byte[] secondRoundTrip = reader.ReadShard(segmentPath, new TransientCacheResolvedShardReference(
                shardId: 2,
                payloadKind: TransientCachePayloadKind.Fragment,
                ordinal: 0,
                relativePath: "segment.bin",
                offsetBytes: secondWrite.OffsetBytes,
                storedLengthBytes: secondWrite.StoredLengthBytes,
                logicalLengthBytes: secondWrite.LogicalLengthBytes,
                sha256: secondWrite.Sha256,
                referenceCount: 1));

            Assert.Multiple(() =>
            {
                Assert.That(firstWrite.OffsetBytes, Is.EqualTo(0));
                Assert.That(secondWrite.OffsetBytes, Is.EqualTo(firstWrite.StoredLengthBytes));
                Assert.That(firstRoundTrip, Is.EqualTo(firstPayload));
                Assert.That(secondRoundTrip, Is.EqualTo(secondPayload));
            });
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Test]
    public void ReadShard_WhenPayloadIsCorrupted_ThrowsInvalidDataException()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            string segmentPath = Path.Combine(tempDirectory, "segment.bin");
            TransientCachePayloadSegmentWriter writer = new();
            TransientCachePayloadSegmentReader reader = new();

            byte[] payload = Encoding.UTF8.GetBytes("ABCDEFGHIJ");
            TransientCachePayloadWriteResult writeResult = writer.AppendShard(segmentPath, TransientCachePayloadKind.Fragment, payload);

            using (FileStream stream = new(segmentPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                stream.Seek(writeResult.OffsetBytes + TransientCachePayloadHeader.SerializedLength, SeekOrigin.Begin);
                int originalByte = stream.ReadByte();
                stream.Seek(-1, SeekOrigin.Current);
                stream.WriteByte((byte)(originalByte ^ 0xFF));
            }

            Assert.That(() => reader.ReadShard(
                    segmentPath,
                    writeResult.OffsetBytes,
                    writeResult.StoredLengthBytes,
                    TransientCachePayloadKind.Fragment,
                    writeResult.LogicalLengthBytes,
                    writeResult.Sha256),
                Throws.TypeOf<InvalidDataException>());
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Test]
    public void SegmentManager_ReusesSegmentWithinCapAndTracksTrueLength()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            TransientCacheStorageLayout layout = TransientCacheStorageLayout.Create(tempDirectory);
            layout.EnsureDirectoriesExist();

            TransientCacheManifestStore store = new(layout.ManifestPath);
            store.Initialize();

            TransientCacheSegmentManager manager = new(store, layout);
            byte[] firstPayload = Encoding.UTF8.GetBytes("FIRST");
            byte[] secondPayload = Encoding.UTF8.GetBytes("SECOND");

            TransientCacheSegmentAppendResult firstAppend = manager.AppendPayloadShard(TransientCachePayloadKind.Occurrence, firstPayload);
            TransientCacheSegmentAppendResult secondAppend = manager.AppendPayloadShard(TransientCachePayloadKind.Peptidoform, secondPayload);

            TransientCachePayloadSegmentRecord? latestOccurrenceSegment = store.TryGetLatestPayloadSegment(TransientCachePayloadKind.Occurrence);

            Assert.Multiple(() =>
            {
                Assert.That(firstAppend.Segment.RelativePath, Is.EqualTo(secondAppend.Segment.RelativePath));
                Assert.That(secondAppend.WriteResult.OffsetBytes, Is.EqualTo(firstAppend.WriteResult.StoredLengthBytes));
                Assert.That(latestOccurrenceSegment, Is.Not.Null);
                Assert.That(latestOccurrenceSegment!.Value.LengthBytes, Is.EqualTo(secondAppend.Segment.LengthBytes));
            });
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Test]
    public void SegmentManager_SeparatesOccurrenceAndFragmentFamilies()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            TransientCacheStorageLayout layout = TransientCacheStorageLayout.Create(tempDirectory);
            layout.EnsureDirectoriesExist();

            TransientCacheManifestStore store = new(layout.ManifestPath);
            store.Initialize();

            TransientCacheSegmentManager manager = new(store, layout);
            TransientCacheSegmentAppendResult occurrenceAppend = manager.AppendPayloadShard(TransientCachePayloadKind.Occurrence, Encoding.UTF8.GetBytes("OCC"));
            TransientCacheSegmentAppendResult fragmentAppend = manager.AppendPayloadShard(TransientCachePayloadKind.Fragment, Encoding.UTF8.GetBytes("FRAG"));

            Assert.Multiple(() =>
            {
                Assert.That(occurrenceAppend.Segment.RelativePath, Does.StartWith($"occurrence{Path.DirectorySeparatorChar}"));
                Assert.That(fragmentAppend.Segment.RelativePath, Does.StartWith($"fragment{Path.DirectorySeparatorChar}"));
                Assert.That(occurrenceAppend.Segment.RelativePath, Is.Not.EqualTo(fragmentAppend.Segment.RelativePath));
            });
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Test]
    public void SegmentManager_RollsOverWhenExistingSegmentWouldExceedCap()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            TransientCacheStorageLayout layout = TransientCacheStorageLayout.Create(tempDirectory);
            layout.EnsureDirectoriesExist();

            TransientCacheManifestStore store = new(layout.ManifestPath);
            store.Initialize();

            string existingRelativePath = Path.Combine("occurrence", "segment-000001.bin");
            store.UpsertPayloadSegment(
                TransientCachePayloadKind.Occurrence,
                existingRelativePath,
                lengthBytes: TransientCacheSegmentManager.DefaultOccurrenceSegmentMaxBytes - 4);

            TransientCacheSegmentManager manager = new(store, layout);
            TransientCacheSegmentAppendResult append = manager.AppendPayloadShard(TransientCachePayloadKind.Occurrence, Encoding.UTF8.GetBytes("THIS-PAYLOAD-FORCES-ROLLOVER"));

            Assert.That(append.Segment.RelativePath, Does.EndWith("segment-000002.bin"));
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    private static string CreateTempDirectory()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }
}

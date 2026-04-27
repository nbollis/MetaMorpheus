using System;
using System.IO;
using System.Text;
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

    private static string CreateTempDirectory()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }
}

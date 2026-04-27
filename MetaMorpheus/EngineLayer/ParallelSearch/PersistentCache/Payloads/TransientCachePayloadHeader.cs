using System;
using System.IO;
using System.Text;

namespace EngineLayer.ParallelSearch.PersistentCache.Payloads;

public sealed record TransientCachePayloadHeader(
    TransientCachePayloadKind PayloadKind,
    long LogicalLengthBytes,
    string Sha256)
{
    public const int MagicLength = 8;
    public const int ChecksumLength = 64;
    public const int SerializedLength = MagicLength + sizeof(int) + sizeof(int) + sizeof(long) + sizeof(int) + ChecksumLength;

    public void Write(BinaryWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.Write(Encoding.ASCII.GetBytes(TransientCacheSchema.PayloadHeaderMagic));
        writer.Write(TransientCacheSchema.PayloadHeaderVersion);
        writer.Write((int)PayloadKind);
        writer.Write(LogicalLengthBytes);
        writer.Write(ChecksumLength);
        writer.Write(EncodeChecksum(Sha256));
    }

    public static TransientCachePayloadHeader Read(BinaryReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        byte[] magicBytes = reader.ReadBytes(MagicLength);
        if (magicBytes.Length != MagicLength)
        {
            throw new InvalidDataException("Transient cache payload header is truncated.");
        }

        string magic = Encoding.ASCII.GetString(magicBytes);
        if (!string.Equals(magic, TransientCacheSchema.PayloadHeaderMagic, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unexpected transient cache payload magic '{magic}'.");
        }

        int headerVersion = reader.ReadInt32();
        if (headerVersion != TransientCacheSchema.PayloadHeaderVersion)
        {
            throw new InvalidDataException($"Unsupported transient cache payload header version '{headerVersion}'.");
        }

        TransientCachePayloadKind payloadKind = (TransientCachePayloadKind)reader.ReadInt32();
        long logicalLengthBytes = reader.ReadInt64();
        int checksumLength = reader.ReadInt32();
        if (checksumLength != ChecksumLength)
        {
            throw new InvalidDataException($"Unexpected transient cache payload checksum length '{checksumLength}'.");
        }

        byte[] checksumBytes = reader.ReadBytes(checksumLength);
        if (checksumBytes.Length != checksumLength)
        {
            throw new InvalidDataException("Transient cache payload checksum is truncated.");
        }

        return new TransientCachePayloadHeader(
            payloadKind,
            logicalLengthBytes,
            Encoding.ASCII.GetString(checksumBytes));
    }

    private static byte[] EncodeChecksum(string sha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        if (sha256.Length != ChecksumLength)
        {
            throw new ArgumentException($"Transient cache payload checksum must be {ChecksumLength} characters.", nameof(sha256));
        }

        return Encoding.ASCII.GetBytes(sha256);
    }
}

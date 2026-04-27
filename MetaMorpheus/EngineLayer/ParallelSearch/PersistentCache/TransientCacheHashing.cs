using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace EngineLayer.ParallelSearch.PersistentCache;

public static class TransientCacheHashing
{
    public static string ComputeDatabaseContentHash(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using var stream = File.OpenRead(filePath);
        return ComputeSha256Hex(stream);
    }

    public static string ComputeCacheSettingsId(string canonicalSettingsPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalSettingsPayload);
        return ComputeSha256Hex(Encoding.UTF8.GetBytes(canonicalSettingsPayload));
    }

    public static string ComputeSha256Hex(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static string ComputeSha256Hex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}

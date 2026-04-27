using System;

namespace EngineLayer.ParallelSearch.PersistentCache;

public static class TransientCacheSchema
{
    public const int CurrentSchemaVersion = 1;
    public const int PayloadHeaderVersion = 1;
    public const string HashAlgorithmName = "SHA-256";
    public const string MessagePrefix = "[TransientCache]";
    public const string ManifestFileName = "manifest.sqlite";
    public const string PayloadDirectoryName = "payloads";
    public const string PayloadHeaderMagic = "MMTCPAY1";

    public static string GetSchemaTag()
    {
        return $"v{CurrentSchemaVersion}";
    }
}

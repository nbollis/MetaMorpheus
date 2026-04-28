using System;

namespace EngineLayer.ParallelSearch.PersistentCache;

internal static class TransientCacheSchema
{
    // Schema version 2 freezes the V2 storage contract: DB-local occurrence payloads,
    // settings-scoped shared fragment reuse by FullSequence, and shared append-only segments.
    public const int CurrentSchemaVersion = 2;
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

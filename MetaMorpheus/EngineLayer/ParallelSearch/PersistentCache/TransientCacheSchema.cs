using System;

namespace EngineLayer.ParallelSearch.PersistentCache;

public static class TransientCacheSchema
{
    public const int CurrentSchemaVersion = 1;
    public const string HashAlgorithmName = "SHA-256";
    public const string MessagePrefix = "[TransientCache]";

    public static string GetSchemaTag()
    {
        return $"v{CurrentSchemaVersion}";
    }
}

namespace EngineLayer.ParallelSearch.PersistentCache;

internal enum TransientCacheLookupOutcome
{
    Hit,
    Miss,
    SettingsMismatch,
    Corrupt,
    IdentityMismatch,
    Disabled,
}

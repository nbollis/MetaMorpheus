namespace EngineLayer.ParallelSearch.PersistentCache;

public enum TransientCacheLookupOutcome
{
    Hit,
    Miss,
    SettingsMismatch,
    Corrupt,
    IdentityMismatch,
    Disabled,
}

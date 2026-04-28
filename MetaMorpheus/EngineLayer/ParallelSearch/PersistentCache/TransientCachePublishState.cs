namespace EngineLayer.ParallelSearch.PersistentCache;

internal enum TransientCachePublishState
{
    Pending = 0,
    Published = 1,
    Failed = 2,
    Corrupt = 3,
}

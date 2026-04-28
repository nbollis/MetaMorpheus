namespace EngineLayer.ParallelSearch.PersistentCache;

internal sealed class TransientCachePublishResult
{
    public TransientCachePublishState PublishState { get; }
    public string? Detail { get; }

    public bool IsSuccess => PublishState == TransientCachePublishState.Published;

    private TransientCachePublishResult(TransientCachePublishState publishState, string? detail)
    {
        PublishState = publishState;
        Detail = detail;
    }

    public static TransientCachePublishResult Success()
        => new(TransientCachePublishState.Published, null);

    public static TransientCachePublishResult Failure(string detail)
        => new(TransientCachePublishState.Failed, detail);
}

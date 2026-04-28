using System.Collections.Generic;
using Omics;

namespace EngineLayer.ParallelSearch.PersistentCache;

internal sealed class TransientCacheHydrationResult
{
    public TransientCacheLookupOutcome Outcome { get; }
    public string? Detail { get; }
    public IReadOnlyList<IBioPolymer>? HydratedBioPolymers { get; }

    public bool IsSuccess => HydratedBioPolymers is not null;

    private TransientCacheHydrationResult(
        TransientCacheLookupOutcome outcome,
        string? detail,
        IReadOnlyList<IBioPolymer>? hydratedBioPolymers)
    {
        Outcome = outcome;
        Detail = detail;
        HydratedBioPolymers = hydratedBioPolymers;
    }

    public static TransientCacheHydrationResult Success(IReadOnlyList<IBioPolymer> hydratedBioPolymers)
        => new(TransientCacheLookupOutcome.Hit, null, hydratedBioPolymers);

    public static TransientCacheHydrationResult Failure(TransientCacheLookupOutcome outcome, string detail)
        => new(outcome, detail, null);

    public static TransientCacheHydrationResult NotApplicable()
        => new(TransientCacheLookupOutcome.Disabled, null, null);
}

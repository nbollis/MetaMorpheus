# TransientDatabaseCache Refactor Plan

## Goal

Replace the current per-engine cache construction model with a shared cache session owned by `ParallelSearchTask`.

The new design should make `TransientDatabaseLoadingEngine` cache-backed by construction, remove the `useCache` toggle from cache APIs, and let one shared cache instance amortize manifest/index work across many transient DB loads in a single task run.

## Locked Design Decisions

- Cache lifetime: one shared `TransientDatabaseCache` per `ParallelSearchTask` run.
- Cache owner: `ParallelSearchTask`.
- Cache use model: `TransientDatabaseLoadingEngine` always assumes a cache object is present.
- Uncached behavior: use `DatabaseLoadingEngine` directly, except `TransientDatabaseLoadingEngine` may still fall back to `base.RunSpecific()` when cache retrieval or hydrate fails.
- Engine/cache contract: the engine requires a shared cache parameter in its constructor.
- DB resolution model: the loader receives the shared cache object and resolves its own DB path through that cache.
- Handle shape: `Probe`/`Resolve` returns a per-DB handle from the shared cache.
- Prewarm: synchronous before parallel transient DB work starts.
- Prewarm contents: manifest/index layer, DB content hashes, and per-DB memoized handles.
- Shared in-memory state: metadata indexes only, not large hydrated payloads or retained fragment tables.
- Global reuse indexes: memoize lazily after first use.
- Concurrency: concurrent reads, serialized writes.
- Manifest access: one long-lived manifest/index layer for the task run.
- Handle evolution: after publish, a miss handle upgrades in place to a reusable handle.
- Telemetry scope: shared cache owns aggregate run telemetry.
- Disposal: shared cache is explicitly disposed at the end of the task run.

## Problems In The Current Shape

The current refactor landed on a cleaner internal split, but it still has the wrong ownership model:

- `TransientDatabaseLoadingEngine` constructs `TransientDatabaseCache` itself.
- `TransientDatabaseCache` is bound to one `_dbFilePath`.
- `TransientDatabaseCache.TryLookup(bool useCache)` mixes policy with cache behavior.
- The cache object lifetime is per loader, not per task run.
- Manifest/index reuse across many transient DBs in one task run is weaker than it should be.

This means we still pay repeated setup cost and do not fully capture the runtime/GC advantages of a shared cache session.

## Target Shape

### Public surface

- `MetaMorpheus/EngineLayer/ParallelSearch/TransientDatabaseLoadingEngine.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientDatabaseCache.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientCacheTelemetry.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/Manifest/TransientCacheGrowthSummary` (currently in `TransientCacheManifestModels.cs`)

### Internal cache surface

- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientCacheHandle.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientCacheHydrationResult.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientCachePublishResult.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientCacheHydrator.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientCachePublisher.cs`
- manifest/payload implementation types under `PersistentCache/**`

### Runtime ownership

1. `ParallelSearchTask` constructs one `TransientDatabaseCache` for the run.
2. `ParallelSearchTask` calls `Prewarm(...)` synchronously before starting parallel transient DB work.
3. `ParallelSearchTask` passes the shared cache into each `TransientDatabaseLoadingEngine`.
4. Each loader resolves its own DB path through the shared cache.
5. `ParallelSearchTask` disposes the shared cache when the run is finished.

### Engine flow after revision

1. `TransientDatabaseLoadingEngine.RunSpecific()` calls `_cache.Resolve(_dbFilePath)`.
2. If the returned handle is reusable:
   - engine loads raw proteins once via `base.RunSpecific()`
   - engine calls `_cache.TryHydrate(handle, rawProteins)`
   - on hydrate success, returns hydrated results
   - on hydrate failure, reuses the already-loaded raw proteins as fallback
3. If the handle is a miss or rejected:
   - engine runs `base.RunSpecific()` once
4. Engine calls `_cache.TryPublish(handle, rawProteins)` after the fallback/raw path succeeds.
5. Engine remains the only caller of `Status(...)` and `Warn(...)`.

## Proposed Shared Cache API

```csharp
public sealed class TransientDatabaseCache : IDisposable
{
    public TransientCacheTelemetry Telemetry { get; }

    public void Prewarm(IEnumerable<string> dbFilePaths);

    public TransientCacheHandle Resolve(string dbFilePath);

    public TransientCacheHydrationResult TryHydrate(
        TransientCacheHandle handle,
        IReadOnlyList<IBioPolymer> rawProteins);

    public TransientCachePublishResult TryPublish(
        TransientCacheHandle handle,
        IReadOnlyList<IBioPolymer> rawProteins);

    public TransientCacheGrowthSummary GetGrowthSummary();
}
```

## Commit Sequence

## Commit 1

### Title
`ParallelSearch: make transient cache a shared session`

### Files to add
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientCacheHandle.cs`

### Files to modify
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientDatabaseCache.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/TransientDatabaseLoadingEngine.cs`
- `MetaMorpheus/TaskLayer/ParallelSearch/ParallelSearchTask.cs`
- `MetaMorpheus/Test/ParallelSearchTask/PersistentCache/TransientDatabaseLoadingEngineTests.cs`

### Exact steps
- Change `TransientDatabaseCache` from DB-bound construction to shared-session construction:
  - remove `_dbFilePath` field
  - remove `bool useCache` from cache-facing APIs
  - stop treating the cache as optional or disabled internally
- Add per-run shared-session state inside `TransientDatabaseCache`:
  - long-lived manifest/index layer
  - in-memory map keyed by DB path to memoized per-DB handles
- Replace `TransientCacheContext` / `TransientCacheLookupResult` with per-DB session state on `TransientCacheHandle`.
- Add `Resolve(string dbFilePath)` on `TransientDatabaseCache`.
- Modify `TransientDatabaseLoadingEngine` so it requires a `TransientDatabaseCache` constructor parameter.
- Remove `_useCache` from `TransientDatabaseLoadingEngine`.
- Remove `_cache = new TransientDatabaseCache(...)` from the engine.
- Update `ParallelSearchTask` so it constructs one shared `TransientDatabaseCache` per run and passes it into every transient DB loader.

### Engine end state after commit 1
- Loader is always cache-backed.
- Loader asks shared cache to resolve its DB path.
- Engine no longer decides whether cache exists.

## Commit 2

### Title
`ParallelSearch: add shared cache prewarm and handle memoization`

### Files to modify
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientDatabaseCache.cs`
- `MetaMorpheus/TaskLayer/ParallelSearch/ParallelSearchTask.cs`
- `MetaMorpheus/Test/ParallelSearchTask/PersistentCache/TransientDatabaseLoadingEngineTests.cs`

### Exact steps
- Add `Prewarm(IEnumerable<string> dbFilePaths)` to `TransientDatabaseCache`.
- Implement synchronous prewarm that:
  - initializes the long-lived manifest/index layer
  - computes DB content hashes for the provided paths
  - builds and stores memoized per-DB handles keyed by DB path
- Make `Resolve(dbFilePath)` return the already-memoized handle when available.
- Memoize both hits and misses.
- After publish succeeds, upgrade a miss handle in place to reusable hit-state.
- Call `Prewarm(...)` from `ParallelSearchTask` before parallel transient DB processing starts.

### Notes
- Prewarm should not eagerly load large payloads.
- Prewarm should not fully mirror global reuse indexes in memory.
- Keep lazy memoization of global reuse indexes for later publish paths.

## Commit 3

### Title
`ParallelSearch: align loader orchestration with shared cache session`

### Files to modify
- `MetaMorpheus/EngineLayer/ParallelSearch/TransientDatabaseLoadingEngine.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientDatabaseCache.cs`
- `MetaMorpheus/Test/ParallelSearchTask/PersistentCache/TransientDatabaseLoadingEngineTests.cs`

### Exact steps
- Replace `TryLookup(...)`-based orchestration with handle-based orchestration in `RunSpecific()`.
- Engine flow should become:
  1. `var handle = _cache.Resolve(_dbFilePath);`
  2. if handle reusable: raw base load once, then hydrate
  3. on hydrate failure: reuse already-loaded raw proteins
  4. on miss/rejected handle: raw base load once
  5. publish through `_cache.TryPublish(handle, rawProteins)`
- Keep engine-owned `Status(...)` and `Warn(...)` composition.
- Preserve current fallback semantics:
  - if cache retrieval or hydrate fails, use `base.RunSpecific()`
  - do not load raw proteins twice in the same run path
- Keep telemetry aggregated on `_cache.Telemetry`.

## Commit 4

### Title
`ParallelSearch: finalize shared cache lifetime and docs`

### Files to modify
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientDatabaseCache.cs`
- `MetaMorpheus/TaskLayer/ParallelSearch/ParallelSearchTask.cs`
- `MetaMorpheus/TaskLayer/ParallelSearch/TransientPersistentCachePlan.md`
- `MetaMorpheus/TaskLayer/ParallelSearch/TransientDatabaseCacheRefactorPlan.md`
- `MetaMorpheus/Test/ParallelSearchTask/PersistentCache/*.cs` as needed

### Exact steps
- Make `TransientDatabaseCache` explicitly disposable.
- Ensure `ParallelSearchTask` disposes the shared cache at the end of the run.
- Add or tighten any internal synchronization needed for:
  - concurrent reads
  - serialized writes
  - lazy memoization of global reuse indexes
- Review visibility after the ownership shift:
  - keep public only the intentional surface
  - keep helper DTOs and manifest/payload details internal
- Update `TransientPersistentCachePlan.md` to document the final runtime ownership model.
- Update this refactor plan file with completion status.

## File-By-File Method Map

### Stay in `TransientDatabaseLoadingEngine.cs`
- constructors
- `RunSpecific()`
- engine-only status/warn formatting if any small helpers remain

### Stay in `TransientDatabaseCache.cs`
- `Prewarm(...)`
- `Resolve(...)`
- `TryHydrate(...)`
- `TryPublish(...)`
- `GetGrowthSummary()`
- `Dispose()`
- shared-session memoization and long-lived manifest/index ownership

### Stay in `TransientCacheHydrator.cs`
- `TryHydrate(...)`
- `CreateLegacyFragmentResolver(...)`
- `CreateSharedFragmentResolver(...)`
- `LoadSharedFragmentPayload(...)`

### Stay in `TransientCachePublisher.cs`
- `TryPublish(...)`
- `BuildDbLocalOccurrencePayload(...)`
- `PublishOccurrenceShard(...)`
- `PublishSharedFragmentShards(...)`
- `ResolveOrPublishFragmentShard(...)`
- `ResolveSelectedMods(...)`
- `GetLocalSequenceKey(...)`
- `ComputeSharedSequenceHash(...)`

## Acceptance Criteria

- `TransientDatabaseLoadingEngine` no longer constructs its own cache object.
- `TransientDatabaseLoadingEngine` no longer has `_useCache`.
- `TransientDatabaseCache` no longer takes one fixed DB path in its constructor.
- Cache APIs do not take `bool useCache`.
- `ParallelSearchTask` owns one shared cache session for the run.
- `ParallelSearchTask` prewarms that session synchronously before parallel transient DB work.
- Loaders resolve DB-local handles through the shared cache.
- Miss handles upgrade in place after publish.
- Telemetry is aggregated at the shared-cache/run level.
- The shared cache is disposed explicitly at end of run.

## Current Status

- [x] Commit 1: make transient cache a shared session
- [x] Commit 2: add shared cache prewarm and handle memoization
- [x] Commit 3: align loader orchestration with shared cache session
- [x] Commit 4: finalize shared cache lifetime and docs

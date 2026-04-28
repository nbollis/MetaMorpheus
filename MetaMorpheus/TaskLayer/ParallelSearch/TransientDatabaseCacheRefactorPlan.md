# TransientDatabaseCache Refactor Plan

## Goal

Make `EngineLayer/ParallelSearch/TransientDatabaseLoadingEngine.cs` orchestration-only by moving cache-specific lookup, hydrate, and publish behavior behind a single cache facade plus internal support classes.

Locked design choices for this refactor:
- Keep `TransientDatabaseLoadingEngine` public.
- Add one cache-facing facade: `TransientDatabaseCache`.
- Keep `Status(...)` / `Warn(...)` emission in the engine.
- Engine owns the two-step hydrate flow: raw proteins are loaded by the engine first, then handed to the cache hydrator.
- Most `PersistentCache` implementation types should become `internal` after the extraction.
- Tests should remain mixed: keep low-level cache tests, but add facade-level/orchestration-focused coverage where useful.

## Target Shape

### Public surface
- `EngineLayer/ParallelSearch/TransientDatabaseLoadingEngine.cs`
- `EngineLayer/ParallelSearch/PersistentCache/TransientCacheTelemetry.cs`
- `EngineLayer/ParallelSearch/PersistentCache/Manifest/TransientCacheGrowthSummary` (currently in `TransientCacheManifestModels.cs`)

### Internal cache surface
- `EngineLayer/ParallelSearch/PersistentCache/TransientDatabaseCache.cs`
- `EngineLayer/ParallelSearch/PersistentCache/TransientCacheContext.cs`
- `EngineLayer/ParallelSearch/PersistentCache/TransientCacheLookupResult.cs`
- `EngineLayer/ParallelSearch/PersistentCache/TransientCacheHydrationResult.cs`
- `EngineLayer/ParallelSearch/PersistentCache/TransientCachePublishResult.cs`
- `EngineLayer/ParallelSearch/PersistentCache/TransientCacheHydrator.cs`
- `EngineLayer/ParallelSearch/PersistentCache/TransientCachePublisher.cs`

### Engine flow after refactor
1. `TransientDatabaseLoadingEngine.RunSpecific()` checks whether cache use is enabled.
2. Engine asks `TransientDatabaseCache.TryLookup(...)` for a cache probe result.
3. If lookup is reusable:
   - engine loads raw proteins once via `base.RunSpecific()`
   - engine asks cache to hydrate those raw proteins
   - on hydrate success, returns hydrated results
   - on hydrate failure, reuses the raw load as fallback
4. If lookup misses or is rejected, engine runs `base.RunSpecific()` once.
5. Engine asks `TransientDatabaseCache.TryPublish(...)` to publish raw proteins into the cache.
6. Engine emits all status/warn messages based on cache result objects.

## Commit 1

### Title
`ParallelSearch: extract transient cache lookup facade`

### Files to add
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientDatabaseCache.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientCacheContext.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientCacheLookupResult.cs`

### Files to modify
- `MetaMorpheus/EngineLayer/ParallelSearch/TransientDatabaseLoadingEngine.cs`
- `MetaMorpheus/Test/ParallelSearchTask/PersistentCache/TransientDatabaseLoadingEngineTests.cs`

### Exact moves
- Move the cache probe/setup block out of `TransientDatabaseLoadingEngine.RunSpecific()` into `TransientDatabaseCache.TryLookup(...)`:
  - cache usability gate based on `UseCache`, DB count/path validity, and file existence
  - `TransientCacheSettingsDescriptor.Create(...)`
  - `TransientCacheHashing.ComputeDatabaseContentHash(...)`
  - `TransientCacheKey` construction
  - `TransientCacheManifestStore` creation
  - manifest entry lookup / shard lookup / resolved sequence lookup needed to describe the probe result
- Keep these methods in the engine for now:
  - `TryHydrateFromCache(...)`
  - `PublishCacheEntry(...)`

### Engine end state after commit 1
- `RunSpecific()` should call `TransientDatabaseCache.TryLookup(...)` instead of manually building cache key/settings/manifest context inline.
- Engine still performs hydrate and publish itself.

### Notes
- `TransientCacheContext` should hold the immutable data that is currently recreated/passed around repeatedly:
  - DB path
  - `TransientCacheKey`
  - canonical settings payload
  - `TransientCacheManifestStore`
  - `TransientCacheStorageLayout`
- `TransientCacheLookupResult` should distinguish at least:
  - disabled/unavailable
  - miss
  - reusable hit candidate
  - corrupt/rejected probe

## Commit 2

### Title
`ParallelSearch: extract transient cache hydrator`

### Files to add
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientCacheHydrator.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientCacheHydrationResult.cs`

### Files to modify
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientDatabaseCache.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/TransientDatabaseLoadingEngine.cs`
- `MetaMorpheus/Test/ParallelSearchTask/PersistentCache/TransientDatabaseLoadingEngineTests.cs`

### Exact moves
- Move `TryHydrateFromCache(...)` from `TransientDatabaseLoadingEngine` to `TransientCacheHydrator.TryHydrate(...)`.
- Move these helper methods from `TransientDatabaseLoadingEngine` to `TransientCacheHydrator`:
  - `CreateLegacyFragmentResolver(...)`
  - `CreateSharedFragmentResolver(...)`
  - `LoadSharedFragmentPayload(...)`
- Move any hydrate-only private record/result types out of the engine and into `TransientCacheHydrationResult.cs` if needed.

### Engine end state after commit 2
- Engine owns the two-step hit path explicitly:
  1. get lookup result from cache facade
  2. load raw proteins via `base.RunSpecific()`
  3. pass raw proteins into `TransientDatabaseCache.TryHydrate(...)`
- Engine continues to emit warnings/statuses based on `TransientCacheHydrationResult`.

### Notes
- Do not let `TransientCacheHydrator` call `base.RunSpecific()` or emit engine messages.
- Hydrator should accept raw proteins and return hydrated proteins/results only.
- Preserve the current lazy fragment loading and quarantine behavior exactly as-is during extraction.

## Commit 3

### Title
`ParallelSearch: extract transient cache publisher`

### Files to add
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientCachePublisher.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientCachePublishResult.cs`

### Files to modify
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientDatabaseCache.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/TransientDatabaseLoadingEngine.cs`
- `MetaMorpheus/Test/ParallelSearchTask/PersistentCache/TransientDatabaseLoadingEngineTests.cs`

### Exact moves
- Move `PublishCacheEntry(...)` from `TransientDatabaseLoadingEngine` to `TransientCachePublisher.TryPublish(...)`.
- Move these helper methods from `TransientDatabaseLoadingEngine` to `TransientCachePublisher`:
  - `BuildDbLocalOccurrencePayload(...)`
  - `PublishOccurrenceShard(...)`
  - `PublishSharedFragmentShards(...)`
  - `ResolveOrPublishFragmentShard(...)`
  - `ResolveSelectedMods(...)`
  - `GetLocalSequenceKey(...)`
  - `ComputeSharedSequenceHash(...)`
- Move any publish-only private record structs out of the engine and into `TransientCachePublishResult.cs` or `TransientCachePublisher.cs` as internal record types:
  - `DbLocalOccurrencePayload`
  - `OccurrenceShardPublishResult`
  - `SharedFragmentPublishResult`
  - `FragmentShardPublishResult`

### Engine end state after commit 3
- Engine calls `TransientDatabaseCache.TryPublish(...)` after raw fallback load succeeds.
- Engine no longer owns cache payload construction, shard publication, or manifest mutation details.

### Notes
- Preserve current telemetry behavior exactly:
  - published shared sequence count
  - occurrence payload bytes written
  - fragment payload bytes written
  - reused fragment shard count
  - quarantine counters
- Keep publish synchronous, matching the current cache contract.

## Commit 4

### Title
`ParallelSearch: tighten transient cache visibility and docs`

### Files to modify
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/Manifest/TransientCacheManifestStore.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/Manifest/TransientCacheManifestModels.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/Payloads/TransientCachePayloadModels.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/Payloads/TransientCacheSegmentManager.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientCacheHashing.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientCacheKey.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientCacheLookupOutcome.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientCacheMessages.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientCachePublishState.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientCacheSettingsDescriptor.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientCacheStorageLayout.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/PersistentCache/TransientCachePayloadSerializer.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/TransientDatabaseLoadingEngine.cs`
- `MetaMorpheus/TaskLayer/ParallelSearch/TransientPersistentCachePlan.md`
- `MetaMorpheus/Test/ParallelSearchTask/PersistentCache/*.cs` as needed for namespace/visibility fallout

### Exact cleanup steps
- Make these implementation types `internal` unless a real production consumer outside the cache facade still needs them:
  - `TransientCacheManifestStore`
  - `TransientCachePayloadSerializer`
  - `TransientCacheSegmentManager`
  - `TransientCacheStorageLayout`
  - `TransientCacheSettingsDescriptor`
  - `TransientCacheMessages`
  - `TransientCacheHashing`
  - `TransientCacheKey`
  - `TransientCacheLookupOutcome`
  - `TransientCachePublishState`
  - manifest/payload DTOs currently public only for implementation reasons
- Keep these public:
  - `TransientDatabaseLoadingEngine`
  - `TransientCacheTelemetry`
  - `TransientCacheGrowthSummary`
- Remove leftover cache-specific private record structs from `TransientDatabaseLoadingEngine.cs` after the extraction.
- Update `TransientPersistentCachePlan.md` with the final readability architecture so the plan matches the new class layout.

### Engine end state after commit 4
- `TransientDatabaseLoadingEngine.cs` should mainly contain:
  - constructor overloads
  - `Telemetry` exposure
  - `RunSpecific()` orchestration
  - tiny helpers only if they are engine-specific rather than cache-specific

## File-By-File Method Map

### Stay in `TransientDatabaseLoadingEngine.cs`
- constructors
- `RunSpecific()`
- any engine-only status/warn composition helpers introduced during refactor

### Move to `TransientDatabaseCache.cs`
- no heavy logic; this should compose collaborators
- facade methods:
  - `TryLookup(...)`
  - `TryHydrate(...)`
  - `TryPublish(...)`
  - optional `GetGrowthSummary()`

### Move to `TransientCacheHydrator.cs`
- `TryHydrateFromCache(...)`
- `CreateLegacyFragmentResolver(...)`
- `CreateSharedFragmentResolver(...)`
- `LoadSharedFragmentPayload(...)`

### Move to `TransientCachePublisher.cs`
- `PublishCacheEntry(...)`
- `BuildDbLocalOccurrencePayload(...)`
- `PublishOccurrenceShard(...)`
- `PublishSharedFragmentShards(...)`
- `ResolveOrPublishFragmentShard(...)`
- `ResolveSelectedMods(...)`
- `GetLocalSequenceKey(...)`
- `ComputeSharedSequenceHash(...)`

## Acceptance Criteria

- `TransientDatabaseLoadingEngine.cs` is substantially smaller and reads as orchestration, not storage implementation.
- Cache lookup, hydrate, and publish concerns each live in their own internal class.
- Engine is still the only source of `Status(...)` / `Warn(...)` calls.
- Existing transient-cache tests remain green.
- Public surface of `PersistentCache` is reduced to the intentional API.

## Status

- [x] Commit 1: lookup facade extracted
- [x] Commit 2: hydrator extracted
- [x] Commit 3: publisher extracted
- [x] Commit 4: visibility tightening and docs update

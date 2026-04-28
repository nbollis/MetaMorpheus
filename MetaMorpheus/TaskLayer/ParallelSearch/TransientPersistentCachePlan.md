# Transient Persistent Cache Plan

## Goal

Speed up `MetaMorpheus/TaskLayer/ParallelSearch/ParallelSearchTask.cs` by reducing repeated digestion and fragmentation work performed during transient search in `MetaMorpheus/EngineLayer/ParallelSearch/TransientClassicSearchEngine.cs`.

The persistent cache will hydrate runtime wrappers into:

- `MetaMorpheus/EngineLayer/ParallelSearch/TransientBioPolymer.cs`
- `MetaMorpheus/EngineLayer/ParallelSearch/TransientBioPolymerWithSetMods.cs`

The loader entry point will be:

- `MetaMorpheus/EngineLayer/ParallelSearch/TransientDatabaseLoadingEngine.cs`

## Problem Shape

- `ParallelSearchTask` can process very many small transient databases.
- Repeated runs currently pay the cost of loading, digesting, and fragmenting those databases again.
- We need a design that remains fast even when a directory contains very large numbers of transient FASTAs.
- We must preserve transient parsimony and transient protein scoring correctness, including parent-reference identity.

## Locked Design Decisions

### Scope and Rollout

- Cache location: one global cache store, not per-database sidecars.
- Phase 1 persistence scope: digests plus theoretical fragments.
- Rollout: feature flag first.
- Retention: no eviction in v1.

### Matching and Fallback

- Cache reuse is strict.
- Transient cache lookup key is `(DatabaseContentHash, CacheSettingsId)`.
- `DatabaseContentHash` is based on full file content hash only.
- `CacheSettingsId` identifies the cache-generation settings universe.
- If there is no exact published match, we fall back to normal `DatabaseLoadingEngine` behavior.
- Any hydrate mismatch, corruption, or parent-identity failure also falls back to base behavior.

### Runtime Identity and Semantics

- Hydrated peptides must point back to the current transient wrapper protein instances used for that DB load.
- Parent-reference identity is enforced, not best-effort.
- Existing output semantics, transient filtering behavior, and protein/parsimony logic must remain unchanged.

### Storage Model

- Manifest/index store: SQLite.
- Heavy immutable payloads: custom append-only binary payload files.
- Format split: hybrid manifest plus binary payloads.
- Peptide identity: `FullSequence` only.
- DB-local payload shape: protein -> occurrence ranges -> DB-local full-sequence ordinals.
- Shared sequence catalog: scoped by `CacheSettingsId`, keyed by sequence hash, verified by stored `FullSequence`.
- Fragment payload identity: `FullSequence` within the outer `CacheSettingsId` namespace.
- Fragment payload optimization goal: fast hydrate layout.
- Fragment payload storage: compact columnar arrays.
- Payload write/update strategy: append-only segments.
- Payload integrity: checksummed payload plus atomic manifest publish.
- Refcounts: tracked in v1.
- Keep `PeptideDescription` in the DB-local occurrence payload.
- Assume cached cleavage specificity is `Full`.
- Shared payload segments are separated by payload kind.
- Default rollover caps are `128MB` for occurrence/digest segments and `512MB` for fragment segments.
- Corrupt shared fragment mappings are quarantined and rebuilt.

### Fragment Hydration

- Digest/protein metadata is hydrated up front on cache hit.
- Fragment bytes are hydrated lazily.
- On first fragment access, load the entire referenced logical fragment shard into memory.
- Physical layout is large segment files plus shard offsets.
- Logical fragment payloads are stored as bounded shards inside those large segment files.

### Concurrency

- Miss processing may build data in memory in parallel.
- Final cache publish path is single-writer.

## Naming

- Use `DatabaseContentHash` for the source DB identity.
- Use `CacheSettingsId` for the settings fingerprint.
- Do not use `NamespaceId`; that name is too vague.

`CacheSettingsId` includes everything besides DB contents that changes emitted digest or fragment payloads, such as:

- digestion settings
- protease and cleavage behavior
- missed cleavages
- peptide length rules
- fragmentation-affecting settings
- dissociation type
- fragmentation terminus
- cache schema version

## High-Level Lookup Rule

For each transient DB load:

1. Compute `DatabaseContentHash` from the full DB file contents.
2. Compute `CacheSettingsId` from digestion, fragmentation-affecting settings, and cache schema version.
3. Query the global SQLite manifest for an exact published entry matching both values.
4. If the exact entry exists, hydrate transient wrappers from cached payloads.
5. If no exact entry exists, use normal `DatabaseLoadingEngine` behavior.
6. If base behavior succeeds and the feature flag is enabled, build and publish the cache synchronously.
7. If hydrate validation fails at any point, discard cached state and fall back to base behavior.

This rule is intentionally strict:

- exact match -> read cache
- any mismatch -> base behavior

## Why a Global Cache

The cache must scale to workloads where a directory may contain very large numbers of small transient databases.

We explicitly do not want:

- sidecar cache files next to each transient DB
- directory scanning to find matching cache artifacts
- per-directory cache explosion

We do want:

- one global manifest
- indexed point lookup by `(DatabaseContentHash, CacheSettingsId)`
- a small number of large append-only payload segment files

## High-Level Architecture

### Manifest Layer (SQLite)

The SQLite manifest is the source of truth for:

- which DB contents have been seen
- which settings fingerprints exist
- which cache entries are published and valid
- where payload shards live inside segment files
- refcounts and checksums
- diagnostics and telemetry

### Payload Layer (Binary Segments)

The binary payload store contains immutable heavy data that must hydrate quickly:

- DB-local occurrence layout
- shared fragment shards keyed by `FullSequence`

Payload data is append-only. Manifest rows become visible only after successful payload write and validation.

## Conceptual Data Model

### 1. Source Database Identity

Represents the exact transient DB contents.

Suggested conceptual fields:

- `DatabaseContentHash`
- optional diagnostic metadata such as original path, file size, or timestamps

Important:

- diagnostic metadata is not part of DB identity
- full file content hash is the identity

### 2. Cache Settings Identity

Represents the exact cache-generation settings universe.

Suggested conceptual fields:

- `CacheSettingsId`
- cache schema version
- digestion settings hash
- fragmentation settings hash
- any additional digest/fragment affecting settings fingerprint

### 3. Database Cache Entry

Represents a published mapping from one source DB under one settings universe to payload references.

Suggested conceptual fields:

- `DatabaseContentHash`
- `CacheSettingsId`
- publish state
- occurrence shard refs
- DB-local local-ordinal -> shared-sequence mappings
- counts
- checksums
- created/published timestamps

### 4. Shared Sequence Catalog

Represents the reusable peptide identity layer for a single `CacheSettingsId` universe.

Each record stores:

- a sequence hash used for lookup
- the `FullSequence` used for collision verification
- the shared fragment payload reference for that sequence

This catalog exists so many databases can reuse the same fragment mapping without forcing occurrence payloads into a giant global ID space.

### 5. Occurrence Records

Represents parent-specific peptide occurrences.

Each protein maps to a range of occurrence records. Each occurrence record points to a DB-local full-sequence ordinal.

This layout is chosen so `TransientBioPolymer.Digest()` can cheaply materialize per-protein peptide lists without regrouping a flat peptide table.

Each database-local payload also keeps:

- a compact full-sequence table
- `PeptideDescription`
- start/end residue offsets and missed cleavages

### 6. DB-Local Sequence Table

Represents the unique `FullSequence` values present in one database-local occurrence payload.

Occurrences point to these local ordinals rather than directly to global shared IDs so cache-hit deserialization stays compact and fast.

### 7. Shared Fragment Payloads

Represents reusable fragment mappings keyed by `FullSequence` inside a given `CacheSettingsId` universe.

This owns data such as:

- the fragment payload reference for each reusable sequence
- any compactly serialized fragment fields needed to reconstruct runtime `Product` objects

That means identical `FullSequence` values may reuse fragment payloads, but only within the outer settings fingerprint that fixes fragmentation behavior.

## Physical Layout

### Manifest

- single global SQLite database
- indexed lookup on `DatabaseContentHash` and `CacheSettingsId`
- manifest rows for payload refs, shard offsets, checksums, publish state, and refcounts

### Payload Segments

- a manageable number of large append-only binary files
- many logical shards per physical file
- SQLite maps shard IDs to file, offset, length, checksum, and payload kind
- occurrence/digest payloads and fragment payloads live in separate segment families
- append targets are selected from manifest state, not directory scans

This avoids both extremes:

- one monolithic fragment file with poor locality
- millions of tiny files with filesystem overhead

## Fragment Payload Serialization

Fragment payloads should be stored in compact columnar arrays rather than row-by-row object serialization.

Design goal:

- cheap decode
- contiguous reads
- low allocation on hydrate
- no dependence on slow random per-peptide reads

The exact persisted field set is still open, but the intent is to persist only what is necessary to reconstruct the `Product` objects used at runtime.

## Runtime Hydration Model

### Cache Hit

1. `TransientDatabaseLoadingEngine` resolves the exact `(DatabaseContentHash, CacheSettingsId)` entry.
2. It hydrates transient protein wrappers and DB-local occurrence mappings up front.
3. It creates `TransientBioPolymer` instances backed by cache payload references.
4. It creates `TransientBioPolymerWithSetMods` instances whose `Parent` references point to the current transient wrapper proteins for this load.
5. Shared fragment bytes remain unloaded until first `Fragment(...)` access.
6. On first fragment touch for a sequence, the referenced shared shard bytes are loaded into process memory.

### Cache Miss

1. Use normal `DatabaseLoadingEngine` behavior.
2. Build transient wrappers from the loaded proteins.
3. Generate DB-local occurrence payloads plus per-sequence shared fragment payloads.
4. Publish the occurrence shard, publish or reuse shared fragment shards, then commit the manifest entry and local-ordinal mappings synchronously if the feature flag is enabled.

### Cache Failure Path

Any of the following causes immediate fallback to base behavior:

- manifest miss
- schema/version mismatch
- checksum failure
- decode failure
- parent identity validation failure
- wrapper reconstruction inconsistency
- quarantined shared fragment mapping

## Wrapper Invariants

### `TransientBioPolymer`

- Must expose the same runtime digest semantics expected by transient search.
- Must be able to materialize per-protein peptide occurrence lists from cached `protein -> occurrence ranges`.
- Must preserve DB-local metadata while reusing shared payloads.

### `TransientBioPolymerWithSetMods`

- Must preserve equality/hash behavior required by transient parsimony and downstream grouping.
- Must preserve parent reference identity against current transient wrapper proteins.
- Must lazily hydrate fragment payloads on first use.

### General

- Any `CachedBioPolymer*` remnants should be removed.
- The transient path should have one wrapper model, not mixed cached/transient types.

## Publish Model

### Miss Build

- Digest and fragment generation can happen in memory during miss processing.
- Build work may be parallelized.

### Publish

- Final publish path is single-writer.
- Payloads are appended to segment files chosen from manifest state.
- Checksums are recorded.
- Manifest rows become visible only after successful payload append and manifest commit.

### Mutability

- Payload segments are append-only.
- Refcounts are tracked now for future compaction/analytics.
- No eviction or compaction is required for v1.

## Integration Points

### `TransientDatabaseLoadingEngine`

Responsibilities:

- compute `DatabaseContentHash`
- compute `CacheSettingsId`
- probe manifest
- hydrate transient wrappers on hit
- fall back to base DB loading on miss or failure
- synchronously publish new cache entries after fallback success

### `ParallelSearchTask`

Responsibilities:

- select transient loader based on feature flag
- preserve current task behavior when cache is disabled or unavailable
- keep downstream transient search/parsimony/protein scoring flows unchanged
- avoid concrete `Protein` assumptions in transient peptide counting logic

### `TransientClassicSearchEngine`

Responsibilities remain unchanged except that it should consume already-hydrated transient wrappers instead of paying repeated digest/fragment costs.

## Implementation Phases

### Phase 0 - Contracts and Schema

- [x] Define cache schema/version constants.
- [x] Define the exact `CacheSettingsId` input set.
- [x] Define manifest publish states.
- [x] Define checksum policy.
- [x] Define logging/telemetry conventions for hit/miss/mismatch/corrupt paths.

Completed in this phase:

- Added `EngineLayer/ParallelSearch/PersistentCache/TransientCacheSchema.cs` for schema/version, hash algorithm, and message prefix constants.
- Added `EngineLayer/ParallelSearch/PersistentCache/TransientCacheKey.cs` for the exact `(DatabaseContentHash, CacheSettingsId)` lookup key contract.
- Added `EngineLayer/ParallelSearch/PersistentCache/TransientCacheLookupOutcome.cs` and `TransientCachePublishState.cs` for manifest/cache state contracts.
- Added `EngineLayer/ParallelSearch/PersistentCache/TransientCacheHashing.cs` for SHA-256 database content hashing and settings payload hashing.
- Added `EngineLayer/ParallelSearch/PersistentCache/TransientCacheMessages.cs` for standardized hit/miss/mismatch/corrupt/fallback message formatting.
- Added `EngineLayer/ParallelSearch/PersistentCache/TransientCacheSettingsDescriptor.cs` to build canonical settings payloads and compute `CacheSettingsId` from the real transient loader/search inputs.
- Added `Test/ParallelSearchTask/PersistentCache/TransientCacheContractsTests.cs` covering content-hash identity, order-independent settings hashing, relevant-settings drift detection, and cache message conventions.
- Validated phase 0 with targeted build/test runs before moving on to runtime cache integration.

### Phase 1 - Wrapper Finalization

- [x] Remove any remaining `CachedBioPolymer*` references.
- [x] Finalize `TransientBioPolymer` cache-backed digest materialization model.
- [x] Finalize `TransientBioPolymerWithSetMods` cache-backed lazy fragment hydrate model.
- [x] Validate wrapper `Clone`, `Equals`, and hash semantics.
- [x] Validate parent reference identity invariants.

Completed in this phase:

- Finalized `EngineLayer/ParallelSearch/TransientBioPolymer.cs` so transient proteins can expose precomputed peptide counts, materialize digest products from a factory or preloaded payload, and normalize emitted peptides back onto the current transient parent wrapper.
- Finalized `EngineLayer/ParallelSearch/TransientBioPolymerWithSetMods.cs` so transient peptides can preserve explicit parent identity, lazily hydrate fragment payloads from a factory or preloaded payload, and still expose the underlying raw `Protein` where downstream code expects it.
- Added wrapper guardrails so any hydrated digestion product already wrapped against the wrong transient parent fails fast instead of leaking invalid parent identity into transient parsimony or protein grouping.
- Kept peptidoform equality and hashing aligned with downstream grouping semantics by comparing `FullSequence`, digestion agent, residue offset, and parent accession across both raw and wrapped peptide instances.
- Added `Test/ParallelSearchTask/Engine/TransientBioPolymerWrapperTests.cs` covering digest factory caching, fragment factory caching, clone behavior, equality/hash behavior, and parent-identity invariants.
- Validated Phase 1 with targeted build/test runs, including the new wrapper suite plus nearby transient parsimony, transient protein scoring, and transient cache contract tests.

### Phase 2 - Manifest and Payload Infrastructure

- [x] Implement SQLite manifest schema.
- [x] Implement payload segment writer/reader.
- [x] Implement shard table and offset mapping.
- [x] Implement refcount tracking.
- [x] Implement checksummed payload headers or equivalent validation blocks.

### Phase 3 - Transient Loader Implementation

- [x] Implement `TransientDatabaseLoadingEngine.RunSpecific()` cache-first flow.
- [x] Implement exact-match manifest lookup using `(DatabaseContentHash, CacheSettingsId)`.
- [x] Implement hydrate path for proteins, occurrences, peptidoforms, and fragment shard refs.
- [x] Implement lazy fragment shard loading on first use.
- [x] Implement fallback-to-base behavior.
- [x] Implement synchronous publish on miss.

Completed in this phase:
- Added `EngineLayer/ParallelSearch/PersistentCache/TransientCachePayloadSerializer.cs` for compact binary digest/fragment payload serialization and deserialization.
- Implemented `TransientDatabaseLoadingEngine.RunSpecific()` to compute the cache key, query the manifest, hydrate `TransientBioPolymer` wrappers with lazy fragment factories on hit, and fall back to `DatabaseLoadingEngine` with synchronous publish on miss.
- Updated `ParallelSearchTask.LoadTransientDatabase` to instantiate and use `TransientDatabaseLoadingEngine` behind `ParallelSearchParameters.UseTransientCache` (default false).
- Added `TransientDatabaseLoadingEngineTests` with cache miss, cache hit, and cache disabled scenarios, all passing.

### Phase 4 - Task Wiring and Compatibility

- [x] Wire `ParallelSearchTask` to use the transient loader behind a feature flag.
- [x] Keep uncached/base behavior unchanged.
- [x] Update `CalculateTransientPeptideCount(...)` to avoid concrete `Protein` assumptions.
- [x] Validate compatibility with transient parsimony reference-equality behavior.

Completed in this phase:
- Verified `CalculateTransientPeptideCount` already uses `IBioPolymer.Digest()` and handles `TransientBioPolymer.PeptideCount` without concrete `Protein` casts.
- Added `RunSpecific_WorksWithTransientBioPolymerWrappers` test to `TransientProteinParsimonyEngineTests` proving that `TransientBioPolymer` wrapper instances flow correctly through parsimony, and reference-equality filtering retains only wrapper-backed groups.

### Phase 5 - Validation and Profiling

- [x] Add cache hit/miss/mismatch/corrupt tests.
- [x] Add restart persistence tests.
- [x] Add parent-identity validation tests.
- [x] Add cached vs uncached parity tests.
- [x] Add performance tests focused on digestion/fragmentation reduction.
- [x] Add telemetry for cache growth and publish volume.

Completed in this phase:
- Added `EngineLayer/ParallelSearch/PersistentCache/TransientCacheTelemetry.cs` to collect per-run cache metrics (hits, misses, fallbacks, corrupt entries, timing, payload bytes written).
- Wired telemetry collection into `TransientDatabaseLoadingEngine.RunSpecific()` for hydrate, fallback, and publish paths.
- Added `TransientCacheManifestStore.GetCacheGrowthSummary()` for aggregate cache size queries (entry counts, shard counts, total payload bytes).
- Expanded `Test/ParallelSearchTask/PersistentCache/TransientDatabaseLoadingEngineTests.cs` with:
  - `Load_CacheCorrupt_FallsBackToBaseAndRepairs` — corrupts payload segment file and verifies fallback + telemetry recording.
  - `Load_CacheSettingsMismatch_FallsBackToBase` — changes dissociation type and verifies independent cache entries.
  - `Load_CacheSurvivesProcessRestart` — verifies cache persistence across new engine instances.
  - `Load_HydratedParentIdentity_MatchesOriginalProteins` — verifies every cached peptide's `Parent` reference points back to the correct `TransientBioPolymer` wrapper.
  - `Load_Performance_CachedLoadIsRepeatableAndFast` — validates repeated cache hits complete quickly and consistently.
  - `Load_Telemetry_RecordsMetrics` — verifies telemetry exposes all expected metric keys on miss.
  - `Load_Telemetry_HitRecordsNoFallback` — verifies hit telemetry shows zero misses/fallbacks/bytes-written.
  - `ManifestStore_GrowthSummary_AfterPublish` — validates aggregate growth summary after cache publish.
- All 14 `TransientDatabaseLoadingEngineTests` pass, plus broader transient cache, wrapper, parsimony, and scoring suites remain green.

## Compatibility and Safety Requirements

- Cache mismatch must never silently reuse stale data.
- Any hydrate failure must safely degrade to base behavior.
- Parent reference identity must remain correct for transient proteins and peptides.
- Transient parsimony and transient protein scoring inputs must remain semantically equivalent.
- Existing output behavior must remain unchanged.

## Observability

Track per run:

- transient DB cache hits
- transient DB cache misses
- transient DB cache mismatches
- transient DB cache corrupt/decode failures
- published shared sequence count
- reused fragment shard count
- quarantined shared sequence count
- time spent in hydrate path
- time spent in fallback path
- occurrence payload bytes written
- fragment payload bytes written
- global cache size growth, including shared-sequence counts and segment/shard counts by kind

## Risks and Mitigations

- **Risk:** stale or unsafe cache reuse.
  - **Mitigation:** strict exact-match lookup using `DatabaseContentHash` and `CacheSettingsId` plus schema version and checksum validation.

- **Risk:** broken parent identity causes transient parsimony or protein grouping errors.
  - **Mitigation:** explicit parent identity validation during hydrate; fallback on failure.

- **Risk:** too many files or poor filesystem performance.
  - **Mitigation:** single global manifest, manifest-driven segment selection, and large append-only segment files with shard offsets.

- **Risk:** binary payload complexity causes corruption or hard-to-debug failures.
  - **Mitigation:** checksummed payloads, atomic manifest publish, and safe fallback to base behavior.

- **Risk:** global cache grows indefinitely.
  - **Mitigation:** no eviction in v1, but track refcounts and size telemetry now to enable later compaction/cleanup.

## Final V2 Layout

- DB-local cache entries own exactly one occurrence shard plus local-ordinal mappings into the settings-scoped shared sequence catalog.
- Shared sequence records are keyed by `CacheSettingsId + SequenceHash + FullSequence` verification and own the reusable shared fragment shard reference.
- Physical payload storage uses shared append-only segment families with manifest-managed rollover, not one payload file per database.
- Cache hits read only the occurrence payload eagerly and resolve shared fragment payloads lazily on first `Fragment(...)` access.
- Corrupt shared fragment shards are quarantined at shard scope, rejected on later hits, and replaced on rebuild.
- The active schema/runtime contract is the V2 layout described above; later changes should be treated as a new schema migration rather than an in-place tweak.

## Exit Criteria for Initial Rollout

- Feature-flagged cached path is stable on representative transient DB sets.
- Cached and uncached outputs are equivalent within expected tolerance.
- Fallback behavior is robust for misses, mismatches, and corrupt cache state.
- Parent identity invariants hold under transient parsimony and protein scoring flows.
- Measurable reduction in transient digestion and fragmentation time is demonstrated.

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
- Dedup granularity: hybrid global payload dedup.
- Shared peptide normalization: occurrence -> shared peptidoform.
- DB-local digest mapping layout: protein -> occurrence ranges.
- Exact protein-digest dedup identity: sequence plus digest-affecting state, not accession.
- Fragment payload identity: `FullSequence` within the outer `CacheSettingsId` namespace.
- Fragment payload optimization goal: fast hydrate layout.
- Fragment payload storage: compact columnar arrays.
- Payload write/update strategy: append-only segments.
- Payload integrity: checksummed payload plus atomic manifest publish.
- Refcounts: tracked in v1.

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

- shared protein-digest payloads
- DB-local occurrence layout
- shared peptidoform payloads
- fragment shards

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
- protein payload refs
- occurrence payload refs
- fragment shard refs
- counts
- checksums
- created/published timestamps

### 4. Shared Protein-Digest Payload

Represents deduplicated digest output for proteins that are effectively identical from a digest perspective.

Canonical identity is based on:

- protein primary sequence
- digest-affecting localized state

Not based on:

- accession
- organism metadata
- protein display names

Metadata like accession remains DB-local and must be preserved in runtime wrappers.

### 5. Occurrence Records

Represents parent-specific peptide occurrences.

Each protein maps to a range of occurrence records. Each occurrence record points to a shared peptidoform payload.

This layout is chosen so `TransientBioPolymer.Digest()` can cheaply materialize per-protein peptide lists without regrouping a flat peptide table.

### 6. Shared Peptidoform Payload

Represents sequence/modification payload shared across occurrences.

This owns data such as:

- full sequence
- base sequence if needed
- monoisotopic mass
- digestion agent / termini / residue offsets needed for reconstruction
- fragment payload reference

### 7. Fragment Payloads

Fragment payload identity is keyed by `FullSequence` inside a given `CacheSettingsId` universe.

That means:

- identical `FullSequence` values may reuse fragment payloads
- only within the outer settings fingerprint that fixes fragmentation behavior

## Physical Layout

### Manifest

- single global SQLite database
- indexed lookup on `DatabaseContentHash` and `CacheSettingsId`
- manifest rows for payload refs, shard offsets, checksums, publish state, and refcounts

### Payload Segments

- a manageable number of large append-only binary files
- many logical shards per physical file
- SQLite maps shard IDs to file, offset, length, checksum, and payload kind

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
2. It hydrates transient protein wrappers and DB-local digest occurrence mappings up front.
3. It creates `TransientBioPolymer` instances backed by cache payload references.
4. It creates `TransientBioPolymerWithSetMods` instances whose `Parent` references point to the current transient wrapper proteins for this load.
5. Fragment bytes remain unloaded until first `Fragment(...)` access.
6. On first fragment touch for a shard, the full shard bytes are loaded into process memory.

### Cache Miss

1. Use normal `DatabaseLoadingEngine` behavior.
2. Build transient wrappers from the loaded proteins.
3. Generate digest and fragment payloads.
4. Publish payloads and manifest entry synchronously if the feature flag is enabled.

### Cache Failure Path

Any of the following causes immediate fallback to base behavior:

- manifest miss
- schema/version mismatch
- checksum failure
- decode failure
- parent identity validation failure
- wrapper reconstruction inconsistency

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
- Payloads are appended to segment files.
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

- [ ] Define cache schema/version constants.
- [ ] Define the exact `CacheSettingsId` input set.
- [ ] Define manifest publish states.
- [ ] Define checksum policy.
- [ ] Define logging/telemetry conventions for hit/miss/mismatch/corrupt paths.

### Phase 1 - Wrapper Finalization

- [ ] Remove any remaining `CachedBioPolymer*` references.
- [ ] Finalize `TransientBioPolymer` cache-backed digest materialization model.
- [ ] Finalize `TransientBioPolymerWithSetMods` cache-backed lazy fragment hydrate model.
- [ ] Validate wrapper `Clone`, `Equals`, and hash semantics.
- [ ] Validate parent reference identity invariants.

### Phase 2 - Manifest and Payload Infrastructure

- [ ] Implement SQLite manifest schema.
- [ ] Implement payload segment writer/reader.
- [ ] Implement shard table and offset mapping.
- [ ] Implement refcount tracking.
- [ ] Implement checksummed payload headers or equivalent validation blocks.

### Phase 3 - Transient Loader Implementation

- [ ] Implement `TransientDatabaseLoadingEngine.RunSpecific()` cache-first flow.
- [ ] Implement exact-match manifest lookup using `(DatabaseContentHash, CacheSettingsId)`.
- [ ] Implement hydrate path for proteins, occurrences, peptidoforms, and fragment shard refs.
- [ ] Implement lazy fragment shard loading on first use.
- [ ] Implement fallback-to-base behavior.
- [ ] Implement synchronous publish on miss.

### Phase 4 - Task Wiring

- [ ] Wire `ParallelSearchTask` to use the transient loader behind a feature flag.
- [ ] Keep uncached/base behavior unchanged.
- [ ] Update `CalculateTransientPeptideCount(...)` to avoid concrete `Protein` assumptions.
- [ ] Validate compatibility with transient parsimony reference-equality behavior.

### Phase 5 - Validation and Profiling

- [ ] Add cache hit/miss/mismatch/corrupt tests.
- [ ] Add restart persistence tests.
- [ ] Add parent-identity validation tests.
- [ ] Add cached vs uncached parity tests.
- [ ] Add performance tests focused on digestion/fragmentation reduction.
- [ ] Add telemetry for cache growth and publish volume.

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
- time spent in hydrate path
- time spent in fallback path
- digestion and fragmentation time saved
- payload bytes written
- global cache size growth

## Risks and Mitigations

- **Risk:** stale or unsafe cache reuse.
  - **Mitigation:** strict exact-match lookup using `DatabaseContentHash` and `CacheSettingsId` plus schema version and checksum validation.

- **Risk:** broken parent identity causes transient parsimony or protein grouping errors.
  - **Mitigation:** explicit parent identity validation during hydrate; fallback on failure.

- **Risk:** too many files or poor filesystem performance.
  - **Mitigation:** single global manifest and large append-only segment files with shard offsets.

- **Risk:** binary payload complexity causes corruption or hard-to-debug failures.
  - **Mitigation:** checksummed payloads, atomic manifest publish, and safe fallback to base behavior.

- **Risk:** global cache grows indefinitely.
  - **Mitigation:** no eviction in v1, but track refcounts and size telemetry now to enable later compaction/cleanup.

## Open Questions

- [ ] Exact SQLite table layout and indexes.
- [ ] Exact binary schema for each payload kind.
- [ ] Exact fragment field set required to reconstruct runtime `Product` objects.
- [ ] Exact shard sizing heuristics within large segment files.
- [ ] Exact builder concurrency behavior before single-writer publish.
- [ ] Exact runtime APIs used by wrappers to bind hydrated payloads.

## Exit Criteria for Initial Rollout

- Feature-flagged cached path is stable on representative transient DB sets.
- Cached and uncached outputs are equivalent within expected tolerance.
- Fallback behavior is robust for misses, mismatches, and corrupt cache state.
- Parent identity invariants hold under transient parsimony and protein scoring flows.
- Measurable reduction in transient digestion and fragmentation time is demonstrated.

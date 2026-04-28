# Transient Persistent Cache Tracker

## Purpose

This file is the working execution tracker for the V2 transient persistent cache redesign.

Use it to:

- track implementation progress in execution order
- keep the build aligned with `TransientPersistentCachePlan.md`
- record deviations before they turn into drift

This tracker is intentionally separate from `todo.md` because that file mixes older ParallelSearch work with this newer cache redesign.

## Scope

This tracker covers the redesign that:

- reduces the number of payload files
- reuses peptide-fragment mappings across databases whenever possible
- keeps cache-hit deserialization fast

It does **not** replace the design doc. The design doc remains:

- `MetaMorpheus/TaskLayer/ParallelSearch/TransientPersistentCachePlan.md`

## Drift Guards

These decisions are currently locked for V2 and should not be changed casually during implementation:

- peptide identity is `FullSequence` only
- keep `PeptideDescription` in V2
- assume cleavage specificity is `Full` for cached values
- shared fragment reuse is scoped by `CacheSettingsId`
- database-local occurrence payloads use local ordinals
- shared payload segments are separated by payload kind
- default rollover caps are `128MB` for occurrence/digest segments and `512MB` for fragment segments
- shared fragment mappings are loaded lazily on demand
- corrupt shared fragment mappings are quarantined and rebuilt
- favor later reuse over the fastest first-time cache build

## Current Status

- Existing transient cache implementation is present and tested.
- This tracker is for the **next V2 storage/layout revision**, not the already-completed earlier cache phases.
- Main driver: reduce payload file count and improve cross-database fragment reuse.

## Execution Checklist

### 1. Lock the V2 contract

- [x] Update `TransientPersistentCachePlan.md` to reflect the final V2 storage/layout direction.
- [x] Bump `TransientCacheSchema.CurrentSchemaVersion` to `2`.
- [x] Add or update code comments where the V2 rules need to stay explicit.

### 2. Add manifest structures for the new shape

- [x] Add a shared sequence catalog keyed by `CacheSettingsId + SequenceHash` with `FullSequence` stored for verification.
- [x] Add mapping from cache entry + local ordinal to shared sequence record.
- [x] Add queries to find the latest appendable segment by payload kind.
- [x] Add quarantine state for bad shared fragment mappings.

### 3. Add a manifest-driven segment allocator

- [x] Create a segment manager that chooses append targets from manifest state.
- [x] Keep separate segment families for occurrence/digest and fragment payloads.
- [x] Implement rollover at `128MB / 512MB`.
- [x] Update true segment length after each append.

### 4. Replace the current per-DB digest blob with a DB-local occurrence payload

- [x] Refactor the payload serializer for the new DB-local occurrence payload.
- [x] Keep per-protein occurrence ranges.
- [x] Keep a DB-local full-sequence table.
- [x] Make occurrences point to local ordinals.
- [x] Keep `PeptideDescription`.
- [x] Remove cached fields we no longer want to persist.

### 5. Add the shared sequence catalog path

- [x] Use a hash key plus `FullSequence` verification.
- [x] Resolve or create shared sequence records during publish.
- [x] Keep occurrence references DB-local.

### 6. Move fragment reuse to per-sequence granularity

- [x] Stop writing one fragment blob per database.
- [x] Generate fragment payloads per unique `FullSequence`.
- [x] Reuse existing fragment shards when bytes match.
- [x] Append new fragment shards when reuse is not possible.
- [x] Bump refcounts on reuse.

### 7. Refactor the publish flow in `TransientDatabaseLoadingEngine`

- [x] Build DB-local sequence and occurrence payloads.
- [x] Publish the occurrence shard.
- [x] Resolve or publish shared fragment shards.
- [x] Publish cache entry metadata and local-ordinal mappings.
- [x] Record telemetry for bytes written and reuse.

### 8. Refactor the hydrate flow for fast cache hits

- [ ] Load proteins normally.
- [ ] Load only the DB-local occurrence payload up front.
- [ ] Rebuild `TransientBioPolymer` wrappers from that payload.
- [ ] Resolve fragment mappings lazily on first use.
- [ ] Preserve current parent-identity guarantees.

### 9. Add quarantine-and-rebuild behavior

- [ ] Mark corrupt shared fragment mappings as quarantined.
- [ ] Prevent later loads from reusing quarantined mappings.
- [ ] Fall back cleanly and rebuild when needed.
- [ ] Record corruption/quarantine telemetry.

### 10. Expand telemetry and growth reporting

- [ ] Track shared sequence count.
- [ ] Track reused fragment mapping count.
- [ ] Track quarantined mapping count.
- [ ] Track segment count by payload kind.
- [ ] Track total bytes split by occurrence payloads vs fragment payloads.

### 11. Replace and expand tests for the V2 layout

- [ ] Add schema-bump rebuild tests.
- [ ] Add settings-scoped shared sequence separation tests.
- [ ] Add cross-database fragment reuse tests for identical `FullSequence`.
- [ ] Add DB-local ordinal round-trip tests.
- [ ] Add manifest-driven segment reuse and rollover tests.
- [ ] Add lazy fragment loading tests.
- [ ] Add quarantine-and-rebuild tests.
- [ ] Add reduced-file-count tests.

### 12. Run validation in build order

- [ ] `dotnet build MetaMorpheus/EngineLayer/EngineLayer.csproj -c Release --no-restore`
- [ ] `dotnet test MetaMorpheus/Test/Test.csproj -c Release --filter "FullyQualifiedName~TransientCache"`
- [ ] `dotnet test MetaMorpheus/Test/Test.csproj -c Release --filter "FullyQualifiedName~TransientDatabaseLoadingEngineTests"`
- [ ] Run targeted reuse tests proving fragment reuse across different databases.

### 13. Clean up old assumptions after the new path is green

- [ ] Remove code that assumes one digest file and one fragment file per database.
- [ ] Remove no-longer-used payload kinds or helpers.
- [ ] Update the plan doc to describe the final V2 layout.

## Active Notes

- Keep this file current as implementation progresses.
- If a locked decision changes, update both this tracker and the plan doc in the same change.
- If implementation reveals a new ambiguity, record it here before changing the storage shape.

## Progress Log

- 2026-04-27: Created the V2 execution tracker to keep implementation aligned with the redesigned cache plan.
- 2026-04-27: Locked the V2 storage contract in the plan/tracker and bumped transient cache schema version to 2.
- 2026-04-27: Added V2 manifest structures for shared sequences, local-ordinal mappings, latest-segment lookup, and quarantine state.
- 2026-04-27: Added the manifest-driven segment allocator with separate occurrence/fragment families, rollover caps, and true segment-length updates.
- 2026-04-27: Replaced the per-database digest payload with a DB-local occurrence payload keyed by local full-sequence ordinals.
- 2026-04-27: Wired publish-time shared-sequence catalog registration so DB-local ordinals resolve to settings-scoped shared sequence records keyed by sequence hash plus `FullSequence` verification.
- 2026-04-27: Switched fragment publication to per-sequence shared shards, reused matching fragment bytes across DB entries, and moved cache hits onto shared fragment mappings while keeping legacy fragment-shard reads as a compatibility fallback.
- 2026-04-27: Refactored publish onto manifest-managed occurrence segments plus shared fragment publication helpers, and started tracking occurrence bytes, fragment bytes, and fragment-shard reuse in telemetry.

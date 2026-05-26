# Per-Database Batched Write Plan

This plan only changes per-database output writing during the parallel transient-database phase. Final aggregate outputs remain unchanged and continue to run sequentially at the end of the task.

## Goal

- Reduce HDD contention from too many databases writing at once.
- Preserve resumability by only marking a database complete in `ManySearchSummary.csv` after its per-database files are durable.
- Reuse the current `TransientDatabaseContext`, `ParallelSearchTask`, `TransientDatabaseResultsManager`, and `ParallelSearchResultCache` structure.
- Avoid introducing a separate write-manager subsystem or new request DTO files.

## Files To Modify

1. `TaskLayer/ParallelSearch/ParallelSearchTask.cs`
2. `TaskLayer/ParallelSearch/TransientDatabaseResultsManager.cs`
3. `TaskLayer/ParallelSearch/ParallelSearchResultCache.cs` only if later append-session optimization is needed

## Core Design

- Add a bounded background writer lane inside `ParallelSearchTask`.
- Keep using `TransientDatabaseContext` as the per-database payload.
- Pair it with the already-computed `TransientDatabaseMetrics` from `_resultsManager.ProcessDatabase(...)`.
- Move per-database file writing, optional compression, and checkpoint append into the background writer loop.
- Keep final aggregate outputs in `TransientDatabaseResultsManager.WriteAllResults(...)` and `ParallelSearchTask.WriteFinalOutputs(...)` exactly as they are.

## Implementation Checklist

### 1. Add the bounded background write lane in `ParallelSearchTask.cs`

Add private fields for:

- a bounded `Channel<(TransientDatabaseContext Context, TransientDatabaseMetrics Metrics)>`
- a `Task` for the background writer loop

Recommended channel configuration:

- `SingleReader = true`
- `SingleWriter = false`
- `FullMode = BoundedChannelFullMode.Wait`
- capacity `2`

This allows compute to get slightly ahead of disk, but applies backpressure before completed databases pile up in memory.

### 2. Add a private writer-loop method in `ParallelSearchTask.cs`

Add a private method that drains the bounded channel and performs, for each completed database:

1. transient PSM TSV write
2. optional all-PSM TSV write
3. transient peptide TSV write
4. optional all-peptide TSV write
5. transient protein-group TSV write
6. optional all-protein-group TSV write
7. optional spectral library writes
8. per-database `results.txt`
9. optional output-folder compression
10. append the checkpoint row to `ManySearchSummary.csv`

The writer loop should use the existing helper methods already in `ParallelSearchTask`:

- `WritePsmsToTsvAsync(...)`
- `WriteProteinGroupsToTsvAsync(...)`
- `WriteSpectralLibraryAsync(...)`
- `WriteIndividualDatabaseResultsTextAsync(...)`

It should also continue to call `FinishedWritingFile(...)` as files complete.

### 3. Start the writer loop in `RunSpecific(...)`

After:

- `_resultsManager = CreateResultsManager(outputFolder);`
- cache checks
- `Initialize(...)`

and before the `Parallel.ForEach(...)` over transient databases:

- create the channel
- start the background writer task

### 4. Drain the writer loop in `RunSpecific(...)` before finalization

After the `Parallel.ForEach(...)` completes, and before any of the following:

- de novo merge into cached metrics
- precursor decon backfill
- `_resultsManager.RunStatisticalAnalysis()`

do this in sequence:

1. complete the channel writer
2. wait for the writer-loop task to finish draining

This guarantees that all per-database outputs and checkpoint rows are durable before final statistics start.

### 5. Remove inline per-database writing from `PerformPostSearchAnalysis(...)`

Keep in `PerformPostSearchAnalysis(...)`:

- FDR and peptide collapse
- parsimony and protein-group preparation
- creation of `TransientDatabaseContext`
- `_resultsManager.ProcessDatabase(...)`

Remove from this method as the inline hot path:

- transient PSM / peptide / protein TSV writes
- optional all-results TSV writes
- spectral library writes
- per-database `results.txt`

The method should return both:

- the `TransientDatabaseContext`
- the `TransientDatabaseMetrics`

No new DTO file is needed.

### 6. Enqueue completed databases in `ProcessTransientDatabase(...)`

After `PerformPostSearchAnalysis(...)` returns:

- enqueue `(context, metrics)` to the bounded channel
- let the background writer own durable file output

`ProcessTransientDatabase(...)` should no longer compress the folder directly, because compression must wait until the background writer finishes file output.

### 7. Change `TransientDatabaseResultsManager.ProcessDatabase(...)` to in-memory cache only

Replace:

- `_analysisCache.AddAndWrite(analysisResult);`

with:

- `_analysisCache.Add(analysisResult);`

This prevents a database from being marked complete in the cache CSV before its actual per-database files have finished writing.

### 8. Add a narrow checkpoint-append method to `TransientDatabaseResultsManager.cs`

Add a public method that delegates to `ParallelSearchResultCache.AppendToFile(...)`, for example:

- `AppendCheckpoint(TransientDatabaseMetrics result)`

The background writer loop should call this only after all per-database files and optional compression succeed.

### 9. Preserve the completion invariant

A transient database is considered complete only after:

1. its per-database output files are written
2. optional compression is finished
3. its row is appended to `ManySearchSummary.csv`

This keeps resumability behavior intact:

- if the row exists, the database outputs are durable
- if the run stops before the row append, the database is not treated as complete on rerun

### 10. Keep final aggregate outputs unchanged

Do not change:

- `TransientDatabaseResultsManager.WriteAllResults(...)`
- `ParallelSearchTask.WriteFinalOutputs(...)`
- final writing of `StatisticalAnalysis_Results.csv`, `Test_Results.csv`, `CalibrationReport.txt`, `ParallelSearchSummary.txt`, or the final ordered `ManySearchSummary.csv`

## Validation Checklist

1. Normal run
- per-database files are still produced with the same names and schemas
- `ManySearchSummary.csv` still tracks completed databases
- final aggregate outputs are unchanged

2. Interrupted run
- stop the task after several databases complete
- rerun with overwrite off
- databases already written and checkpointed skip cleanly

3. Compression enabled
- compression happens only after file output is complete
- checkpoint append happens after compression

4. Fast-path cache hit
- when all databases are cached, the search phase is skipped and finalization still works cleanly

## Optional Improvements After The Core Change

### A. Chunk TSV writes in `WritePsmsToTsvAsync(...)`

Replace one-line-at-a-time writing with buffered `StringBuilder` flushes every `N` lines or after a character threshold.

### B. Chunk protein-group writes in `WriteProteinGroupsToTsvAsync(...)`

Apply the same buffered flush pattern there.

### C. Keep `ManySearchSummary.csv` append writer warm

If appending the checkpoint row still shows up in profiling, optimize `ParallelSearchResultCache.AppendToFile(...)` to reuse an append session during the transient database phase.

### D. Make queue capacity configurable

If later needed, expose the bounded queue capacity as a `ParallelSearchParameters` setting. Start with `2` for spinning-disk friendly behavior.

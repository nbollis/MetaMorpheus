# Post-Search Analysis Statistical Testing

This page documents the statistical testing layer used after `ParallelSearchTask` has already produced per-database analysis metrics. It explains how tests are assembled, how p-values and q-values are produced across all transient databases, how family-aware summaries are created, how combined significance is computed, and which output files expose the results.

## Key Files

- [`../ParallelSearchTask.cs`](../ParallelSearchTask.cs)
- [`../TransientDatabaseResultsManager.cs`](../TransientDatabaseResultsManager.cs)
- [`../Statistics/TestCollection.cs`](../Statistics/TestCollection.cs)
- [`../Statistics/IStatisticalTest.cs`](../Statistics/IStatisticalTest.cs)
- [`../Statistics/StatisticalTestBase.cs`](../Statistics/StatisticalTestBase.cs)
- [`../Statistics/StatisticalTestResult.cs`](../Statistics/StatisticalTestResult.cs)
- [`../Statistics/TestSummary.cs`](../Statistics/TestSummary.cs)
- [`../Statistics/MultipleTestingCorrection.cs`](../Statistics/MultipleTestingCorrection.cs)
- [`../Statistics/MetaAnalysis.cs`](../Statistics/MetaAnalysis.cs)
- [`../Statistics/Tests/GaussianTest.cs`](../Statistics/Tests/GaussianTest.cs)
- [`../Statistics/Tests/NegativeBinomialTest.cs`](../Statistics/Tests/NegativeBinomialTest.cs)
- [`../Statistics/Tests/PermutationTest.cs`](../Statistics/Tests/PermutationTest.cs)
- [`../Statistics/Tests/FisherExactTest.cs`](../Statistics/Tests/FisherExactTest.cs)
- [`../Statistics/Tests/KolmogorovSmirnovTest.cs`](../Statistics/Tests/KolmogorovSmirnovTest.cs)
- [`../IO/StatisticalTestResultFile.cs`](../IO/StatisticalTestResultFile.cs)
- [`../IO/TestSummaryResultFile.cs`](../IO/TestSummaryResultFile.cs)

## Scope

- This page covers only the statistical testing phase of post-search analysis.
- It assumes `TransientDatabaseResultsManager.ProcessDatabase(...)` has already cached one `TransientDatabaseMetrics` result per transient database.
- It does not describe how collectors populate metrics, except where that is needed to explain test inputs.

## Statistical Testing Flow

1. `ParallelSearchTask.CreateResultsManager(...)` builds the active test list from `TestCollection`.
2. Each transient database is processed into a cached `TransientDatabaseMetrics` object by `TransientDatabaseResultsManager.ProcessDatabase(...)`.
3. `ParallelSearchTask` calls `TransientDatabaseResultsManager.RunStatisticalAnalysis()` after all databases are processed.
4. `RunStatisticalAnalysis()` calls `ComputePValuesForAllDatabases(...)` to run every active `IStatisticalTest` across the full cached database set.
5. Results are grouped by `TestName` and `MetricName`, then corrected with `MultipleTestingCorrection.BenjaminiHochberg(...)`.
6. A `TestSummary` is created for each test-metric grouping, and synthetic family-summary rows are added per `StatisticalEvidenceFamily`.
7. `ApplyCombinedPValues(...)` computes one synthetic `Combined | All` result per database via `MetaAnalysis.CombinePValuesAcrossTests(...)`.
8. `WriteAllResults(...)` writes the aggregate CSV outputs.

Intent: rank transient databases against the searched population using multiple independent evidence axes instead of a single count-based score.

```text
RunStatisticalAnalysis()
|
+-- Input: all cached `TransientDatabaseMetrics` from `_analysisCache.AllResultsList`
|
+-- ComputePValuesForAllDatabases(searchResults)
    |
    +-- Parallel loop over configured `_tests`
        |
        +-- For one `IStatisticalTest`
            |
            +-- `test.CanRun(searchResults)`?
            |   |
            |   +-- No  -> warn, add test to `toRemove`, do not emit results
            |   |
            |   +-- Yes -> call `test.RunTest(searchResults, _alpha)`
            |           |
            |           +-- throws?
            |           |   |
            |           |   +-- Yes -> warn, add test to `toRemove`, do not emit results
            |           |   |
            |           |   +-- No
            |           |
            |           +-- `test.SignificantResults >= resultCount / 10`?
            |               |
            |               +-- Yes -> warn, reject test as too noisy, add to `toRemove`
            |               |
            |               +-- No  -> convert each database p-value into one `StatisticalTestResult`
            |                         with `TestName`, `MetricName`, `PValue`, and `TestStatistic`
            |
            +-- After loop: remove all rejected tests from `_tests`
            +-- Update each database's legacy and family-aware summary fields:
                `StatisticalTestsRun`, `StatisticalTestsPassed`, `TestPassedRatio`,
                `ValidTestCount`, `PassedTestCount`, `ValidFamilyCount`, `PassedFamilyCount`,
                and per-family best/count summaries
|
+-- Group surviving `StatisticalTestResult` rows by `result.Key`
|      where `result.Key = $"{TestName}_{MetricName}"`
|
+-- For each surviving test-metric group
    |
    +-- collect non-NaN p-values by database
    +-- apply `MultipleTestingCorrection.BenjaminiHochberg(...)`
    +-- fill `QValue` on each result row
    +-- cache rows in `_statTestResultCache`
    +-- create one per-test `TestSummary`
|
+-- Build synthetic family summary rows
    |
    +-- one row per `StatisticalEvidenceFamily`
    +-- count databases with at least one defined result in that family
    +-- count databases with at least one significant result in that family
    +-- store rows with `IsFamilySummary = true`
|
+-- `ApplyCombinedPValues(statisticalResults)`
    |
    +-- combine each database's surviving test p-values with
        `MetaAnalysis.CombinePValuesAcrossTests(...)`
    +-- apply Benjamini-Hochberg again to combined p-values
    +-- store synthetic `Combined | All` rows in `_statTestResultCache["Combined"]`
|
+-- `WriteAllResults(...)`
    |
    +-- `ManySearchSummary.csv`
    +-- `StatisticalAnalysis_Results.csv`
    +-- `Test_Results.csv`
```

Intent: show the per-test decision path first, then the group-level correction and final combined-result steps that only happen for surviving tests.

## Core Types

### `IStatisticalTest`

- Purpose: common contract for all test implementations.
- Inputs: a `List<TransientDatabaseMetrics>` covering all transient databases.
- Outputs: per-database p-values, optional test statistic values, and a `CanRun(...)` gate.
- Intent: let `TransientDatabaseResultsManager` treat all tests uniformly.

### `StatisticalTestBase`

- Purpose: shared implementation for `RunTest(...)`, default `CanRun(...)`, equality, evidence-family metadata, and numeric helpers.
- Inputs: metric name, `StatisticalEvidenceFamily`, and optional per-database `isDefinedFor` predicate supplied by each concrete test.
- Outputs: `SignificantResults` count and per-database p-values from `ComputePValues(...)`.
- Intent: centralize cross-test behavior so each concrete test only needs to implement its scoring logic and structural validity rules.

### `StatisticalTestResult`

- Purpose: store one database-specific result for one test-metric pair.
- Inputs: `DatabaseName`, `TestName`, `MetricName`, `PValue`, `QValue`, optional `TestStatistic`, and optional `EffectSize`.
- Outputs: CSV-ready result objects plus helper properties like `NegLog10PValue`, `NegLog10QValue`, `EvidenceFamily`, and `IsSignificant(...)`.
- Intent: give the finalization and output layers one uniform result shape.

### `TestSummary`

- Purpose: summarize one test-metric grouping across all databases.
- Inputs: all `StatisticalTestResult` rows sharing the same `TestName` and `MetricName`.
- Outputs: `ValidDatabases`, `UndefinedDatabases`, `SignificantByP`, `SignificantByQ`, and optional `IsFamilySummary` / `EvidenceFamily` metadata.
- Intent: describe how informative each test was over the current transient database population.

## Test Assembly In `TestCollection`

`ParallelSearchTask.CreateResultsManager(...)` composes the active tests by concatenating named lists from [`../Statistics/TestCollection.cs`](../Statistics/TestCollection.cs). The exact set depends on whether protein-group and de novo metrics are available.

### Base Tests

- Purpose: test core enrichment and yield signals for transient-specific evidence.
- Metrics used:
  - unambiguous transient target PSM and peptide counts
  - transient confident target PSM and peptide counts
  - ambiguous vs unambiguous evidence
  - target vs decoy evidence
- Null / comparison model:
  - Gaussian and permutation tests compare each database against the cross-database population.
  - Negative binomial tests fit a count distribution across all databases.
  - Fisher exact tests compare one database against the rest of the dataset via a `2x2` contingency table.
- Interpretation: asks whether a transient database has unusually strong evidence relative to the searched transient database population.
- Notes:
  - rate-style tests normalize by `TransientPeptideCount`.
  - Fisher exact tests emit odds ratios into `TransientDatabaseMetrics.Results` under `FisherExact_<MetricName>_OddsRatio`.

### Score Distribution Tests

- Purpose: test whether score distributions shift in a favorable direction.
- Metrics used:
  - `PsmBacterialUnambiguousTargetScores`
  - `PeptideBacterialUnambiguousTargetScores`
- Null / comparison model: `KolmogorovSmirnovTest` compares each database array against a pooled background distribution built from all supplied arrays.
- Interpretation: asks whether one transient database shows a stronger score distribution than the background population.
- Notes:
  - `DistributionMinValuesThreshold` in `TestCollection` is a structural-validity gate for array-based tests, not a weak-signal screen.

### Protein Group Tests

- Purpose: extend enrichment testing to the protein-group layer.
- Metrics used:
  - confident transient protein-group rates
  - unambiguous transient protein-group counts
- Null / comparison model: same Gaussian, negative binomial, and permutation patterns used for PSMs and peptides.
- Interpretation: asks whether transient evidence still looks enriched after parsimony and protein-group FDR.
- Notes:
  - this family is only added when `SearchParameters.DoParsimony` is enabled.

### Retention Time Tests

- Purpose: score chromatographic plausibility.
- Metrics used:
  - `Psm_MeanAbsoluteRtError`
  - `Peptide_MeanAbsoluteRtError`
  - `Psm_AllRtErrors`
  - `Peptide_AllRtErrors`
- Null / comparison model:
  - Gaussian tests use `isLowerTailTest: true` because lower RT error is better.
  - K-S tests compare each error distribution against the pooled background with `KSAlternative.Greater`.
- Interpretation: asks whether a transient database is supported by lower-than-expected RT error.
- Notes:
  - scalar RT tests now rely on collector-emitted `NaN` values to mark structurally undefined cases instead of pre-filtering low-signal databases by count.

### Fragmentation Tests

- Purpose: evaluate fragment evidence quality for confident transient identifications.
- Metrics used:
  - complementary ion counts
  - bidirectional ion-series coverage
  - sequence coverage fractions
  - both full arrays and median summaries at PSM and peptide levels
- Null / comparison model:
  - K-S tests operate on full distributions.
  - Gaussian and permutation tests operate on median summaries.
- Interpretation: asks whether transient-supported identifications show stronger fragmentation evidence than the searched population.
- Notes:
  - median-based fragmentation tests now rely on the underlying summary value being defined rather than on a separate minimum-count screen.

### De Novo Tests

- Purpose: add orthogonal support from an external de novo mapping source.
- Metrics used:
  - prediction counts
  - target prediction counts
  - mapped peptide and protein counts
  - RT error arrays
  - score summaries and score arrays
- Null / comparison model: the same Gaussian, negative binomial, permutation, and K-S patterns used elsewhere, but only over de novo-derived metrics.
- Interpretation: asks whether a transient database has unusual de novo support relative to the searched population.
- Notes:
  - this family is only added when `DeNovoMappingCollector` is active.

## Concrete Test Implementations

### `GaussianTest<TNumeric>`

- Purpose: fit a normal model over one extracted metric across all databases and compute one-sided p-values.
- Metrics used: scalar counts, ratios, medians, or mean errors, depending on construction in `TestCollection`.
- Null / comparison model: global normal fit over all non-skipped databases.
- Interpretation: high values are favored by default; low values are favored only when `isLowerTailTest` is set.
- Notes:
  - used for enrichment-style metrics and lower-is-better RT error metrics.
  - effect size is the observed value divided by the mean value across all defined databases.

### `NegativeBinomialTest<TNumeric>`

- Purpose: model discrete count data with a negative binomial distribution and fall back toward Poisson behavior when variance does not exceed the mean.
- Metrics used: count-valued metrics such as transient PSMs, peptides, protein groups, and de novo counts.
- Null / comparison model: method-of-moments fit over the observed cross-database counts.
- Interpretation: asks whether one database count is unusually large relative to the fitted count model.
- Notes:
  - the class summary says the test normalizes by proteome size, but the implementation directly fits the extracted counts supplied by `TestCollection`.
  - any normalization therefore happens in the extractor, not inside `NegativeBinomialTest<TNumeric>` itself.
  - effect size is the observed count divided by the mean count across all defined databases.

### `PermutationTest<TNumeric>`

- Purpose: build an empirical null by redistributing observations across databases according to database size.
- Metrics used: either discrete counts or continuous summaries like medians and means.
- Null / comparison model:
  - count data uses multinomial redistribution.
  - continuous data uses weighted resampling from the observed organism values.
  - weights are based on `TransientProteinCount`.
- Interpretation: asks whether the observed value is unexpectedly large under a size-weighted random assignment model.
- Notes:
  - this is a cross-database outlier test, not a fixed biological-control comparison.
  - `PermutationTest.CanRun(...)` only requires at least two databases.
  - effect size is observed-to-expected ratio, using a size-weighted null expectation.

### `FisherExactTest`

- Purpose: compare one database against the rest of the dataset using unambiguous vs ambiguous or target vs decoy counts.
- Metrics used: integer count pairs supplied by `TestCollection`.
- Null / comparison model: one-sided Fisher exact test with `alternative: Greater`.
- Interpretation: significant results indicate enrichment for the favored evidence type in that database relative to the rest of the searched population.
- Notes:
  - odds ratios are stored in `TransientDatabaseMetrics.Results` and later exposed as `TestStatistic` values.
  - no-evidence cases are recorded as `double.NaN` p-values.
  - effect size is the odds ratio.

### `KolmogorovSmirnovTest`

- Purpose: compare one database-specific score or error distribution to a pooled background distribution.
- Metrics used: arrays such as scores, RT errors, complementary ion counts, and coverage values.
- Null / comparison model: two-sample K-S comparison against the pooled background array built from all databases.
- Interpretation: significance indicates a shifted distribution in the configured direction.
- Notes:
  - the code stores the K-S statistic in `TransientDatabaseMetrics.Results` under `KolmSmir_<MetricName>_KS`.
  - `KSAlternative.Less` means the sample is shifted toward higher values; `KSAlternative.Greater` means shifted toward lower values.
  - effect size is the median shift between the database-specific sample and the pooled background distribution.

## Finalization In `TransientDatabaseResultsManager`

### `ComputePValuesForAllDatabases(...)`

- Purpose: execute every configured test over the full cached database set.
- Inputs: `_tests` and `_analysisCache.AllResultsList`.
- Outputs: one `StatisticalTestResult` per database per surviving test.
- Intent: move from per-database metrics to cross-database significance.
- Notes:
  - tests run in parallel with `Parallel.ForEach(...)`.
  - tests failing `CanRun(...)` are removed.
  - tests are also removed when `test.SignificantResults >= resultCount / 10`.
  - surviving results update both legacy and family-aware summary fields on each `TransientDatabaseMetrics` object.
  - family-aware fields include `ValidTestCount`, `PassedTestCount`, `ValidFamilyCount`, `PassedFamilyCount`, and per-family best/count summaries.

### Benjamini-Hochberg Correction

- Purpose: control the false discovery rate within each test-metric grouping.
- Inputs: all p-values for one `TestName` + `MetricName` group.
- Outputs: `QValue` on each corresponding `StatisticalTestResult`.
- Intent: keep significance interpretation at the grouped-test level, not just raw p-values.

### Family-Level Summaries

- Purpose: summarize support at the evidence-family layer so correlated tests do not dominate the top-line counts.
- Inputs: all `StatisticalTestResult` rows grouped by `DatabaseName` or by `EvidenceFamily`, depending on the output being produced.
- Outputs:
  - per-database family-aware fields in `TransientDatabaseMetrics`
  - synthetic family rows in `TestSummaryResultsList`
- Intent: distinguish `many tests fired` from `multiple independent evidence families fired`.
- Notes:
  - `ValidFamilyCount` counts distinct evidence families with at least one defined result for a database.
  - `PassedFamilyCount` counts distinct evidence families with at least one significant result for a database.
  - per-family best evidence is currently stored as the best finite p-value and q-value observed within each family for a database.
  - `TestPassedRatio` is still written for backward compatibility, but Step 6 de-emphasizes it relative to family-aware summaries.

### `ApplyCombinedPValues(...)`

- Purpose: create one aggregate significance result per database.
- Inputs: all individual `StatisticalTestResult` rows produced before combination.
- Outputs: a synthetic `Combined | All` row for each database in `_statTestResultCache`.
- Intent: provide a single combined significance view across the active statistical tests.
- Notes:
  - combination uses `MetaAnalysis.CombinePValuesAcrossTests(...)`.
  - the default combining method is Fisher's method.
  - the combined p-values are themselves corrected again with Benjamini-Hochberg.

### `MetaAnalysis`

- Purpose: combine multiple p-values for the same database into one meta-analytic p-value.
- Inputs: all per-database p-values across test families.
- Outputs: one combined p-value per database.
- Intent: aggregate evidence across heterogeneous test families.
- Notes:
  - `PValueCombiningMethod` includes `Fishers`, `Brown`, and `KostMcDermott`.
  - `CombinePValuesAcrossTests(...)` defaults to `Fishers`.
  - Brown and Kost-McDermott paths fall back to Fisher when covariance or degrees-of-freedom estimates become invalid.

## Output Files

### `ManySearchSummary.csv`

- Purpose: persist the per-database metric summary.
- Produced by: `ParallelSearchResultCache.WriteAllToFile(...)` through `TransientDatabaseResultsManager.WriteAllResults(...)`.
- Key fields tied to statistical testing:
  - `PassedFamilyCount`
  - `ValidFamilyCount`
  - `PassedTestCount`
  - `ValidTestCount`
  - `TestPassedRatio`
  - per-family `*ValidTests`, `*PassedTests`, `*BestPValue`, and `*BestQValue` fields
- Intent: keep one sortable per-database summary that mixes metric values with both legacy and family-aware pass counts.

### `StatisticalAnalysis_Results.csv`

- Purpose: expose one row per database with columns for each statistical test result.
- Produced by: [`../IO/StatisticalTestResultFile.cs`](../IO/StatisticalTestResultFile.cs).
- Includes:
  - leading summary fields for passed/valid tests and passed/valid families
  - `pValue_Combined_All`, `qValue_Combined_All`, `isSignificant_Combined_All`
  - per-test `pValue_*`, `qValue_*`, `isSignificant_*`
  - per-test `effectSize_*`
  - optional `testStatistic_*` columns when available
  - taxonomy columns from `TaxonomyMapping.GetTaxonomyInfo(databaseName)`
- Intent: provide the most complete cross-database statistical output in one file.

### `Test_Results.csv`

- Purpose: summarize how each test behaved across the full database set.
- Produced by: [`../IO/TestSummaryResultFile.cs`](../IO/TestSummaryResultFile.cs).
- Includes:
  - `TestName`
  - `MetricName`
  - `EvidenceFamily`
  - `ValidDatabases`
  - `UndefinedDatabases`
  - `SignificantByP`
  - `SignificantByQ`
- Intent: make it easy to inspect which tests were informative, sparse, or broadly permissive.
- Notes:
  - Step 6 adds synthetic family-summary rows with `IsFamilySummary = true` so the file contains both per-test and per-family rollups.

## Interpreting Significance

- `StatisticalTestResult.IsSignificant(...)` defaults to q-value significance, not raw p-value significance.
- A database can have many significant individual tests without necessarily being the strongest combined result.
- `Combined | All` is the most compact aggregate signal, but it depends on which underlying tests survived filtering.
- `TestPassedRatio` is population-relative and changes when tests are added, removed, or filtered out.
- `PassedFamilyCount` is often a more stable top-line ranking signal than raw passed-test counts because it reduces within-family redundancy.

## Known Caveats / Open Questions

- The statistical layer is population-relative. Most tests compare one transient database to the set of searched transient databases, not to a fixed biological control.
- `NegativeBinomialTest<TNumeric>` describes proteome-size normalization in its summary, but the actual implementation fits the extracted counts directly.
- `PermutationTest<TNumeric>` weights null redistribution by `TransientProteinCount`, so database size assumptions materially affect p-values.
- `ComputePValuesForAllDatabases(...)` removes tests with `>= 10%` significant p-values. This helps suppress noisy tests, but it also changes the meaning of `StatisticalTestsRun`, `StatisticalTestsPassed`, and `Combined | All`.
- K-S tests use pooled background arrays built from all databases. That is convenient, but it means the null is the observed search population, not an external reference distribution.
- Step 6 adds family-aware summaries, but the current `Combined | All` meta-analysis is still test-level rather than hierarchical family-first aggregation. That is the next planned refactor step.

## Related Pages

- [Parallel Search Wiki](README.md)

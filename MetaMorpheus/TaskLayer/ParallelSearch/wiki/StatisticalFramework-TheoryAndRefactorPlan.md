	# Statistical Framework Theory and Refactor Plan

This page captures the intended statistical interpretation of the parallel-search post-analysis layer after the search engine has already produced reliable per-database evidence. It is written for the specific use case where almost all transient databases are expected to be true negatives, while a very small number may contain real biological signal. The first half is manuscript-style prose that can be adapted into a methods or discussion section; the second half turns that theory into a stepwise refactor plan for the current code.

## Key Files

- [`../TransientDatabaseResultsManager.cs`](../TransientDatabaseResultsManager.cs)
- [`../Statistics/Suite/TestSuiteBuilder.cs`](../Statistics/Suite/TestSuiteBuilder.cs)
- [`../Statistics/Abstractions/IStatisticalTest.cs`](../Statistics/Abstractions/IStatisticalTest.cs)
- [`../Statistics/Abstractions/StatisticalTestBase.cs`](../Statistics/Abstractions/StatisticalTestBase.cs)
- [`../Statistics/Results/StatisticalTestResult.cs`](../Statistics/Results/StatisticalTestResult.cs)
- [`../Statistics/Results/StatisticalTestResultBuilder.cs`](../Statistics/Results/StatisticalTestResultBuilder.cs)
- [`../Statistics/Correction/MultipleTestingCorrection.cs`](../Statistics/MultipleTestingCorrection.cs)
- [`../Statistics/Correction/MetaAnalysis.cs`](../Statistics/Correction/MetaAnalysis.cs)
- [`../Statistics/Correction/ICorrelationEstimator.cs`](../Statistics/Correction/ICorrelationEstimator.cs)
- [`../Statistics/Services/StatisticalTestExecutor.cs`](../Statistics/Services/StatisticalTestExecutor.cs)
- [`../Statistics/Services/StatisticalSummaryBuilder.cs`](../Statistics/Services/StatisticalSummaryBuilder.cs)
- [`../Statistics/Services/HierarchicalCombinedScoringService.cs`](../Statistics/Services/HierarchicalCombinedScoringService.cs)
- [`../Analysis/TransientDatabaseMetrics.cs`](../Analysis/TransientDatabaseMetrics.cs)
- [`../ParallelSearchTask.cs`](../ParallelSearchTask.cs)

## Scope

- This page addresses the theory and redesign of the post-search statistical framework.
- It assumes the search component and metric collection layer are already functioning as intended.
- It focuses on how the statistical layer should behave when the vast majority of transient databases are expected to be biologically absent from the sample.

## Manuscript Prose

The post-search statistical layer in parallel search should be understood as a rare-target evidence integration system rather than as a conventional balanced hypothesis-testing panel. In the intended application, most transient databases represent organisms that are not expected to be present in the analyzed sample. Consequently, the dominant outcome is not moderate evidence spread across many candidate databases, but rather a large background mass of databases with little or no support and a very small tail of databases with potentially meaningful evidence. Under this setting, a highly zero-inflated distribution of passed statistical tests is not itself a sign of methodological failure. On the contrary, it is the pattern expected when the biological prior strongly favors absence for nearly all searched organisms.

This interpretation changes the role of the individual statistical tests. The purpose of each test is not to guarantee that every database receives a useful inferential statement on every metric. Instead, each test acts as one evidence channel that asks whether a particular transient database appears unusually strong along a specific axis, such as confident identification yield, ambiguity structure, score distribution, fragmentation quality, retention-time agreement, protein-group support, or de novo corroboration. A database that is truly absent should usually fail to accumulate evidence across these channels. A database that is biologically relevant should tend to separate from the null bulk by showing coherent support across multiple partially independent evidence families.

This view also clarifies the treatment of missing or excluded test results. A database-specific `NaN` is statistically defensible when it means that a test is undefined for that database, for example because there are too few observations to estimate a distribution or because an optional evidence layer was not produced. In that case, the missing value reflects structural non-identifiability rather than a failed result. By contrast, excluding a database from a test merely because its observed signal is weak is not theoretically attractive in this setting, since weak signal is precisely the expected behavior for most null databases. Low counts, sparse support, and near-zero evidence should therefore generally remain part of the null population rather than being filtered away from it.

The same rare-target framing also argues against outcome-driven rejection of whole tests. A test should not be discarded simply because it produces few discoveries or because its discoveries are sparse and concentrated in the extreme tail. In fact, that may be the desired behavior for a useful screen in a problem dominated by true negatives. Whole-test removal is more justifiable when it follows from design-time or calibration-time considerations, such as structural inapplicability, missing inputs, demonstrably poor null calibration, or unacceptable redundancy with another evidence family. The key distinction is that a test should be removed because its assumptions or calibration are unsound, not because its realized p-value pattern is inconvenient.

Taken together, these considerations suggest that the current framework is best interpreted as a multi-evidence ranking system whose statistical outputs help prioritize transient databases for follow-up rather than as a single clean inferential procedure with uniformly calibrated per-database conclusions. Within that interpretation, quantities such as `StatisticalTestsPassed`, `StatisticalTestsRun`, and `TestPassedRatio` are still useful, but they should be treated as screening summaries rather than as standalone endpoints. Their primary value is operational: they compress a complex evidence profile into an easily sortable summary. Their limitation is that they depend on the set of tests that were valid for a given database and on the correlation structure among the tests themselves.

For this reason, the most principled long-term direction is to treat the framework as a layered system with separate notions of eligibility, evidence, integration, and ranking. Eligibility determines whether a test is statistically defined for a database. Evidence is represented by the per-test p-value, q-value, and effect size for each valid comparison. Integration occurs within broader evidence families so that redundant tests do not dominate the final interpretation. Ranking then places each transient database within the empirical null landscape, highlighting those candidates whose support is both stronger and more internally consistent than the overwhelming background bulk. Under this formulation, a database supported by several distinct evidence families is more compelling than one that passes several closely related tests within a single family.

An empirical calibration strategy is especially important in this application. Because the searched database set is very large and the prevalence of true positives is expected to be extremely low, theoretical p-values alone are unlikely to provide the full operating characteristics needed for practical use. Instead, the framework should be validated against known-positive and known-negative scenarios, including spike-in experiments and label-shuffled null analyses. These calibration exercises can reveal whether the null bulk remains well behaved, whether certain evidence families are systematically anti-conservative, and where useful operating thresholds lie for prioritizing follow-up databases. In practice, the final system should be expected to function as a rare-target prioritization engine whose strength comes from integrating multiple weak-to-moderate signals into a more robust composite profile.

## Design Implications

- `NaN` should mean a test was undefined, not that the database was uninteresting.
- Low counts and sparse evidence should usually remain in the tested null population.
- Whole-test removal should be driven by predefined validity or calibration rules, not by realized significance rates.
- Test outputs should be interpreted at the evidence-family level before they are reduced to one global ranking score.
- `StatisticalTestsPassed` is useful as a screening feature, but it should not be treated as a calibrated endpoint on its own.
- Empirical calibration with positive and null controls is part of the statistical design, not an afterthought.

## Stepwise Refactor Plan

### Step 1: Formalize Test Eligibility

- Add an explicit distinction between `test is undefined` and `test was run but not significant`.
- Replace ad hoc `ShouldSkip` semantics with a clearer eligibility contract at the `IStatisticalTest` level.
- Preferred shape:
  - `CanRun(allResults)` remains a whole-test capability check.
  - add a per-database method or result code such as `IsDefinedFor(result)` or `Evaluate(result) -> Defined/Undefined`.
- Goal: ensure `NaN` only represents structural inapplicability.

### Step 2: Standardize Result Semantics

- Extend `StatisticalTestResult` to capture result state explicitly.
- Add fields such as:
  - `IsDefined`
  - `EligibilityReason`
  - `EffectSize`
  - optional `EvidenceFamily`
- Keep `PValue` and `QValue`, but stop overloading `NaN` as the only signal for exclusion.
- Goal: make downstream summaries able to distinguish undefined, null, and positive evidence.

### Step 3: Reorganize Test Registration By Evidence Family

- Group tests into explicit families via `TestSuiteBuilder` family-specific methods:
  - `AddCountEnrichmentTests()` → `CountEnrichment`
  - `AddAmbiguityOrTargetDecoyTests()` → `AmbiguityOrTargetDecoy`
  - `AddScoreDistributionTests()` → `ScoreDistribution`
  - `AddFragmentationTests()` → `Fragmentation`
  - `AddRetentionTimeTests()` → `RetentionTime`
  - `AddProteinGroupTests()` → `ProteinGroup`
  - `AddDeNovoTests()` → `DeNovo`
- `AddFamily(StatisticalEvidenceFamily)` dispatches to the correct method by enum value.
- The old `TestCollection` static class has been eliminated; all test registrations are now in `TestSuiteBuilder`.
- Store family membership in test metadata rather than inferring it from metric names.
- Goal: enable family-level reporting and reduce overcounting from correlated tests.

### Step 4: Remove Weak-Signal Exclusion Rules

- Audit each existing skip threshold in `TestSuiteBuilder` (formerly `TestCollection`).
- Keep thresholds that define mathematical validity, such as insufficient array length for distribution tests.
- Remove thresholds that merely exclude low-signal but still valid count or scalar observations.
- Goal: keep the null population honest in a setting where most databases are expected to be near zero.

Implementation note:

- Count-based and scalar tests should prefer finite extracted values over minimum-count gates.
- Distribution tests may still require minimum array sizes because that is a structural validity requirement rather than a weak-signal screen.

### Step 5: Introduce Effect Sizes As First-Class Outputs

- For each test family, define one or more effect-size measures alongside p-values.
- Examples:
  - count/rate enrichment ratios
  - odds ratios for contingency-style tests
  - median score or RT shift
  - fragmentation median shifts
- Persist these in `TransientDatabaseMetrics.Results` and surface them in `StatisticalTestResult` where appropriate.
- Goal: let ranking prefer large, biologically interpretable shifts rather than significance alone.

### Step 6: Rework Final Summaries Around Families

- In `TransientDatabaseResultsManager`, compute and store:
  - `ValidTestCount`
  - `PassedTestCount`
  - `ValidFamilyCount`
  - `PassedFamilyCount`
  - family-specific combined or best evidence values
- De-emphasize raw `TestPassedRatio` as the primary ranking endpoint.
- Goal: make summaries more robust to within-family redundancy.

Implementation note:
- The current implementation stores `ValidTestCount`, `PassedTestCount`, `ValidFamilyCount`, `PassedFamilyCount`, and per-family best `PValue` / `QValue` summaries on `TransientDatabaseMetrics`.
- `ManySearchSummary.csv`, `StatisticalAnalysis_Results.csv`, `Test_Results.csv`, and `ParallelSearchSummary.txt` now expose family-aware summary information.
- Legacy `StatisticalTestsPassed`, `StatisticalTestsRun`, and `TestPassedRatio` are still preserved for backward compatibility, but the main ordering now prefers passed families before passed tests.

### Step 7: Make Combined Scoring Explicitly Hierarchical

- Combine evidence in two stages:
  1. combine tests within a family
  2. combine family-level evidence across families
- Keep the current `MetaAnalysis` utilities if useful, but apply them at the correct layer.
- Goal: prevent one dense family from dominating the final score purely because it contributes more tests.

Implementation note:
- The current implementation now emits synthetic `Combined | <Family>` rows first, applies Benjamini-Hochberg across databases within each family-combined group, and then builds `Combined | All` from those family-level combined rows.
- `TransientDatabaseMetrics` now stores `CombinedPValue`, `CombinedQValue`, and per-family `*CombinedPValue` / `*CombinedQValue` fields alongside the Step 6 best-evidence summaries.

### Step 8: Separate Calibration Diagnostics From Production Ranking

- Add a calibration mode that records:
  - null p-value histograms
  - family-level hit rates
  - effect-size distributions
  - combined-score distributions
- Use known spike-in datasets and shuffled-label null runs to characterize operating behavior.
- Goal: define practical thresholds from observed null and positive-control behavior rather than from arbitrary cutoffs.

### Step 9: Update Output Files And Documentation

- Extend output writers so `StatisticalAnalysis_Results.csv` and summary files expose:
  - defined vs undefined status
  - evidence family
  - effect size
  - family-level summaries
- Update the wiki pages covering metrics and statistical testing so they reflect the new semantics.
- Goal: keep developer-facing and user-facing outputs aligned with the redesigned theory.

### Step 10: Migrate Ranking Logic In `ParallelSearchTask`

- Update the final database-selection logic that currently relies on counts of significant tests.
- Replace pure `StatisticalTestsPassed` style thresholds with a composite that can incorporate:
  - passed families
  - combined family evidence
  - effect sizes
  - empirical tail position relative to the null bulk
- Goal: make downstream database-writing decisions better match the rare-target detection problem.

## Suggested Implementation Order

1. Add explicit defined/undefined semantics to test results.
2. Audit and narrow skip rules to structural validity only.
3. Add evidence-family metadata to `IStatisticalTest` implementations.
4. Add effect-size support to result objects and writers.
5. Refactor `TransientDatabaseResultsManager` summaries from test-count based to family-aware summaries.
6. Rework combined scoring into within-family then across-family integration.
7. Add calibration outputs and validate on known-positive and null datasets.
8. Update final ranking and database-selection logic.

## Known Caveats / Open Questions

- The exact statistical replacement for every current test does not need to be decided before the semantic cleanup. Eligibility, family structure, and output meaning can be fixed first.
- If certain current tests remain useful mainly as heuristics, that is acceptable as long as the outputs are described as ranking evidence rather than strict hypothesis-test conclusions.
- Calibration should be treated as a required part of the redesign because the intended operating regime is dominated by true negatives and a small positive tail.

## Completed Refactor Phases

### Phase A–C: God-Class Decomposition (Completed)

Extracted `StatisticalTestExecutor`, `StatisticalSummaryBuilder`, `HierarchicalCombinedScoringService`, and `TransientDatabaseMetricsFamilySummaryMapper` from `TransientDatabaseResultsManager`. Reorganized files into `Statistics/Abstractions/`, `Results/`, `Services/`, `Correction/`, `Suite/`.

### Phase D: Staged Result Construction (Completed)

Created `StatisticalTestResultBuilder` with fluent API for building `StatisticalTestResult` instances, enforcing required fields and defaulting undefined results correctly.

### Phase E: TestSuiteBuilder (Completed)

Created `TestSuiteBuilder` with family-specific registration methods. Eliminated `TestCollection` static class; all test registrations now live in `TestSuiteBuilder` methods (`AddCountEnrichmentTests()`, `AddAmbiguityOrTargetDecoyTests()`, etc.). `AddFamily(StatisticalEvidenceFamily)` dispatches by enum.

### Phase F: Correlation Estimation Abstraction (Completed)

Extracted `ICorrelationEstimator` interface with two implementations: `TestStatisticCorrelationEstimator` (uses test-statistic correlation for Brown's method) and `IndependenceCorrelationEstimator` (identity matrix, assuming independence). `MetaAnalysis.CombinePValuesAcrossTests` now accepts an optional `ICorrelationEstimator` parameter.

### Phase G: Two-Stage Hierarchical Combination Strategy (Completed)

Extended `HierarchicalCombinedScoringService` to accept separate combination strategies for within-family and across-family stages:

- **Within-family**: defaults to `Brown` + `TestStatisticCorrelationEstimator` — tests within a single evidence family share underlying data, so Brown's method adjusts for the resulting correlation.
- **Across-family**: defaults to `Fishers` + `IndependenceCorrelationEstimator` — evidence families capture different signal axes and are more plausibly independent.

`TransientDatabaseResultsManager` exposes four parameters (`withinFamilyCorrelationEstimator`, `withinFamilyCombiningMethod`, `acrossFamilyCorrelationEstimator`, `acrossFamilyCombiningMethod`) with smart defaults matching the above. `ParallelSearchTask` uses these defaults, so no changes were needed at the call site.

This closes out Step 7 from the plan: combined scoring is now explicitly hierarchical with statistically principled defaults.

## Related Pages

- [Parallel Search Wiki](README.md)
- [Post-Search Analysis Metrics and Collectors](PostSearchAnalysis-MetricsAndCollectors.md)
- [Post-Search Analysis Statistical Testing](PostSearchAnalysis-StatisticalTesting.md)

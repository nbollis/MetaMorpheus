# Baseline-Human Reconciliation Truth Map

This document maps the exact code path for how ParallelSearch reconciles transient database evidence against a one-time baseline search that includes human proteins, and where each reconciliation metric is computed.

## Scope

- Focus: baseline-vs-transient reconciliation logic and derived metrics.
- Out of scope: full fragmentation, retention-time, and de novo metric details.

## Ground Truth Pipeline

1. **Baseline search is run once**
   - `ParallelSearchTask.Initialize(...)` creates `BaseSearchPsms` by searching persistent databases only.
   - Source: `TaskLayer/ParallelSearch/ParallelSearchTask.cs`.

2. **Each transient DB starts from cloned baseline PSMs**
   - `CloneBasePsms()` deep-clones peptide PSMs from `BaseSearchPsms`.
   - `PsmFdrInfo` and `PeptideFdrInfo` are reset to `null` before transient search.
   - Commented behavior: ClassicSearchEngine can `AddOrReplace` candidates on top of the cloned baseline candidates.
   - Source: `TaskLayer/ParallelSearch/ParallelSearchTask.cs`.

3. **Transient search runs against transient proteins only**
   - `PerformSearch(transientProteins, psmArray, ...)` mutates the cloned array.
   - Source: `TaskLayer/ParallelSearch/ParallelSearchTask.cs`.

4. **Post-search normalization before metrics**
   - `PerformPostSearchAnalysis(...)` resolves ambiguities, deduplicates best PSM per `(file, scan, mass)`, runs FDR and disambiguation, then builds filtered sets.
   - Source: `TaskLayer/ParallelSearch/ParallelSearchTask.cs`.

5. **Transient-labeled subsets are created**
   - `FilterToTransientDatabaseOnly(...)`: includes PSM/peptide if **any** best match accession is in transient DB accession set.
   - `FilterProteinGroupsToTransientDatabaseOnly(...)`: includes protein group if **any** protein accession is in transient DB accession set.
   - Note: comments mention "exclusively" / "all proteins" but implementation is `Any(...)`.
   - Source: `TaskLayer/ParallelSearch/ParallelSearchTask.cs`.

6. **Collectors compute reconciliation metrics from context**
   - `TransientDatabaseContext` carries `AllPsms`, `TransientPsms`, `AllPeptides`, `TransientPeptides`, optional protein groups, and DB-size metadata.
   - `MetricAggregator.RunAnalysis(...)` executes enabled collectors and merges outputs into `TransientDatabaseMetrics.Results`.
   - Source: `TaskLayer/ParallelSearch/Analysis/TransientDatabaseContext.cs`, `TaskLayer/ParallelSearch/Analysis/MetricAggregator.cs`.

7. **Statistical reconciliation over all transient DBs**
   - `RunStatisticalAnalysis()` runs all tests over cached `TransientDatabaseMetrics`, applies BH correction by `(TestName, MetricName)`, and stores per-DB pass counts.
   - Source: `TaskLayer/ParallelSearch/TransientDatabaseResultsManager.cs`.

## Core Semantics

- **"Baseline-human reconciliation" signal** is primarily encoded as:
  - transient-attributed evidence (`Transient*` sets), and
  - organism ambiguity against target organism string (`"Homo sapiens"` by default).
- **Ambiguous vs unambiguous rule**:
  - In PSM/peptide and protein-group collectors, evidence is marked *organism-ambiguous* if any best match/protein has `Organism` containing target string.
  - Non-ambiguous evidence contributes to `*Unambiguous*` metrics.
  - Source: `TaskLayer/ParallelSearch/Analysis/Collectors/PsmPeptideSearchCollector.cs`, `TaskLayer/ParallelSearch/Analysis/Collectors/ProteinGroupCollector.cs`.

## Metric Truth Map

The table below lists reconciliation metrics and where they are computed.

| Metric (TransientDatabaseMetrics) | Computed in | Definition in code |
|---|---|---|
| `TotalProteins` | `BasicMetricCollector.CollectData` | `context.TotalProteins` (base + transient count passed into context) |
| `TransientProteinCount` | `BasicMetricCollector.CollectData` | `context.TransientProteinAccessions.Count` |
| `TransientPeptideCount` | `BasicMetricCollector.CollectData` | precomputed peptide count for transient DB (`context.TransientPeptideCount`) |
| `TargetPsmsAtQValueThreshold` | `BasicMetricCollector.CollectData` | count of non-decoy in `AllPsms` with PSM q-value <= `min(QValueThreshold, PepQValueThreshold)` |
| `TargetPsmsFromTransientDb` | `BasicMetricCollector.CollectData` | count of non-decoy in `TransientPsms` |
| `TargetPsmsFromTransientDbAtQValueThreshold` | `BasicMetricCollector.CollectData` | count of non-decoy in `TransientPsms` at threshold |
| `TargetPeptidesAtQValueThreshold` | `BasicMetricCollector.CollectData` | count of non-decoy in `AllPeptides` with peptide q-value <= threshold |
| `TargetPeptidesFromTransientDb` | `BasicMetricCollector.CollectData` | count of non-decoy in `TransientPeptides` |
| `TargetPeptidesFromTransientDbAtQValueThreshold` | `BasicMetricCollector.CollectData` | count of non-decoy in `TransientPeptides` at threshold |
| `PsmTargets` / `PsmDecoys` | `PsmPeptideSearchCollector.CollectData` | target/decoy counts from `AllPsms` after thresholding |
| `PsmBacterialTargets` / `PsmBacterialDecoys` | `PsmPeptideSearchCollector.CollectData` | target/decoy counts from `TransientPsms` after thresholding |
| `PsmBacterialAmbiguous` | `PsmPeptideSearchCollector.CollectData` | `PsmBacterialTargets - PsmBacterialUnambiguousTargets` |
| `PsmBacterialUnambiguousTargets` / `PsmBacterialUnambiguousDecoys` | `PsmPeptideSearchCollector.AnalyzeSpectralMatches` | counts in `TransientPsms` where no best match organism contains target string |
| `PsmBacterialUnambiguousTargetScores` / `PsmBacterialUnambiguousDecoyScores` | `PsmPeptideSearchCollector.AnalyzeSpectralMatches` | score arrays for unambiguous transient PSM targets/decoys |
| `PeptideTargets` / `PeptideDecoys` | `PsmPeptideSearchCollector.CollectData` | target/decoy counts from `AllPeptides` after thresholding |
| `PeptideBacterialTargets` / `PeptideBacterialDecoys` | `PsmPeptideSearchCollector.CollectData` | target/decoy counts from `TransientPeptides` after thresholding |
| `PeptideBacterialAmbiguous` | `PsmPeptideSearchCollector.CollectData` | `PeptideBacterialTargets - PeptideBacterialUnambiguousTargets` |
| `PeptideBacterialUnambiguousTargets` / `PeptideBacterialUnambiguousDecoys` | `PsmPeptideSearchCollector.AnalyzeSpectralMatches` | counts in `TransientPeptides` where no best match organism contains target string |
| `PeptideBacterialUnambiguousTargetScores` / `PeptideBacterialUnambiguousDecoyScores` | `PsmPeptideSearchCollector.AnalyzeSpectralMatches` | score arrays for unambiguous transient peptide targets/decoys |
| `ProteinGroupTargets` / `ProteinGroupDecoys` | `ProteinGroupCollector.CollectData` | target/decoy counts from all protein groups (if parsimony enabled) |
| `TargetProteinGroupsAtQValueThreshold` | `ProteinGroupCollector.CollectData` | set equal to `ProteinGroupTargets` from `AnalyzeProteinGroups` |
| `TargetProteinGroupsFromTransientDb` | `ProteinGroupCollector.CollectData` | non-decoy count in transient-filtered protein groups before q-value filter |
| `TargetProteinGroupsFromTransientDbAtQValueThreshold` | `ProteinGroupCollector.CollectData` | transient target protein groups after q-value and peptide-count filters |
| `ProteinGroupBacterialTargets` / `ProteinGroupBacterialDecoys` | `ProteinGroupCollector.AnalyzeProteinGroups` | transient protein-group target/decoy counts after filters |
| `ProteinGroupBacterialUnambiguousTargets` / `ProteinGroupBacterialUnambiguousDecoys` | `ProteinGroupCollector.AnalyzeProteinGroups` | transient protein groups without target-organism ambiguity |
| `StatisticalTestsRun` / `StatisticalTestsPassed` / `TestPassedRatio` | `TransientDatabaseResultsManager.ComputePValuesForAllDatabases` | per-DB counts over all retained statistical tests |

## Where Reconciliation Metrics Drive Statistical Decisions

`TestCollection.BaseTests` consumes the key reconciliation metrics:

- **Size-normalized enrichment**
  - `PsmBacterialUnambiguousTargets / TransientPeptideCount`
  - `PeptideBacterialUnambiguousTargets / TransientPeptideCount`
  - `TargetPsmsFromTransientDbAtQValueThreshold / TransientPeptideCount`
  - `TargetPeptidesFromTransientDbAtQValueThreshold / TransientPeptideCount`
  - Tests: Gaussian + Permutation.

- **Raw count overdispersion/enrichment**
  - `PsmBacterialUnambiguousTargets`, `PeptideBacterialUnambiguousTargets`
  - `TargetPsmsFromTransientDbAtQValueThreshold`, `TargetPeptidesFromTransientDbAtQValueThreshold`
  - Tests: NegativeBinomial.

- **Unambiguous vs ambiguous odds**
  - PSM: `PsmBacterialUnambiguousTargets` vs `PsmBacterialAmbiguous`
  - Peptide: `PeptideBacterialUnambiguousTargets` vs `PeptideBacterialAmbiguous`
  - Tests: FisherExact.

- **Target vs decoy odds in transient evidence**
  - PSM: `PsmBacterialTargets` vs `PsmBacterialDecoys`
  - Peptide: `PeptideBacterialTargets` vs `PeptideBacterialDecoys`
  - Tests: FisherExact.

## Final Representation Decision

- After all tests run, per-DB significance count is compared against:
  - `sigPassedCutoff = StatisticalTestCount * TestRatioForWriting`.
- DBs meeting cutoff are treated as significant for combined FASTA generation and optional follow-up search.
- Source: `TaskLayer/ParallelSearch/ParallelSearchTask.cs` (`WriteFinalOutputs`).

## Important Terminology Note

- Metric names still use `*Bacterial*` prefixes for historical reasons.
- In current logic they effectively mean **transient-database-attributed evidence**, with ambiguity judged relative to configured target organism (default `"Homo sapiens"`).

## FDR Alignment Refactor Plan (Three-Service Model)

### Scope for this iteration
- Use one alignment service instance each for:
  - PSM-level `SpectralMatch` alignment
  - peptide-level `SpectralMatch` alignment
  - `ProteinGroup` alignment
- Avoid mode switches and nullable branching in the apply path by splitting service responsibilities.
- Preserve current score-clamp semantics.

### Goals
- Move baseline FDR cache and score-alignment logic out of `ParallelSearchTask`.
- Make alignment behavior deterministic and easy to unit test.
- Preserve current alignment semantics (including clamp behavior and peptide-mode fallback).

### Step 1: Contracts and immutable cache models
1. Keep a generic build/apply alignment contract and shared `AlignmentStats`.
2. Add a shared score-based base class for derived aligners.
3. Add strongly-typed baseline entry models:
   - `PsmBaselineFdrEntry`
   - `PeptideBaselineFdrEntry`
   - `ProteinGroupBaselineFdrEntry`
4. Use `FdrInfo.Clone()` directly for FDR snapshot copies.

### Step 2: Implement three derived aligners
1. `PsmSpectralMatchFdrAlignmentService`
   - baseline built from `SpectralMatch.PsmFdrInfo`
   - apply writes cloned PSM FDR info
2. `PeptideSpectralMatchFdrAlignmentService`
   - baseline built only from matches with computed peptide FDR
   - apply writes cloned peptide FDR info
3. `ProteinGroupFdrAlignmentService`
   - baseline built from `ProteinGroup` score/FDR-related values
   - apply copies aligned values back to `ProteinGroup`

### Next steps (not part of this iteration)
1. Wire services into `ParallelSearchTask.Initialize` and `PerformPostSearchAnalysis`.
2. Remove old in-task baseline lookup + apply methods once parity is validated.
3. Add unit tests per aligner and integration parity tests for transient outputs.

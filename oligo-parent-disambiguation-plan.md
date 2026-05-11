# Parent-Aware Ambiguity Disambiguation Plan

## Overview

Add a new ambiguity-resolution pass that prefers the hypothesis whose parent bio-polymer has no parent-level localized modifications when an ambiguous match contains the same unmodified child `FullSequence` from both modified-parent and unmodified-parent entries.

This is intentionally more specific than a generic "prefer unmodified over modified" rule:

- The comparison is keyed on child `FullSequence`, not `BaseSequence`
- The child match must itself be unmodified
- The deciding factor is the parent entry's localized modification state
- The rule should be written generically, even though the motivating case is modified and unmodified oligo parents

## Rule Definition

For a single ambiguous `SpectralMatch`, remove a hypothesis only when all of the following are true:

1. Multiple best-scoring hypotheses share the same child `FullSequence`
2. That shared child `FullSequence` is unmodified, meaning it is equal to the child's `BaseSequence`
3. Within that `FullSequence` group, at least one hypothesis has a parent with no localized modifications
4. Within that same `FullSequence` group, at least one hypothesis has a parent with localized modifications
5. In that case, keep the hypotheses whose parent has no localized modifications and remove the hypotheses whose parent has localized modifications

Do not disambiguate when:

- The competing hypotheses have different child `FullSequence` values
- The child `FullSequence` is modified
- All parents in the `FullSequence` group are modified
- All parents in the `FullSequence` group are unmodified

## Implementation Steps

### Step 1: Extend `DisambiguationEngineResults`

Status: Done

Note: Minimal verification uses the private disambiguation pass removal count directly in the regression test rather than the full `Run()` path, because the synthetic test object model is intentionally lightweight and does not exercise the full FDR rerun pipeline.

File:
`MetaMorpheus/EngineLayer/SpectrumMatch/DisambiguationEngine.cs`

Add a new result counter to track removals from the parent-aware pass.

Recommended property name:

```csharp
public int RemovedByParentModificationPreference { get; set; }
```

Update `ToString()` so this removal count is reported alongside the existing q-value-notch count.

### Step 2: Add a New Disambiguation Pass

Status: Done

File:
`MetaMorpheus/EngineLayer/SpectrumMatch/DisambiguationEngine.cs`

Add a new private method.

Recommended name:

```csharp
private int DisambiguateByUnmodifiedFullSequenceAndParentModificationState()
```

This method should:

1. Iterate over ambiguous PSMs
2. Materialize the best hypotheses into a list
3. Group hypotheses by child `SpecificBioPolymer.FullSequence`
4. Evaluate each group independently
5. Remove only the modified-parent hypotheses from qualifying groups
6. Return the number of removed hypotheses

### Step 3: Define the Group Qualification Logic

Status: Done

Inside the new method, for each `FullSequence` group:

1. Ignore groups with fewer than two hypotheses
2. Confirm the child sequence is unmodified for the group
3. Split the group by parent localized modification state
4. Only act if both parent categories are present

Suggested checks:

```csharp
var fullSequenceGroups = psm.BestMatchingBioPolymersWithSetMods
    .ToList()
    .GroupBy(h => h.SpecificBioPolymer.FullSequence);
```

Unmodified child check:

```csharp
h.SpecificBioPolymer.FullSequence == h.SpecificBioPolymer.BaseSequence
```

Parent modification-state check:

```csharp
h.SpecificBioPolymer.Parent.OneBasedPossibleLocalizedModifications.Any()
```

### Step 4: Remove Candidates Safely

Status: Done

Do not mutate the ambiguity collection while querying it.

Implementation pattern:

1. Build a `toRemove` list from a materialized snapshot
2. Remove those hypotheses in a separate loop

That is important because `psm.RemoveThisAmbiguousPeptide(...)` immediately calls `ResolveAllAmbiguities()` and mutates the live hypothesis collection.

Suggested shape:

```csharp
var toRemove = qualifyingGroup
    .Where(h => h.SpecificBioPolymer.Parent.OneBasedPossibleLocalizedModifications.Any())
    .ToList();

foreach (var hypothesis in toRemove)
{
    psm.RemoveThisAmbiguousPeptide(hypothesis);
    removed++;
}
```

### Step 5: Wire the New Pass into `RunSpecific()`

Status: Done

File:
`MetaMorpheus/EngineLayer/SpectrumMatch/DisambiguationEngine.cs`

Update `RunSpecific()` so it runs both disambiguation passes and reruns the existing resolution and FDR logic if either pass removed anything.

Recommended flow:

```csharp
int removedQValueNotch = DisambiguateByQValueNotch();
int removedByParentPreference = DisambiguateByUnmodifiedFullSequenceAndParentModificationState();

if (removedQValueNotch > 0 || removedByParentPreference > 0)
{
    foreach (var psm in _allSpectralMatches)
        psm.ResolveAllAmbiguities();

    FdrAnalysisEngine.DoFalseDiscoveryRateAnalysis(...);
}
```

Then populate both result counters in `DisambiguationEngineResults`.

### Step 6: Keep the Logic Generic

Status: Done

Do not hard-code `AnalyteType.Oligo` in the new pass.

The logic should remain generic and rely only on:

- child `FullSequence`
- child `BaseSequence`
- parent `OneBasedPossibleLocalizedModifications`

That keeps the implementation reusable anywhere the same ambiguity pattern appears.

## Test Plan

### Step 7: Add a Focused Positive Test

Status: Done

File:
`MetaMorpheus/Test/TestPsm.cs`

Create a synthetic ambiguous PSM in memory using the existing `TestPsm` style.

Recommended setup:

1. Create one unmodified parent RNA
2. Create one modified parent RNA using `oneBasedModifications`
3. Digest both to produce child `OligoWithSetMods` objects
4. Ensure the chosen child from both parents has the same unmodified `FullSequence`
5. Create an `OligoSpectralMatch` with one hypothesis
6. Add the second hypothesis with `AddOrReplace(...)` at equal score
7. Run `DisambiguationEngine`
8. Assert that only the unmodified-parent hypothesis remains

Core assertions:

```csharp
Assert.That(psm.BestMatchingBioPolymersWithSetMods.Count(), Is.EqualTo(1));
Assert.That(!psm.BestMatchingBioPolymersWithSetMods.First()
    .SpecificBioPolymer.Parent.OneBasedPossibleLocalizedModifications.Any());
```

### Step 8: Add Guardrail Tests

Add narrow tests for the following cases:

1. Same `BaseSequence`, different child `FullSequence`
   Expected: no removal

2. Same child `FullSequence`, but the child sequence is modified
   Expected: no removal

3. Same unmodified child `FullSequence`, but all parents are modified
   Expected: no removal

4. Same unmodified child `FullSequence`, but all parents are unmodified
   Expected: no removal

These tests protect the exact nuance of the rule and prevent it from drifting into a broader modification-preference heuristic.

### Step 9: Validate Result Accounting

Status: Done

For the minimal implementation, validate the removal count returned by the parent-aware disambiguation pass when the positive-case test removes a modified-parent hypothesis.

## Verification Steps

### Step 10: Run Targeted Tests First

Status: Done

Focused command run:

- `dotnet test "MetaMorpheus/MetaMorpheus.sln" --filter TestDisambiguationPrefersUnmodifiedParentForSharedUnmodifiedFullSequence`

Run the most focused tests covering this feature before broader validation.

Suggested scope:

- `TestPsm` tests related to ambiguity resolution
- any new RNA/oligo-specific ambiguity test added for this feature

### Step 11: Run Broader Relevant Tests

After the targeted tests pass, run a broader test slice that covers ambiguity handling and related post-search analysis.

Suggested follow-up areas:

- `TestPsm`
- ambiguity-related tests
- transcriptomics or RNA-related tests if the new coverage lives there

## Files Expected to Change

- `MetaMorpheus/EngineLayer/SpectrumMatch/DisambiguationEngine.cs`
- `MetaMorpheus/Test/TestPsm.cs`

## Notes

- `SpectralMatch.ResolveAllAmbiguities()` should not absorb this new rule. That method is general-purpose and currently only contains broad ambiguity collapsing behavior.
- `ProteinParsimonyEngine` is not the right place for this logic because it resolves parent parsimony, not parent-modification preference for an identical child sequence.
- `ClassicSearchEngine` should continue generating both modified and unmodified candidates. The ambiguity should be resolved after search, not prevented during candidate generation.

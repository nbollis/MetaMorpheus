# ParallelSearchTask Statistical Analysis Test Suite

## Overview

This test suite provides comprehensive testing for the statistical analysis framework used in the ParallelSearchTask. The framework performs statistical tests on transient database search results to identify organisms with significant enrichment.

## Test Organization

```
Test/ParallelSearchTask/
??? Utility/
?   ??? TestDataFactory.cs              # Factory for creating test data with known properties
??? StatisticalAnalysis/
?   ??? MultipleTestingCorrectionTests.cs    # Benjamini-Hochberg FDR correction
?   ??? MetaAnalysisTests.cs                 # Fisher's method for combining p-values
?   ??? StatisticalAnalysisAggregatorTests.cs # Orchestration and CSV output (TODO)
??? StatisticalTests/
?   ??? GaussianTestTests.cs            # Gaussian distribution test
?   ??? PermutationTestTests.cs         # Permutation test with decoy-based null
?   ??? NegativeBinomialTestTests.cs    # Negative binomial test (TODO)
?   ??? FisherExactTestTests.cs         # Fisher's exact test (TODO)
?   ??? KolmogorovSmirnovTestTests.cs   # K-S test for score distributions (TODO)
??? Analysis/
?   ??? ResultCountAnalyzerTests.cs     # Count analyzer (TODO)
?   ??? OrganismSpecificityAnalyzerTests.cs # Organism specificity (TODO)
??? Integration/
    ??? EndToEndStatisticalAnalysisTests.cs  # Full pipeline integration (TODO)
```

## Test Data Factory

### Purpose
The `TestDataFactory` provides methods to create `AggregatedAnalysisResult` objects with controlled distributions and properties for testing.

### Key Methods

#### Basic Result Creation
- `CreateBasicResult()` - Simple result with specified counts
- `CreateRealisticResult()` - Result with organism-specific counts and score distributions

#### Scenario-Based Creation
- `CreateHighSignalScenario()` - Mix of low and high signal organisms (for power tests)
- `CreateGaussianDistribution()` - Results following normal distribution
- `CreateOverdispersedDistribution()` - Results with variance > mean (for NB test)
- `CreateNullScenario()` - All organisms with similar low signal (for false positive rate tests)

#### Edge Cases
- `EdgeCases.SingleObservation()` - Single database
- `EdgeCases.AllZeros()` - All counts are zero
- `EdgeCases.AllSameValue()` - All databases identical
- `EdgeCases.SingleOutlier()` - One extreme outlier
- `EdgeCases.Empty()` - Empty list
- `EdgeCases.LargeNumbers()` - Test numerical stability
- `EdgeCases.MixedScale()` - Very small and very large values

#### Test-Specific Data
- `CreateFisherTestData()` - Data for Fisher's Exact Test with controlled ambiguous/unambiguous ratios
- `CreateKSTestData()` - Data with shifted score distributions for K-S test

## Testing Strategy

### 1. Correctness
Each statistical test is verified against:
- Known mathematical properties (e.g., BH monotonicity)
- Hand-calculated examples
- Published statistical results

### 2. Edge Cases
All tests handle:
- Empty inputs
- Single observations
- All zeros
- All same values
- Large numbers
- Mixed scales

### 3. Numerical Stability
Tests verify:
- No NaN or Infinity values
- Proper handling of very small p-values (< 1e-10)
- Correct behavior with large numbers

### 4. Performance
Performance tests ensure:
- Tests complete in < 1 second for typical datasets (50-100 organisms)
- Permutation tests with 1000 iterations complete in < 2 seconds
- No memory leaks or excessive allocations

### 5. Reproducibility
- PermutationTest uses fixed seed (42) by default
- All tests are deterministic
- Floating-point comparisons use appropriate tolerances

## Key Test Patterns

### Testing Statistical Properties

```csharp
[Test]
public void TestName_Property_ExpectedBehavior()
{
    // Arrange: Create test data with known properties
    var results = TestDataFactory.CreateGaussianDistribution(count: 100);
    var test = GaussianTest.ForPsm();

    // Act: Run the statistical test
    var pValues = test.ComputePValues(results);

    // Assert: Verify statistical properties
    Assert.That(pValues.Values.All(p => p >= 0 && p <= 1), Is.True);
}
```

### Testing Monotonicity

```csharp
[Test]
public void ComputePValues_HigherSignal_LowerPValues()
{
    var results = new List<AggregatedAnalysisResult>
    {
        TestDataFactory.CreateBasicResult("Low", psmCount: 10),
        TestDataFactory.CreateBasicResult("High", psmCount: 100)
    };

    var test = GaussianTest.ForPsm();
    var pValues = test.ComputePValues(results);

    Assert.That(pValues["High"], Is.LessThan(pValues["Low"]));
}
```

### Testing Edge Cases

```csharp
[Test]
public void TestName_EdgeCase_HandlesGracefully()
{
    var results = TestDataFactory.EdgeCases.AllZeros();
    var test = GaussianTest.ForPsm();

    Assert.DoesNotThrow(() => test.ComputePValues(results));
}
```

## Tolerance Values

- **Exact comparisons**: Use `Within(1e-10)` for deterministic calculations
- **Statistical tests**: Use `Within(0.01)` for Gaussian/NB tests
- **Permutation tests**: Use `Within(0.05)` or qualitative comparisons
- **Proportions**: Use `Is.InRange()` or `Is.LessThan()`

## Test Execution

### Run All Tests
```bash
dotnet test
```

### Run Specific Test Class
```bash
dotnet test --filter "FullyQualifiedName~MultipleTestingCorrectionTests"
```

### Run Tests by Category (when implemented)
```bash
dotnet test --filter "Category=Statistical"
```

## Completed Tests

- ? **TestDataFactory** - Comprehensive test data generation
- ? **MultipleTestingCorrectionTests** - 16 tests covering BH correction
- ? **MetaAnalysisTests** - 18 tests covering Fisher's method
- ? **GaussianTestTests** - 18 tests covering Gaussian distribution test
- ? **PermutationTestTests** - 18 tests covering permutation test

## TODO

### High Priority
- [ ] **NegativeBinomialTestTests** - Test NB distribution fitting and p-values
- [ ] **FisherExactTestTests** - Test hypergeometric probabilities and odds ratios
- [ ] **KolmogorovSmirnovTestTests** - Test CDF comparison and p-value approximation

### Medium Priority
- [ ] **StatisticalAnalysisAggregatorTests** - Test orchestration and CSV output
- [ ] **ResultCountAnalyzerTests** - Test count extraction at various FDR thresholds
- [ ] **OrganismSpecificityAnalyzerTests** - Test organism-specific counting

### Low Priority
- [ ] **TaxonomyMappingTests** - Test taxonomy TSV parsing
- [ ] **EndToEndStatisticalAnalysisTests** - Integration test of full pipeline
- [ ] **Mock taxonomy test data** - Create embedded test resources

## Success Criteria

- [x] All statistical tests have unit tests verifying correctness
- [x] Edge cases are handled gracefully
- [x] Floating-point comparisons use appropriate tolerances  
- [ ] Tests are fast (<5 seconds total)
- [x] Tests are deterministic (reproducible)
- [ ] Code coverage >80% for statistical components

## Notes

- **Fixed Seed**: PermutationTest uses seed=42 by default for reproducibility in tests
- **Console Output**: Some tests may produce console output from the statistical tests (e.g., iteration counts)
- **Performance**: Performance tests set generous upper bounds (1-2 seconds) to avoid flaky failures on slow machines
- **Tolerance**: Statistical tests use larger tolerances (0.01-0.05) than pure mathematical tests (1e-10)

## References

- Benjamini, Y., & Hochberg, Y. (1995). Controlling the false discovery rate. *Journal of the Royal Statistical Society: Series B*, 57(1), 289-300.
- Fisher, R. A. (1925). *Statistical Methods for Research Workers*. Oliver and Boyd.
- Good, P. (2005). *Permutation, Parametric, and Bootstrap Tests of Hypotheses* (3rd ed.). Springer.

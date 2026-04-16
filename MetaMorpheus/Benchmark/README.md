# MetaMorpheus Protein Parsimony & Scoring Benchmarks

This project contains performance benchmarks for the protein parsimony and scoring engines in MetaMorpheus.

## Overview

The benchmarks measure:
- **Protein Parsimony Engine** performance across different dataset sizes
- **Protein Scoring and FDR Engine** performance in isolation and as part of the full pipeline
- Memory allocations and GC pressure
- Performance characteristics with different configuration options

## Project Structure

```
Benchmark/
??? Program.cs                          # Entry point for running benchmarks
??? BenchmarkDataGenerator.cs           # Generates realistic test data
??? ProteinParsimonyBenchmarks.cs      # Parsimony and full pipeline benchmarks
??? ProteinScoringBenchmarks.cs        # Scoring-specific benchmarks
??? README.md                           # This file
```

## Dataset Sizes

### Small Dataset
- **Proteins**: 50 (33 targets, 17 decoys)
- **Peptides**: ~400
- **PSMs**: ~800
- **Purpose**: Quick feedback, regression detection

### Medium Dataset
- **Proteins**: 500 (333 targets, 167 decoys)
- **Peptides**: ~5,000
- **PSMs**: ~15,000
- **Purpose**: Realistic workload simulation

### Large Dataset
- **Proteins**: 2,000 (1,333 targets, 667 decoys)
- **Peptides**: ~24,000
- **PSMs**: ~96,000
- **Purpose**: Stress testing, scalability assessment

## Running Benchmarks

### Run All Benchmarks
```bash
cd Benchmark
dotnet run -c Release
```

### Run Specific Benchmark Class
```bash
dotnet run -c Release -- --filter *ProteinParsimonyBenchmarks*
```

### Run Specific Benchmark Method
```bash
dotnet run -c Release -- --filter *Parsimony_Medium*
```

### Run with Job Configuration
```bash
dotnet run -c Release -- --job short  # Quick run
dotnet run -c Release -- --job long   # More iterations for accuracy
```

## Understanding Results

### Key Metrics

| Metric | Description |
|--------|-------------|
| **Mean** | Average execution time across all iterations |
| **Error** | Half of the 99.9% confidence interval |
| **StdDev** | Standard deviation of all measurements |
| **Ratio** | Performance relative to baseline (if specified) |
| **Gen0/Gen1/Gen2** | Garbage collections per 1000 operations |
| **Allocated** | Total memory allocated |

### Example Output

```
|                Method |     Mean |   Error |  StdDev | Ratio | Gen0 | Gen1 | Allocated |
|---------------------- |---------:--------:--------:|------:|-----:|-----:|----------:|
| Parsimony_Medium      | 45.2 ms  | 0.89 ms | 0.83 ms |  1.00 |  500 |   50 |   2.5 MB  |
| Scoring_Medium        | 12.3 ms  | 0.24 ms | 0.23 ms |  0.27 |  200 |   20 |   1.1 MB  |
| FullPipeline_Medium   | 58.1 ms  | 1.14 ms | 1.07 ms |  1.29 |  700 |   70 |   3.6 MB  |
```

### Interpreting Results

- **Lower Mean** = Faster execution
- **Lower StdDev** = More consistent performance
- **Lower Gen0/Gen1/Gen2** = Less GC pressure
- **Lower Allocated** = More memory efficient
- **Ratio < 1.00** = Faster than baseline

## Benchmark Scenarios

### ProteinParsimonyBenchmarks

1. **Parsimony_Small/Medium/Large**: Full parsimony across dataset sizes
2. **Parsimony_Medium_ModsAsDifferent**: Treats modified peptides as unique
3. **Parsimony_Medium_ModsAsSame**: Treats modified peptides as identical
4. **Scoring_Small/Medium/Large**: Scoring after parsimony
5. **FullPipeline_Medium/Large**: Complete workflow (parsimony + scoring)

### ProteinScoringBenchmarks

1. **ScoringOnly_Small/Medium/Large**: Isolated scoring performance
2. **Scoring_NoMerging**: Scoring without merging indistinguishable groups
3. **Scoring_WithMerging**: Scoring with merging enabled
4. **Scoring_NoOneHitWonders**: Filtering proteins with single peptide

## Optimization Strategy

### Before Optimizing

1. Run baseline benchmarks:
   ```bash
   dotnet run -c Release > baseline_results.txt
   ```

2. Identify hotspots using the baseline data

3. Document baseline metrics for comparison

### After Optimizing

1. Run benchmarks again:
   ```bash
   dotnet run -c Release > optimized_results.txt
   ```

2. Compare results:
   - Mean execution time improvement
   - Memory allocation reduction
   - GC pressure decrease

3. Verify correctness with unit tests:
   ```bash
   cd ../Test
   dotnet test --filter "FullyQualifiedName~Parsimony"
   ```

## Key Areas to Optimize

Based on code analysis, focus optimization efforts on:

### Protein Parsimony Engine

1. **Stage 0**: Mod-agnostic peptide-protein associations
   - Parallel operations overhead
   - Dictionary allocations

2. **Stage 2**: Building peptide-protein matching
   - HashSet operations
   - Repeated dictionary lookups

3. **Stage 3**: Greedy algorithm
   - LINQ queries in hot path
   - List/HashSet operations

4. **Stage 4**: Indistinguishable proteins
   - Parallel processing efficiency
   - SetEquals operations

### Protein Scoring Engine

1. **ScoreProteinGroups**:
   - Dictionary building (peptideToPsmMatching)
   - LINQ Select/Where operations
   - HashSet UnionWith operations

2. **DoProteinFdr**:
   - Multiple sorting operations
   - Dictionary operations
   - LINQ Except/Where/GroupBy

3. **Merging Indistinguishable Groups**:
   - Nested loops
   - HashSet comparisons
   - String concatenations

## Advanced Usage

### Custom Data Generation

Modify `BenchmarkDataGenerator.cs` to create specific scenarios:

```csharp
var customData = BenchmarkDataGenerator.GenerateData(
    proteinCount: 1000,
    avgPeptidesPerProtein: 15,
    avgPsmsPerPeptide: 5,
    includeDecoys: true,
    includeModifications: true
);
```

### Adding New Benchmarks

```csharp
[Benchmark(Description = "My Custom Benchmark")]
public void MyCustomBenchmark()
{
    // Your benchmark code here
}
```

### Profiling Memory

For detailed memory profiling, use:

```bash
dotnet run -c Release -- --filter *Parsimony_Medium* --profiler ETW
```

## Continuous Integration

To run benchmarks in CI:

```bash
dotnet run -c Release --exporters json --filter *Medium*
```

This generates JSON results that can be tracked over time.

## Troubleshooting

### OutOfMemoryException

If you encounter memory issues with large datasets:

1. Reduce dataset size in `Setup()`
2. Run benchmarks individually
3. Increase system memory or reduce other processes

### Inconsistent Results

If results vary significantly:

1. Close other applications
2. Disable background processes
3. Use `--job long` for more iterations
4. Check for thermal throttling

### Build Errors

Ensure all dependencies are restored:

```bash
dotnet restore
dotnet build -c Release
```

## References

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [Performance Best Practices](https://docs.microsoft.com/en-us/dotnet/core/testing/benchmark-dotnet)
- MetaMorpheus Parsimony Algorithm: Based on Zhang et al., Anal Chem. 2003

## Contributing

When adding benchmarks:

1. Follow existing naming conventions
2. Add meaningful descriptions
3. Update this README with new scenarios
4. Verify benchmarks work with `dotnet run -c Release --list`

## License

Same as MetaMorpheus project license.

# Quick Start Guide - MetaMorpheus Benchmarks

## ?? Getting Started in 5 Minutes

### Step 1: Build the Project

```bash
cd Benchmark
dotnet build -c Release
```

### Step 2: Run Your First Benchmark

**Option A: Run all benchmarks (takes ~5-10 minutes)**
```bash
dotnet run -c Release
```

**Option B: Quick test (takes ~30 seconds)**
```bash
# Windows
.\run_benchmarks.ps1 quick

# Linux/Mac
./run_benchmarks.sh quick
```

### Step 3: View Results

Results will be displayed in the console with a summary table:

```
|                Method |     Mean |   Error |  StdDev | Ratio |   Gen0 |  Gen1 | Allocated |
|---------------------- |---------:|--------:|--------:|------:|-------:|------:|----------:|
| Parsimony_Small       |  2.34 ms | 0.04 ms | 0.04 ms |  1.00 |  50.00 |  5.00 |    256 KB |
| Scoring_Small         |  0.78 ms | 0.01 ms | 0.01 ms |  0.33 |  18.00 |  2.00 |     96 KB |
```

## ?? Common Benchmark Scenarios

### Establish a Baseline

Before making any optimizations:

```bash
# Windows
.\run_benchmarks.ps1 baseline

# Linux/Mac
./run_benchmarks.sh baseline
```

This saves baseline metrics to `results/baseline_TIMESTAMP.txt`

### Test Your Optimizations

After making changes:

```bash
# Windows
.\run_benchmarks.ps1 compare

# Linux/Mac
./run_benchmarks.sh compare
```

### Run Specific Benchmarks

**Test only parsimony performance:**
```bash
dotnet run -c Release -- --filter *Parsimony*
```

**Test only scoring performance:**
```bash
dotnet run -c Release -- --filter *Scoring*
```

**Test only FDR analysis performance:**
```bash
dotnet run -c Release -- --filter *Fdr*
```

**Test medium dataset only:**
```bash
dotnet run -c Release -- --filter *Medium*
```

## ?? Interpreting Results

### What to Look For

? **Good Signs:**
- Lower `Mean` time
- Lower `StdDev` (more consistent)
- Lower `Allocated` memory
- Lower `Gen0/Gen1/Gen2` collections
- `Ratio < 1.00` (faster than baseline)

? **Red Flags:**
- `Ratio > 1.00` (slower than baseline)
- High `Gen2` collections (major GC events)
- Large memory allocations
- High standard deviation

### Example Comparison

**Before Optimization:**
```
| Parsimony_Medium |  45.2 ms | 0.89 ms | 0.83 ms | 1.00 | 500 | 50 | 2.5 MB |
```

**After Optimization:**
```
| Parsimony_Medium |  28.1 ms | 0.56 ms | 0.52 ms | 0.62 | 300 | 20 | 1.8 MB |
```

**Improvements:**
- 38% faster (ratio 0.62)
- 40% fewer Gen0 collections
- 60% fewer Gen1 collections
- 28% less memory allocated

## ?? Troubleshooting

### Build Errors

```bash
# Restore NuGet packages
dotnet restore

# Clean and rebuild
dotnet clean
dotnet build -c Release
```

### Inconsistent Results

- Close other applications
- Run benchmarks multiple times
- Use `--job long` for more accurate results:
  ```bash
  dotnet run -c Release -- --job long
  ```

### Out of Memory

Reduce dataset size in `ProteinParsimonyBenchmarks.cs`:

```csharp
_largeData = BenchmarkDataGenerator.GenerateData(
    proteinCount: 1000,  // Reduced from 2000
    avgPeptidesPerProtein: 10,  // Reduced from 12
    avgPsmsPerPeptide: 3,  // Reduced from 4
    includeDecoys: true,
    includeModifications: true
);
```

## ?? Optimization Workflow

1. **Establish Baseline**
   ```bash
   .\run_benchmarks.ps1 baseline
   ```

2. **Make Code Changes**
   - Focus on one optimization at a time
   - Document what you changed

3. **Run Benchmarks**
   ```bash
   .\run_benchmarks.ps1 compare
   ```

4. **Verify Correctness**
   ```bash
   cd ../Test
   dotnet test --filter "FullyQualifiedName~Parsimony"
   ```

5. **Commit if Improved**
   - If performance improved AND tests pass
   - Document speedup in commit message

## ?? Advanced Usage

### Profile Memory Usage

```bash
dotnet run -c Release -- --filter *Medium* --profiler ETW
```

### Export Results

```bash
# JSON format
dotnet run -c Release -- --exporters json

# Markdown format
dotnet run -c Release -- --exporters markdown

# HTML format
dotnet run -c Release -- --exporters html
```

### List All Benchmarks

```bash
dotnet run -c Release -- --list flat
```

### Dry Run (validate without running)

```bash
dotnet run -c Release -- --list tree
```

## ?? Next Steps

1. **Read the full README.md** for detailed information
2. **Review BenchmarkDataGenerator.cs** to understand test data
3. **Check optimization opportunities** in the main README
4. **Start optimizing!** Focus on the hotspots identified

## ?? Need Help?

- Check the main [README.md](README.md) for detailed documentation
- Review [BenchmarkDotNet docs](https://benchmarkdotnet.org/)
- Look at existing test patterns in `../Test/StefanParsimonyTest.cs`

## ? Quick Reference

| Command | Purpose |
|---------|---------|
| `.\run_benchmarks.ps1 all` | Run everything |
| `.\run_benchmarks.ps1 quick` | Fast test (small datasets) |
| `.\run_benchmarks.ps1 baseline` | Save baseline |
| `.\run_benchmarks.ps1 compare` | Compare with baseline |
| `.\run_benchmarks.ps1 list` | List all benchmarks |
| `dotnet run -c Release` | Manual run |

---

**Ready to optimize? Run your first benchmark now!** ??

```bash
dotnet run -c Release -- --filter *Small*
```

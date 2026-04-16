# Quick Start Guide - MetaMorpheus Benchmarks

## ?? Getting Started in 2 Steps

### Step 1: Build the Project

```bash
cd Benchmark
dotnet build -c Release
```

### Step 2: Run Benchmarks

**Run all benchmarks:**
```bash
dotnet run -c Release
```

**Run specific benchmarks:**
```bash
# FDR analysis only (fastest)
dotnet run -c Release -- --filter *FdrAnalysisBenchmarks*

# Parsimony only
dotnet run -c Release -- --filter *ProteinParsimonyBenchmarks*

# Scoring only
dotnet run -c Release -- --filter *ProteinScoringBenchmarks*

# Small datasets only (quick test)
dotnet run -c Release -- --filter *Small*
```

---

## ?? Comparing Before/After Optimizations

### Option 1: PowerShell Script (If It Works)

```powershell
# Try this first - if you get "No command provided" error, use Option 2 below
powershell -ExecutionPolicy Bypass -File run_benchmarks.ps1 baseline

# After making optimizations:
powershell -ExecutionPolicy Bypass -File run_benchmarks.ps1 compare
```

### Option 2: Manual Commands (Always Works)

**Save baseline (before optimization):**
```powershell
# Run benchmarks with JSON export
dotnet run -c Release -- --exporters json markdown html

# Save the JSON baseline
$json = Get-ChildItem "BenchmarkDotNet.Artifacts\results\*-report-full*.json" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
New-Item -ItemType Directory -Force -Path "results" | Out-Null
Copy-Item $json.FullName "results\baseline_$timestamp.json"
Write-Host "Baseline saved to: results\baseline_$timestamp.json" -ForegroundColor Green
```

**Compare (after optimization):**
```powershell
# Run benchmarks again
dotnet run -c Release -- --exporters json markdown html

# Install comparison tool (one time only)
dotnet tool install -g BenchmarkDotNet.Tool

# Compare with baseline
$baseline = Get-ChildItem "results\baseline_*.json" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$current = Get-ChildItem "BenchmarkDotNet.Artifacts\results\*-report-full*.json" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host "`n==================== COMPARISON ====================" -ForegroundColor Cyan
dotnet benchmark compare $baseline.FullName $current.FullName --threshold 5%
Write-Host "====================================================`n" -ForegroundColor Cyan
```

---

## ?? Understanding Results

### Key Metrics

| Metric | What It Means |
|--------|---------------|
| **Mean** | Average execution time (lower is better) |
| **Allocated** | Memory used (lower is better) |
| **Gen0/Gen1/Gen2** | Garbage collections (lower is better) |
| **Ratio** | Performance vs baseline (< 1.00 is faster) |

### Example Output

```
|                Method |     Mean |  Allocated |
|---------------------- |---------:|-----------:|
| Parsimony_Medium      | 45.23 ms |   2.50 MB  |
| Scoring_Medium        | 12.34 ms |   1.12 MB  |
```

### Comparison Output

```
|              Method | Baseline |  Current |  Change |
|-------------------- |---------:|---------:|--------:|
| Parsimony_Medium    | 45.23 ms | 28.15 ms | -37.8%  | ? 38% FASTER!
| Scoring_Medium      | 12.34 ms | 13.01 ms |  +5.4%  | ?? 5% SLOWER
```

**Good**: Negative % = faster/less memory  
**Bad**: Positive % = slower/more memory

---

## ?? Optimization Workflow

```
1. Save baseline     ? dotnet run -c Release -- --exporters json
2. Make changes      ? Edit FdrAnalysisEngine.cs (or other files)
3. Run benchmarks    ? dotnet run -c Release -- --exporters json
4. Compare results   ? dotnet benchmark compare baseline.json current.json
5. Verify tests pass ? cd ../Test && dotnet test --filter FdrTest
```

---

## ?? Tips

- **Benchmarks take 5-10 minutes** - Be patient, they need multiple iterations for accuracy
- **Close other apps** - For consistent results
- **Focus on one optimization** - Easier to measure impact
- **Always verify with tests** - Don't break existing functionality

---

## ?? Troubleshooting

**"No JSON found":**
- Files are named `*-report-full-compressed.json`
- Use `*-report-full*.json` pattern to find them

**"PowerShell script doesn't work":**
- Use Option 2 (Manual Commands) - always works
- Or run: `Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser`

**"Benchmarks too slow":**
- Run only small datasets: `dotnet run -c Release -- --filter *Small*`
- Or specific engine: `dotnet run -c Release -- --filter *Fdr*`

---

## ?? More Information

See [README.md](README.md) for:
- Detailed benchmark descriptions
- Dataset sizes
- Optimization strategies
- Advanced usage

---

**Ready to benchmark? Start here:** ??

```bash
dotnet run -c Release -- --filter *Small*
```

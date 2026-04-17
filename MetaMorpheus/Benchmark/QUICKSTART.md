# Quick Start - MetaMorpheus Benchmarks

## Run and Compare

```powershell
# Save baseline
.\run_benchmarks.ps1 baseline

# Make your code changes...

# Compare
.\run_benchmarks.ps1 compare
```

---

## Named Runs

```powershell
.\run_benchmarks.ps1 baseline before
.\run_benchmarks.ps1 baseline after
.\compare_benchmarks.ps1 results\before.json results\after.json
```

---

## Output

```
Method                    Baseline    Current     Change
Parsimony_Medium            45 ms       28 ms     -38%  ?
```

**Negative % = Faster ?**  
**Positive % = Slower ??**

---

**Takes 15-20 minutes. Close other apps for consistent results.**

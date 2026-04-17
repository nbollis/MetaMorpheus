# MetaMorpheus Benchmark Runner
# Usage: 
#   .\run_benchmarks.ps1 baseline [name]  - Save baseline (optional: use custom name)
#   .\run_benchmarks.ps1 compare          - Compare with latest baseline

param(
    [Parameter(Position=0)]
    [string]$Command = "",
    
    [Parameter(Position=1)]
    [string]$Name = ""
)

$ErrorActionPreference = "Stop"

# Check directory
if (-not (Test-Path "Benchmark.csproj")) {
    Write-Host "[ERROR] Run this from the Benchmark directory" -ForegroundColor Red
    exit 1
}

switch ($Command.ToLower()) {
    "baseline" {
        Write-Host "[BENCHMARK] Running all benchmarks..." -ForegroundColor Green
        
        # Run benchmarks
        dotnet run -c Release -- --filter * --exporters json markdown html
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[ERROR] Benchmark failed" -ForegroundColor Red
            exit 1
        }
        
        # Save result
        Start-Sleep -Seconds 2
        $json = Get-ChildItem "BenchmarkDotNet.Artifacts\results\*-report-full*.json" -ErrorAction Stop | 
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        
        New-Item -ItemType Directory -Force -Path "results" | Out-Null
        
        if ($Name) {
            $outFile = "results\$Name.json"
        } else {
            $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
            $outFile = "results\baseline_$timestamp.json"
        }
        
        Copy-Item $json.FullName $outFile
        Write-Host "`n[SUCCESS] Saved to: $outFile" -ForegroundColor Green
    }
    
    "compare" {
        Write-Host "[BENCHMARK] Running comparison..." -ForegroundColor Green
        
        # Find baseline
        $baseline = Get-ChildItem "results\baseline_*.json" -ErrorAction SilentlyContinue | 
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        
        if (-not $baseline) {
            Write-Host "[ERROR] No baseline found. Run: .\run_benchmarks.ps1 baseline" -ForegroundColor Red
            exit 1
        }
        
        Write-Host "[INFO] Using baseline: $($baseline.Name)" -ForegroundColor Cyan
        
        # Run benchmarks
        dotnet run -c Release -- --filter * --exporters json markdown html
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[ERROR] Benchmark failed" -ForegroundColor Red
            exit 1
        }
        
        # Save and compare
        Start-Sleep -Seconds 2
        $json = Get-ChildItem "BenchmarkDotNet.Artifacts\results\*-report-full*.json" -ErrorAction Stop | 
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        
        $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
        $currentFile = "results\current_$timestamp.json"
        Copy-Item $json.FullName $currentFile
        
        # Compare
        & ".\compare_benchmarks.ps1" $baseline.FullName $currentFile
    }
    
    default {
        Write-Host @"
MetaMorpheus Benchmark Runner

Usage:
  .\run_benchmarks.ps1 baseline [name]    Save baseline (optionally named)
  .\run_benchmarks.ps1 compare            Compare with latest baseline

Examples:
  .\run_benchmarks.ps1 baseline
  .\run_benchmarks.ps1 baseline before_fix
  .\run_benchmarks.ps1 compare

"@ -ForegroundColor White
        exit 1
    }
}

Write-Host "[DONE]" -ForegroundColor Green

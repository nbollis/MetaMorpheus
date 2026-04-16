# MetaMorpheus Benchmark Runner Script
# Usage: .\run_benchmarks.ps1 [command]
# Commands: baseline, compare, fdr, parsimony, scoring, all, help

param(
    [Parameter(Position=0)]
    [string]$Command = ""
)

$ErrorActionPreference = "Stop"

# Check if running from correct directory
if (-not (Test-Path "Benchmark.csproj")) {
    Write-Host "[ERROR] Please run this script from the Benchmark directory" -ForegroundColor Red
    exit 1
}

# Display help
function Show-Help {
    Write-Host @"
MetaMorpheus Benchmark Runner

Usage: .\run_benchmarks.ps1 [COMMAND]

Commands:
    baseline            Save baseline benchmarks (JSON export)
    compare             Compare with saved baseline
    fdr                 Run FDR analysis benchmarks
    parsimony           Run protein parsimony benchmarks
    scoring             Run protein scoring benchmarks
    all                 Run all benchmarks
    quick               Run quick benchmarks (small datasets)
    list                List all available benchmarks
    help                Show this help message

Examples:
    .\run_benchmarks.ps1 baseline
    .\run_benchmarks.ps1 compare
    .\run_benchmarks.ps1 fdr

"@ -ForegroundColor White
}

# Main command handler
switch ($Command.ToLower()) {
    "baseline" {
        Write-Host "[BENCHMARK] Running baseline and saving to JSON..." -ForegroundColor Green
        
        # Create results directory
        New-Item -ItemType Directory -Force -Path "results" | Out-Null
        
        # Run benchmarks with JSON export
        Write-Host "[BENCHMARK] Running benchmarks (this may take several minutes)..." -ForegroundColor Green
        dotnet run -c Release -- --exporters json markdown html
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[ERROR] Benchmark run failed" -ForegroundColor Red
            exit 1
        }
        
        # Wait for files
        Start-Sleep -Seconds 2
        
        # Find and save JSON
        $jsonFile = Get-ChildItem -Path "BenchmarkDotNet.Artifacts\results\*-report-full*.json" -ErrorAction SilentlyContinue | 
            Sort-Object LastWriteTime -Descending | 
            Select-Object -First 1
        
        if ($jsonFile) {
            $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
            $baselineFile = "results\baseline_$timestamp.json"
            Copy-Item $jsonFile.FullName $baselineFile
            Write-Host "`n[SUCCESS] Baseline saved to: $baselineFile" -ForegroundColor Green
            Write-Host "[INFO] To compare later: .\run_benchmarks.ps1 compare`n" -ForegroundColor Cyan
        } else {
            Write-Host "[ERROR] Could not find JSON output" -ForegroundColor Red
            exit 1
        }
    }
    
    "compare" {
        Write-Host "[BENCHMARK] Running comparison..." -ForegroundColor Green
        
        # Check for baseline
        if (-not (Test-Path "results\baseline_*.json")) {
            Write-Host "[ERROR] No baseline found!" -ForegroundColor Red
            Write-Host "[INFO] Run: .\run_benchmarks.ps1 baseline" -ForegroundColor Yellow
            exit 1
        }
        
        # Find baseline
        $baseline = Get-ChildItem -Path "results\baseline_*.json" | 
            Sort-Object LastWriteTime -Descending | 
            Select-Object -First 1
        Write-Host "[INFO] Using baseline: $($baseline.Name)" -ForegroundColor Cyan
        
        # Run benchmarks
        Write-Host "[BENCHMARK] Running benchmarks (this may take several minutes)..." -ForegroundColor Green
        dotnet run -c Release -- --exporters json markdown html
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[ERROR] Benchmark run failed" -ForegroundColor Red
            exit 1
        }
        
        # Wait for files
        Start-Sleep -Seconds 2
        
        # Find current JSON
        $current = Get-ChildItem -Path "BenchmarkDotNet.Artifacts\results\*-report-full*.json" -ErrorAction SilentlyContinue | 
            Sort-Object LastWriteTime -Descending | 
            Select-Object -First 1
        
        if (-not $current) {
            Write-Host "[ERROR] Could not find current results" -ForegroundColor Red
            exit 1
        }
        
        # Save current
        $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
        $currentFile = "results\current_$timestamp.json"
        Copy-Item $current.FullName $currentFile
        
        # Install comparison tool if needed
        $toolCheck = dotnet tool list -g | Select-String "benchmarkdotnet.tool"
        if (-not $toolCheck) {
            Write-Host "[INFO] Installing BenchmarkDotNet.Tool..." -ForegroundColor Yellow
            dotnet tool install -g BenchmarkDotNet.Tool 2>&1 | Out-Null
        }
        
        # Compare
        Write-Host "`n==================== COMPARISON ====================" -ForegroundColor Cyan
        dotnet benchmark compare $baseline.FullName $currentFile --threshold 5%
        Write-Host "====================================================`n" -ForegroundColor Cyan
        
        Write-Host "[SUCCESS] Comparison complete!" -ForegroundColor Green
        Write-Host "[INFO] Baseline: $($baseline.Name)" -ForegroundColor Cyan
        Write-Host "[INFO] Current:  $(Split-Path $currentFile -Leaf)`n" -ForegroundColor Cyan
    }
    
    "fdr" {
        Write-Host "[BENCHMARK] Running FDR analysis benchmarks..." -ForegroundColor Green
        dotnet run -c Release -- --filter "*FdrAnalysisBenchmarks*"
    }
    
    "parsimony" {
        Write-Host "[BENCHMARK] Running parsimony benchmarks..." -ForegroundColor Green
        dotnet run -c Release -- --filter "*ProteinParsimonyBenchmarks*"
    }
    
    "scoring" {
        Write-Host "[BENCHMARK] Running scoring benchmarks..." -ForegroundColor Green
        dotnet run -c Release -- --filter "*ProteinScoringBenchmarks*"
    }
    
    "all" {
        Write-Host "[BENCHMARK] Running all benchmarks..." -ForegroundColor Green
        dotnet run -c Release
    }
    
    "quick" {
        Write-Host "[BENCHMARK] Running quick benchmarks (small datasets)..." -ForegroundColor Green
        dotnet run -c Release -- --filter "*Small*"
    }
    
    "list" {
        Write-Host "[BENCHMARK] Available benchmarks:" -ForegroundColor Green
        dotnet run -c Release -- --list flat
    }
    
    "help" {
        Show-Help
    }
    
    default {
        if ([string]::IsNullOrWhiteSpace($Command)) {
            Write-Host "[ERROR] No command provided`n" -ForegroundColor Red
            Show-Help
        } else {
            Write-Host "[ERROR] Unknown command: $Command`n" -ForegroundColor Red
            Show-Help
        }
        exit 1
    }
}

Write-Host "[BENCHMARK] Done!" -ForegroundColor Green

# MetaMorpheus Benchmark Runner Script (PowerShell)
# Provides convenient commands for running common benchmark scenarios

$ErrorActionPreference = "Stop"

# Colors for output
function Write-Info {
    param([string]$Message)
    Write-Host "[BENCHMARK] $Message" -ForegroundColor Green
}

function Write-Error-Custom {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Write-Warning-Custom {
    param([string]$Message)
    Write-Host "[WARNING] $Message" -ForegroundColor Yellow
}

# Check if running from correct directory
if (-not (Test-Path "Benchmark.csproj")) {
    Write-Error-Custom "Please run this script from the Benchmark directory"
    exit 1
}

# Function to run benchmarks
function Run-Benchmark {
    param(
        [string]$Filter,
        [string]$OutputFile
    )
    
    Write-Info "Running benchmark: $Filter"
    
    if ($OutputFile) {
        dotnet run -c Release -- --filter $Filter | Tee-Object -FilePath $OutputFile
    } else {
        dotnet run -c Release -- --filter $Filter
    }
}

# Display help
function Show-Help {
    @"
MetaMorpheus Benchmark Runner

Usage: .\run_benchmarks.ps1 [OPTION]

Options:
    all                 Run all benchmarks
    parsimony           Run protein parsimony benchmarks
    scoring             Run protein scoring benchmarks
    baseline            Run baseline benchmarks and save to file
    small               Run small dataset benchmarks only
    medium              Run medium dataset benchmarks only
    large               Run large dataset benchmarks only
    quick               Run quick benchmarks (small datasets)
    compare             Compare baseline vs current results
    list                List all available benchmarks
    help                Show this help message

Examples:
    .\run_benchmarks.ps1 all                 # Run everything
    .\run_benchmarks.ps1 parsimony           # Only parsimony benchmarks
    .\run_benchmarks.ps1 baseline            # Save baseline for comparison
    .\run_benchmarks.ps1 small               # Quick test run

"@
}

# Main command handler
$command = $args[0]

switch ($command) {
    "all" {
        Write-Info "Running all benchmarks..."
        dotnet run -c Release
    }
    
    "parsimony" {
        Write-Info "Running protein parsimony benchmarks..."
        Run-Benchmark "*ProteinParsimonyBenchmarks*"
    }
    
    "scoring" {
        Write-Info "Running protein scoring benchmarks..."
        Run-Benchmark "*ProteinScoringBenchmarks*"
    }
    
    "baseline" {
        Write-Info "Running baseline benchmarks and saving results..."
        New-Item -ItemType Directory -Force -Path "results" | Out-Null
        $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
        $outputFile = "results\baseline_$timestamp.txt"
        Run-Benchmark "*Medium*" $outputFile
        Write-Info "Baseline saved to $outputFile"
    }
    
    "small" {
        Write-Info "Running small dataset benchmarks..."
        Run-Benchmark "*Small*"
    }
    
    "medium" {
        Write-Info "Running medium dataset benchmarks..."
        Run-Benchmark "*Medium*"
    }
    
    "large" {
        Write-Info "Running large dataset benchmarks..."
        Run-Benchmark "*Large*"
    }
    
    "quick" {
        Write-Info "Running quick benchmarks (small datasets only)..."
        Run-Benchmark "*Small*"
    }
    
    "compare" {
        Write-Info "Running comparison benchmarks..."
        if (-not (Test-Path "results") -or 
            -not (Get-ChildItem -Path "results\baseline_*.txt" -ErrorAction SilentlyContinue)) {
            Write-Warning-Custom "No baseline found. Run '.\run_benchmarks.ps1 baseline' first."
            exit 1
        }
        $latestBaseline = Get-ChildItem -Path "results\baseline_*.txt" | 
            Sort-Object LastWriteTime -Descending | 
            Select-Object -First 1
        Write-Info "Comparing against: $($latestBaseline.Name)"
        $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
        $outputFile = "results\current_$timestamp.txt"
        Run-Benchmark "*Medium*" $outputFile
        Write-Info "Results saved to $outputFile"
        Write-Info "Compare files manually or use a diff tool"
    }
    
    "list" {
        Write-Info "Available benchmarks:"
        dotnet run -c Release -- --list flat
    }
    
    "help" {
        Show-Help
    }
    
    default {
        if ([string]::IsNullOrEmpty($command)) {
            Write-Error-Custom "No option provided"
        } else {
            Write-Error-Custom "Unknown option: $command"
        }
        Write-Host ""
        Show-Help
        exit 1
    }
}

Write-Info "Done!"

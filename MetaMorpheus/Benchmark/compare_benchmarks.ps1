# Compare two benchmark JSON files
# Usage: .\compare_benchmarks.ps1 baseline.json current.json

param(
    [Parameter(Mandatory=$false)]
    [string]$BaselineFile,
    
    [Parameter(Mandatory=$false)]
    [string]$CurrentFile
)

# If no files specified, find latest baseline and current
if (-not $BaselineFile) {
    $BaselineFile = Get-ChildItem "results\baseline_*.json" -ErrorAction SilentlyContinue | 
        Sort-Object LastWriteTime -Descending | 
        Select-Object -First 1 -ExpandProperty FullName
    
    if (-not $BaselineFile) {
        Write-Host "[ERROR] No baseline file found in results\" -ForegroundColor Red
        Write-Host "Run benchmarks with: dotnet run -c Release -- --exporters json" -ForegroundColor Yellow
        exit 1
    }
    Write-Host "[INFO] Using baseline: $BaselineFile" -ForegroundColor Cyan
}

if (-not $CurrentFile) {
    $CurrentFile = Get-ChildItem "results\current_*.json" -ErrorAction SilentlyContinue | 
        Sort-Object LastWriteTime -Descending | 
        Select-Object -First 1 -ExpandProperty FullName
    
    if (-not $CurrentFile) {
        Write-Host "[ERROR] No current file found in results\" -ForegroundColor Red
        Write-Host "Run benchmarks with: dotnet run -c Release -- --exporters json" -ForegroundColor Yellow
        exit 1
    }
    Write-Host "[INFO] Using current: $CurrentFile" -ForegroundColor Cyan
}

# Load JSON files
try {
    $baseline = Get-Content $BaselineFile | ConvertFrom-Json
    $current = Get-Content $CurrentFile | ConvertFrom-Json
}
catch {
    Write-Host "[ERROR] Failed to load JSON files: $_" -ForegroundColor Red
    exit 1
}

# Display comparison
Write-Host "`n==================== BENCHMARK COMPARISON ====================" -ForegroundColor Cyan
Write-Host ("{0,-40} {1,12} {2,12} {3,10} {4}" -f "Method", "Baseline", "Current", "Change", "") -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Cyan

$improvements = 0
$regressions = 0
$unchanged = 0
$newBenchmarks = 0

foreach ($curr in $current.Benchmarks) {
    $base = $baseline.Benchmarks | Where-Object { $_.FullName -eq $curr.FullName }
    
    if ($base) {
        # Convert nanoseconds to milliseconds
        $bMean = [math]::Round($base.Statistics.Mean / 1000000, 2)
        $cMean = [math]::Round($curr.Statistics.Mean / 1000000, 2)
        $change = [math]::Round((($cMean - $bMean) / $bMean) * 100, 1)
        
        # Determine status
        if ($change -lt -2) {
            $arrow = "?"
            $color = "Green"
            $improvements++
        }
        elseif ($change -gt 2) {
            $arrow = "??"
            $color = "Red"
            $regressions++
        }
        else {
            $arrow = "?"
            $color = "Yellow"
            $unchanged++
        }
        
        $changeStr = if ($change -gt 0) { "+$change%" } else { "$change%" }
        Write-Host ("{0,-40} {1,10:N2} ms {2,10:N2} ms {3,10} {4}" -f $curr.MethodTitle, $bMean, $cMean, $changeStr, $arrow) -ForegroundColor $color
    }
    else {
        # New benchmark not in baseline
        $cMean = [math]::Round($curr.Statistics.Mean / 1000000, 2)
        Write-Host ("{0,-40} {1,12} {2,10:N2} ms {3,10} {4}" -f $curr.MethodTitle, "N/A", $cMean, "NEW", "??") -ForegroundColor Cyan
        $newBenchmarks++
    }
}

Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "`nSummary:" -ForegroundColor White
Write-Host "  Improvements: $improvements" -ForegroundColor Green
Write-Host "  Regressions:  $regressions" -ForegroundColor Red
Write-Host "  Unchanged:    $unchanged" -ForegroundColor Yellow
if ($newBenchmarks -gt 0) {
    Write-Host "  New:          $newBenchmarks (not in baseline)" -ForegroundColor Cyan
}
Write-Host "==============================================================`n" -ForegroundColor Cyan

#!/bin/bash

# MetaMorpheus Benchmark Runner Script
# Provides convenient commands for running common benchmark scenarios

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Print colored message
print_message() {
    echo -e "${GREEN}[BENCHMARK]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

# Check if running from correct directory
if [ ! -f "Benchmark.csproj" ]; then
    print_error "Please run this script from the Benchmark directory"
    exit 1
fi

# Function to run benchmarks
run_benchmark() {
    local filter=$1
    local output=$2
    
    print_message "Running benchmark: $filter"
    
    if [ -n "$output" ]; then
        dotnet run -c Release -- --filter "$filter" | tee "$output"
    else
        dotnet run -c Release -- --filter "$filter"
    fi
}

# Display help
show_help() {
    cat << EOF
MetaMorpheus Benchmark Runner

Usage: ./run_benchmarks.sh [OPTION]

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
    ./run_benchmarks.sh all                 # Run everything
    ./run_benchmarks.sh parsimony           # Only parsimony benchmarks
    ./run_benchmarks.sh baseline            # Save baseline for comparison
    ./run_benchmarks.sh small               # Quick test run

EOF
}

# Main command handler
case "$1" in
    all)
        print_message "Running all benchmarks..."
        dotnet run -c Release
        ;;
    
    parsimony)
        print_message "Running protein parsimony benchmarks..."
        run_benchmark "*ProteinParsimonyBenchmarks*"
        ;;
    
    scoring)
        print_message "Running protein scoring benchmarks..."
        run_benchmark "*ProteinScoringBenchmarks*"
        ;;
    
    baseline)
        print_message "Running baseline benchmarks and saving results..."
        mkdir -p results
        TIMESTAMP=$(date +%Y%m%d_%H%M%S)
        run_benchmark "*Medium*" "results/baseline_${TIMESTAMP}.txt"
        print_message "Baseline saved to results/baseline_${TIMESTAMP}.txt"
        ;;
    
    small)
        print_message "Running small dataset benchmarks..."
        run_benchmark "*Small*"
        ;;
    
    medium)
        print_message "Running medium dataset benchmarks..."
        run_benchmark "*Medium*"
        ;;
    
    large)
        print_message "Running large dataset benchmarks..."
        run_benchmark "*Large*"
        ;;
    
    quick)
        print_message "Running quick benchmarks (small datasets only)..."
        run_benchmark "*Small*"
        ;;
    
    compare)
        print_message "Running comparison benchmarks..."
        if [ ! -d "results" ] || [ -z "$(ls -A results/baseline_*.txt 2>/dev/null)" ]; then
            print_warning "No baseline found. Run './run_benchmarks.sh baseline' first."
            exit 1
        fi
        LATEST_BASELINE=$(ls -t results/baseline_*.txt | head -1)
        print_message "Comparing against: $LATEST_BASELINE"
        TIMESTAMP=$(date +%Y%m%d_%H%M%S)
        run_benchmark "*Medium*" "results/current_${TIMESTAMP}.txt"
        print_message "Results saved to results/current_${TIMESTAMP}.txt"
        print_message "Compare files manually or use a diff tool"
        ;;
    
    list)
        print_message "Available benchmarks:"
        dotnet run -c Release -- --list flat
        ;;
    
    help|--help|-h)
        show_help
        ;;
    
    *)
        if [ -z "$1" ]; then
            print_error "No option provided"
        else
            print_error "Unknown option: $1"
        fi
        echo ""
        show_help
        exit 1
        ;;
esac

print_message "Done!"

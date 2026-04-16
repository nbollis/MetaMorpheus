using BenchmarkDotNet.Running;

namespace Benchmark;

class Program
{
    static void Main(string[] args)
    {
        // Run all benchmarks
        var summary = BenchmarkRunner.Run<ProteinParsimonyBenchmarks>();
        
        // Optionally run scoring benchmarks separately
        // var scoringSummary = BenchmarkRunner.Run<ProteinScoringBenchmarks>();
        
        // Or use BenchmarkSwitcher to select at runtime:
        // BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}

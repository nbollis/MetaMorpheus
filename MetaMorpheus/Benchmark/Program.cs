using BenchmarkDotNet.Running;

namespace Benchmark;

class Program
{
    static void Main(string[] args)
    {
        // Use BenchmarkSwitcher to select benchmarks at runtime
        // This allows you to choose which benchmark to run via command-line arguments
        // Example usage:
        //   dotnet run -c Release -- --filter *Parsimony*
        //   dotnet run -c Release -- --filter *Scoring*
        //   dotnet run -c Release -- --filter *Fdr*
        
        var switcher = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly);
        switcher.Run(args);
        
        // Alternatively, run specific benchmarks directly:
        // var parsimonyResults = BenchmarkRunner.Run<ProteinParsimonyBenchmarks>();
        // var scoringResults = BenchmarkRunner.Run<ProteinScoringBenchmarks>();
        // var fdrResults = BenchmarkRunner.Run<FdrAnalysisBenchmarks>();
    }
}

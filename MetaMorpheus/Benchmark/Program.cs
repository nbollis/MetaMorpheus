using BenchmarkDotNet.Running;

namespace Benchmark;

class Program
{
    static void Main(string[] args)
    {
        var switcher = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly);
        switcher.Run(args);
    }
}

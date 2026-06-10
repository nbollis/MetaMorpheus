using MzLibUtil;

namespace EngineLayer.ParallelSearch.Scoring
{
    /// <summary>
    /// Hands out an <see cref="ISpectralScorer"/> to each search partition (thread).
    /// Each thread gets its own lightweight scorer (per-thread match buffer, no shared state).
    /// </summary>
    public interface ISpectralScorerProvider : System.IDisposable
    {
        /// <summary>Scorer for the calling partition. Do NOT dispose the returned scorer; the provider owns it.</summary>
        ISpectralScorer GetScorer();
        string BackendDescription { get; }
    }

    /// <summary>CPU provider: a fresh (cheap) CpuSpectralScorer per partition.</summary>
    public sealed class CpuScorerProvider : ISpectralScorerProvider
    {
        private readonly SpectralScoringData _data;
        private readonly Tolerance _tolerance;
        public CpuScorerProvider(SpectralScoringData data, Tolerance tolerance) { _data = data; _tolerance = tolerance; }
        public ISpectralScorer GetScorer() => new CpuSpectralScorer(_data, _tolerance);
        public string BackendDescription => "CPU (binary search per fragment)";
        public void Dispose() { }
    }
}

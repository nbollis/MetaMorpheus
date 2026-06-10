using System;
using Chemistry;
using MzLibUtil;

namespace EngineLayer.ParallelSearch.Scoring
{
    /// <summary>
    /// CPU implementation of <see cref="ISpectralScorer"/> and the correctness baseline for the
    /// GPU path. Reproduces the original TransientClassicSearchEngine matching exactly: for each
    /// theoretical fragment, find the closest experimental envelope (binary search), accept it if
    /// it is within the product tolerance and its charge does not exceed the precursor charge.
    /// </summary>
    public sealed class CpuSpectralScorer : ISpectralScorer
    {
        private readonly SpectralScoringData _data;
        private readonly Tolerance _productTolerance;
        private FragmentMatch[] _matchBuffer = new FragmentMatch[64];

        public CpuSpectralScorer(SpectralScoringData data, Tolerance productTolerance)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _productTolerance = productTolerance ?? throw new ArgumentNullException(nameof(productTolerance));
        }

        public string BackendDescription => "CPU (binary search per fragment)";

        public void ScoreBatch(ScoringBatch batch, IScoringSink sink)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            if (sink == null) throw new ArgumentNullException(nameof(sink));

            for (int w = 0; w < batch.WorkItemCount; w++)
            {
                int slot = batch.WorkPeptideSlot[w];
                int scanIndex = batch.WorkScanIndex[w];

                int fragStart = batch.PeptideFragmentOffsets[slot];
                int fragEnd = batch.PeptideFragmentOffsets[slot + 1];
                int fragCount = fragEnd - fragStart;

                if (_matchBuffer.Length < fragCount)
                    _matchBuffer = new FragmentMatch[Math.Max(fragCount, _matchBuffer.Length * 2)];

                int precursorCharge = _data.ScanPrecursorCharges[scanIndex];
                int matchCount = 0;

                for (int local = 0; local < fragCount; local++)
                {
                    double theoreticalMass = batch.FragmentNeutralMasses[fragStart + local];

                    // unknown fragment mass; rare, for sequences with unknown amino acids
                    if (double.IsNaN(theoreticalMass))
                        continue;

                    int idx = _data.GetClosestFragmentIndex(scanIndex, theoreticalMass);
                    if (idx < 0)
                        continue;

                    double experimentalMass = _data.FragmentMonoMasses[idx];
                    int experimentalCharge = _data.FragmentCharges[idx];

                    if (_productTolerance.Within(experimentalMass, theoreticalMass)
                        && Math.Abs(experimentalCharge) <= Math.Abs(precursorCharge))
                    {
                        _matchBuffer[matchCount++] = new FragmentMatch(
                            local,
                            experimentalMass.ToMz(experimentalCharge),
                            _data.FragmentIntensities[idx],
                            experimentalCharge);
                    }
                }

                if (matchCount > 0)
                    sink.AcceptWorkItem(w, _matchBuffer, matchCount);
            }
        }

        public void Dispose() { /* nothing to release on the CPU path */ }
    }
}

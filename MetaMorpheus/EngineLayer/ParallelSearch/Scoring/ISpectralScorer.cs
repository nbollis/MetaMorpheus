using System;

namespace EngineLayer.ParallelSearch.Scoring
{
    /// <summary>
    /// Abstraction over the hot inner step of the transient classic search: for each
    /// (peptide, candidate-scan) work item, decide which theoretical fragments match an
    /// experimental fragment within tolerance and the charge filter.
    ///
    /// Deliberately narrow: the scorer does ONLY the matching (the expensive binary-search +
    /// tolerance work that is identical across all 30k databases). Score computation, the
    /// <c>ScoreCutoff</c> gate, MatchedFragmentIon construction and PSM updates all stay in the
    /// engine.
    ///
    /// Implementation:
    ///   CpuSpectralScorer — binary search per fragment on the CPU.
    ///
    /// One instance per thread (not safe for concurrent ScoreBatch calls on the same instance).
    /// </summary>
    public interface ISpectralScorer : IDisposable
    {
        /// <summary>
        /// Match every work item in <paramref name="batch"/>, reporting each item's matched
        /// fragments to <paramref name="sink"/>. Items with no matches need not be reported.
        /// </summary>
        void ScoreBatch(ScoringBatch batch, IScoringSink sink);

        /// <summary>Human-readable backend description (for startup logging).</summary>
        string BackendDescription { get; }
    }

    /// <summary>One matched theoretical-vs-experimental fragment pairing.</summary>
    public readonly struct FragmentMatch
    {
        /// <summary>Index of the theoretical product within its peptide's product list.</summary>
        public readonly int LocalProductIndex;
        /// <summary>Experimental m/z = monoisotopic mass converted at the envelope charge.</summary>
        public readonly double ExperimentalMz;
        /// <summary>Representative (first-peak) intensity of the matched experimental envelope.</summary>
        public readonly double ExperimentalIntensity;
        /// <summary>Charge of the matched experimental envelope.</summary>
        public readonly int ExperimentalCharge;

        public FragmentMatch(int localProductIndex, double experimentalMz, double experimentalIntensity, int experimentalCharge)
        {
            LocalProductIndex = localProductIndex;
            ExperimentalMz = experimentalMz;
            ExperimentalIntensity = experimentalIntensity;
            ExperimentalCharge = experimentalCharge;
        }
    }

    /// <summary>
    /// Receives match results one work item at a time. The <paramref name="matches"/> array is a
    /// reusable buffer owned by the scorer — the sink must consume entries [0, matchCount) during
    /// the call and not retain the array.
    /// </summary>
    public interface IScoringSink
    {
        void AcceptWorkItem(int workIndex, FragmentMatch[] matches, int matchCount);
    }

    /// <summary>
    /// A batch of (peptide, candidate-scan) work items to match, in struct-of-arrays form.
    /// Theoretical fragment masses are concatenated per peptide slot (CSR via
    /// <see cref="PeptideFragmentOffsets"/>); NaN masses are kept in place so
    /// <see cref="FragmentMatch.LocalProductIndex"/> aligns with the peptide's product list (the
    /// scorer skips NaN, which never matches).
    /// </summary>
    public sealed class ScoringBatch
    {
        /// <summary>Number of work items.</summary>
        public int WorkItemCount;

        /// <summary>Peptide slot for each work item (index into PeptideFragmentOffsets).</summary>
        public int[] WorkPeptideSlot = Array.Empty<int>();
        /// <summary>Candidate scan index for each work item (index into the SpectralScoringData).</summary>
        public int[] WorkScanIndex = Array.Empty<int>();

        /// <summary>Number of distinct peptide slots in this batch.</summary>
        public int PeptideCount;
        /// <summary>CSR offsets into <see cref="FragmentNeutralMasses"/>; length = PeptideCount + 1.</summary>
        public int[] PeptideFragmentOffsets = new int[1];
        /// <summary>Concatenated theoretical product neutral masses for all peptide slots.</summary>
        public double[] FragmentNeutralMasses = Array.Empty<double>();

        /// <summary>Reset counters for reuse without reallocating the backing arrays.</summary>
        public void Clear()
        {
            WorkItemCount = 0;
            PeptideCount = 0;
            PeptideFragmentOffsets[0] = 0;
        }
    }
}

using System.Collections.Generic;
using MassSpectrometry;
using Omics.Fragmentation;

namespace EngineLayer.SpectrumMatch.Scoring;

public class MorpheusScore : ScoreFunction
{
    public override double CalculatePeptideScore(MsDataScan thisScan, List<MatchedFragmentIon> matchedFragmentIons)
    {
        double score = 0;

        foreach (var ion in matchedFragmentIons)
        {
            switch (ion.NeutralTheoreticalProduct.ProductType)
            {
                case ProductType.D:
                    break;
                default:
                    score += 1 + ion.Intensity / thisScan.TotalIonCurrent;
                    break;
            }
        }

        return score;
    }
}
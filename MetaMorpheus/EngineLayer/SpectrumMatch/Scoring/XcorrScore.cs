using System;
using System.Collections.Generic;
using System.Linq;
using MassSpectrometry;
using Omics.Fragmentation;

namespace EngineLayer.SpectrumMatch.Scoring;

public class XcorrScore() : ScoreFunction
{
    public override double CalculatePeptideScore(MsDataScan thisScan, List<MatchedFragmentIon> matchedFragmentIons)
    {
        double score = 0;

        foreach (var fragment in matchedFragmentIons)
        {
            switch (fragment.NeutralTheoreticalProduct.ProductType)
            {
                case ProductType.aDegree:
                case ProductType.aStar:
                case ProductType.bWaterLoss:
                case ProductType.bAmmoniaLoss:
                case ProductType.yWaterLoss:
                case ProductType.yAmmoniaLoss:
                    score += 0.01 * fragment.Intensity;
                    break;
                case ProductType.D: //count nothing for diagnostic ions.
                    break;
                default:
                    score += 1 * fragment.Intensity;
                    break;
            }
        }

        return score;
    }
}
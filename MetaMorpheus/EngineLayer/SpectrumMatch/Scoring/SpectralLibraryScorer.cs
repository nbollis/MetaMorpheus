using System.Collections.Generic;
using MassSpectrometry;
using Omics.Fragmentation;

namespace EngineLayer.SpectrumMatch.Scoring;

/// <summary>
///Used only when user wants to generate spectral library.
///Normal search only looks for one match ion for one fragment, and if it accepts it then it doesn't try to look for different charge states of that same fragment. 
///The score will be the number of matched ions and plus some fraction calculated by intensity(matchedFragmentIons[i].Intensity / thisScan.TotalIonCurrent).
///Like b1, b2, b3 will have score 3.xxx;But when generating library, we need look for match ions with all charges.So we will have b1,b2,b3, b1^2, b2^3. If using 
///the normal scoring function, the score will be 5.xxxx, which is not proper. The score for b1 and b1^2 should also be 1 plus some some fraction calculated by intensity, 
///because they are matching the same fragment ion just with different charges. So b1, b2, b3, b1^2, b2^3 should be also 3.xxx(but a little higher than b1, b2, b3 as 
///the fraction part) rather than 5.xxx. 
/// </summary>
public class SpectralLibraryScorer : BaseSpectralMatchScorer
{
    public override string Name => "SpectralLibrary";
    public override double CalculatePeptideScore(MsDataScan thisScan, List<MatchedFragmentIon> matchedFragmentIons)
    {
        double score = 0;
        double totalIonCurrent = thisScan.TotalIonCurrent;

        // Morpheus score
        HashSet<string> ions = new(matchedFragmentIons.Count);
        foreach (MatchedFragmentIon ion in matchedFragmentIons)
        {
            var product = ion.NeutralTheoreticalProduct;
            string ionKey = $"{product.ProductType}{product.FragmentNumber}";
            score += ions.Add(ionKey) 
                ? 1 + ion.Intensity / totalIonCurrent 
                : ion.Intensity / totalIonCurrent;
        }

        return score;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using Omics;
using Omics.SpectrumMatch;
using Proteomics.ProteolyticDigestion;
using Proteomics.PSM;
using Transcriptomics;
using Transcriptomics.Digestion;

namespace GuiFunctions
{
    public static class MzlibExtensions
    {
        public static bool IsCrossLinkedPeptide(this SpectrumMatchFromTsv sm)
        {
            return sm is PsmFromTsv { BetaPeptideBaseSequence: not null };
        }

        public static bool IsPeptide(this SpectrumMatchFromTsv sm)
        {
            if (sm is OsmFromTsv)
                return false;
            return true;
        }

        public static IBioPolymerWithSetMods ToBioPolymerWithSetMods(this SpectrumMatchFromTsv sm)
        {
            if (sm.IsPeptide())
                return new PeptideWithSetModifications(sm.FullSequence, GlobalVariables.AllModsKnownDictionary);
            else
                return new OligoWithSetMods(sm.FullSequence, GlobalVariables.AllRnaModsKnownDictionary);
        }

        public static SpectrumMatchFromTsv ReplaceFullSequence(this SpectrumMatchFromTsv sm, string fullSequence, string baseSequence = "")
        {
            if (sm.IsPeptide())
                return new PsmFromTsv(sm as PsmFromTsv, fullSequence, baseSequence: baseSequence);
            else
                return new OsmFromTsv(sm as OsmFromTsv, fullSequence, baseSequence: baseSequence);
        }
    }
}

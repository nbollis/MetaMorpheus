﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using iText.StyledXmlParser.Jsoup.Select;
using Omics;
using Omics.SpectrumMatch;
using Proteomics.ProteolyticDigestion;
using Proteomics.PSM;
using TaskLayer;
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

        public static IBioPolymerWithSetMods ToBioPolymerWithSetMods(this SpectrumMatchFromTsv sm, string fullSequence = null)
        {
            if (sm.IsPeptide())
                return new PeptideWithSetModifications(fullSequence ?? sm.FullSequence, GlobalVariables.AllModsKnownDictionary);
            else
                return new OligoWithSetMods(fullSequence ?? sm.FullSequence, GlobalVariables.AllRnaModsKnownDictionary);
        }

        public static SpectrumMatchFromTsv ReplaceFullSequence(this SpectrumMatchFromTsv sm, string fullSequence, string baseSequence = "")
        {
            if (sm.IsPeptide())
                return new PsmFromTsv(sm as PsmFromTsv, fullSequence, baseSequence: baseSequence);
            else
                return new OsmFromTsv(sm as OsmFromTsv, fullSequence, baseSequence: baseSequence);
        }

        public static IEnumerable<(int Start, int End)> GetStartAndEndPosition(this SpectrumMatchFromTsv sm)
        {
            foreach (var ambigSplit in sm.StartAndEndResiduesInParentSequence.Split('|'))
            {
                var split = ambigSplit.Replace("[", "").Replace("]", "").Split("to");
                yield return (int.Parse(split[0]), int.Parse(split[1]));
            }
        }
    }
}
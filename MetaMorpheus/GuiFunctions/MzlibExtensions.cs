using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Omics.SpectrumMatch;
using Proteomics.PSM;
using Transcriptomics;

namespace GuiFunctions
{
    public static class MzlibExtensions
    {
        public static bool IsCrossLinkedPeptide(this SpectrumMatchFromTsv sm)
        {
            if (sm is PsmFromTsv psm)
            {
                return psm.BetaPeptideChildScanMatchedIons != null;
            }

            return false;
        }

        public static bool IsPeptide(this SpectrumMatchFromTsv sm)
        {
            if (sm is OsmFromTsv)
                return false;
            return true;
        }
    }
}

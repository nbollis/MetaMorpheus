using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;

namespace GuiFunctions
{
    public static class ResultAnalysisVariables
    {
        public static double QValueFilter = 0.01;
        public static double PepFilter = 0.1;
        public static bool FilterByQ = true;
        public static bool FilterByPep = false;
        public static bool CalculateForPsms = true;
        public static bool FilterOutDecoys = true;

        public static bool PassesFilter(this PsmFromTsv psm)
        {
            if (psm == null)
                return false;
            if (FilterOutDecoys && psm.DecoyContamTarget == "D")
                return false;
            return (!FilterByQ || (psm.QValue <= QValueFilter)) && (!FilterByPep || (psm.PEP < PepFilter));
        }
    }
}

using MzLibUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Transcriptomics.Digestion;

namespace TaskLayer
{
    public class RnaSearchParameters : SearchParameterParent
    {
        public RnaSearchParameters()
        {
            DoParsimony = false;
            // Output Options
            ModsToWriteSelection = new Dictionary<string, int>
            {
                //Key is modification type.

                //Value is integer 0, 1, 2 and 3 interpreted as:
                //   0:   Do not Write
                //   1:   Write if in DB and Observed
                //   2:   Write if in DB
                //   3:   Write if Observed
                {"Biological", 3},
                {"Digestion Termini", 3},
                {"Metal", 3},
            };
        }
    }
}

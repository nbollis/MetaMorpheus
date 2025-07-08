using System.Collections.Generic;

namespace TaskLayer
{
    public class RnaSearchParameters : SearchParameters
    {
        public RnaSearchParameters() : base()
        {
            DoLocalizationAnalysis = false;
            DoLabelFreeQuantification = false;
            WritePepXml = false;


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

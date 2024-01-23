using MzLibUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Transcriptomics.Digestion;
using UsefulProteomicsDatabases;

namespace TaskLayer
{
    public class RnaSearchParameters
    {
        public RnaSearchParameters()
        {
            // SearchTask Build Stuff
            DisposeOfFileWhenDone = true;
            MassDiffAcceptorType = MassDiffAcceptorType.OneMM;
            CustomMdac = null;
            DecoyType = DecoyType.Reverse;

            // Output Options
            WriteHighQValueOsms = true;
            WriteDecoys = true;
            WriteContaminants = true;
            WriteAmbiguous = true;
            WriteIndividualFiles = true;
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

        #region SearchTask Build Stuff

        public bool DisposeOfFileWhenDone { get; set; }
        public MassDiffAcceptorType MassDiffAcceptorType { get; set; }
        public string CustomMdac { get; set; }
        public DecoyType DecoyType { get; set; }

        #endregion

        #region Output Options

        public bool WriteHighQValueOsms { get; set; }
        public bool WriteDecoys { get; set; }
        public bool WriteContaminants { get; set; }
        public bool WriteAmbiguous { get; set; }
        public bool WriteIndividualFiles { get; set; }
        public Dictionary<string, int> ModsToWriteSelection { get; set; }


        #endregion
    }
}

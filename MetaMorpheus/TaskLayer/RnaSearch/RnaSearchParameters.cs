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
            FragmentIonTolerance = new PpmTolerance(10);
            PrecursorMassTolerance = new PpmTolerance(10);
            DecoyType = DecoyType.Reverse;

            // Output Options
            WriteHighQValueOsms = true;
            WriteDecoys = true;
            WriteContaminants = true;
            WriteAmbiguous = true;
            WriteIndividualFiles = true;
        }

        #region SearchTask Build Stuff

        public bool DisposeOfFileWhenDone { get; set; }
        public MassDiffAcceptorType MassDiffAcceptorType { get; set; }
        public string CustomMdac { get; set; }
        public PpmTolerance FragmentIonTolerance { get; set; }
        public PpmTolerance PrecursorMassTolerance { get; set; }
        public DecoyType DecoyType { get; set; }

        #endregion

        #region Output Options

        public bool WriteHighQValueOsms { get; set; }
        public bool WriteDecoys { get; set; }
        public bool WriteContaminants { get; set; }
        public bool WriteAmbiguous { get; set; }
        public bool WriteIndividualFiles { get; set; }


        #endregion
    }
}

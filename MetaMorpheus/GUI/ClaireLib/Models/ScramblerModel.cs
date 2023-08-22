using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsefulProteomicsDatabases;

namespace MetaMorpheusGUI
{
    public class ScramblerModel : ScramblerVM
    {
        public static ScramblerModel Instance => new ScramblerModel();

        private ScramblerModel()
        {
            Loaders.LoadElements();
            ProteinSequence = "PEPTIDE";
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetaMorpheusGUI
{
    public class ScramblerModel : ScramblerVM
    {
        public static ScramblerModel Instance => new ScramblerModel();

        private ScramblerModel()
        {
            ProteinSequence = "PEPTIDE";
        }

    }
}

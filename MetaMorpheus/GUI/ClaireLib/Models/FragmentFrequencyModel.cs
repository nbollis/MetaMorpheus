using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetaMorpheusGUI
{
    public class FragmentFrequencyModel : FragmentFrequencyVM
    {
        public static FragmentFrequencyModel Instance => new FragmentFrequencyModel();
        public FragmentFrequencyModel() : base()
        {
            PsmFileList.Add("tacos.psmtsv");
        }
    }
}

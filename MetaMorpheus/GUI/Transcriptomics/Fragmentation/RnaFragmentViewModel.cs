using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GuiFunctions;
using Omics.Fragmentation;

namespace MetaMorpheusGUI
{
    public class RnaFragmentViewModel : BaseViewModel
    {
        public RnaFragmentViewModel(bool use, ProductType type)
        {
            Use = use;
            ProductType = type;
        }

        public ProductType ProductType { get; }
        public string TypeString => ProductType.ToString();

        private bool use;
        public bool Use
        {
            get => use;
            set { use = value; OnPropertyChanged(nameof(Use)); }
        }
    }
}

using GuiFunctions;
using Omics.Fragmentation;

namespace MetaMorpheusGUI;

public class RnaFragmentVm : BaseViewModel
{
    public RnaFragmentVm(bool use, ProductType type)
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
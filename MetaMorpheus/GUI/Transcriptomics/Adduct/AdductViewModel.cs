using GuiFunctions;

namespace MetaMorpheusGUI;

public class AdductViewModel : BaseViewModel
{
    public Adduct Adduct { get; set; }

    private int maxCount;
    public int MaxCount
    {
        get => maxCount;
        set { maxCount = value; OnPropertyChanged(nameof(MaxCount)); }
    }

    public string Name => Adduct.Name;

    public AdductViewModel(Adduct adduct, int maxCount = 2)
    {
        MaxCount = maxCount;
        Adduct = adduct;
    }
}
using GuiFunctions;
using MzLibUtil;

namespace MetaMorpheusGUI;

public class RnaSearchParametersVm : BaseViewModel
{
    private RnaSearchParameters parameters;

    public bool MatchMs1
    {
        get => parameters.MatchMs1;
        set { parameters.MatchMs1 = value; OnPropertyChanged(nameof(MatchMs1));}
    }
    public bool MatchMs2
    {
        get => parameters.MatchMs2;
        set { parameters.MatchMs2 = value; OnPropertyChanged(nameof(MatchMs2));}
    }
    public int MinScanId
    {
        get => parameters.MinScanId;
        set { parameters.MinScanId = value; OnPropertyChanged(nameof(MinScanId));}
    }
    public int MaxScanId
    {
        get => parameters.MaxScanId;
        set { parameters.MaxScanId = value; OnPropertyChanged(nameof(MaxScanId));}
    }

    public bool MatchAllCharges
    {
        get => parameters.MatchAllCharges;
        set { parameters.MatchAllCharges = value; OnPropertyChanged(nameof(MatchAllCharges)); }
    }

    public double FragmentIonTolerance
    {
        get => parameters.FragmentIonTolerance.Value;
        set { parameters.FragmentIonTolerance = new PpmTolerance(value); OnPropertyChanged(nameof(FragmentIonTolerance)); }
    }

    public RnaSearchParametersVm()
    {
        parameters = new();
    }
}
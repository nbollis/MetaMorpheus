using GuiFunctions;

namespace MetaMorpheusGUI;

public class RnaBigVm : BaseViewModel
{
    private RnaTargetedSearchVm searchVm;

    public RnaTargetedSearchVm SearchVm
    {
        get => searchVm;
        set { searchVm = value; OnPropertyChanged(nameof(SearchVm)); }
    }

    private RnaVisualizationVm visualizationVm;

    public RnaVisualizationVm VisualizationVm
    {
        get => visualizationVm;
        set { visualizationVm = value; OnPropertyChanged(nameof(VisualizationVm)); }
    }

    private AdductCalculatorViewModel adductVm;

    public AdductCalculatorViewModel AdductVm
    {
        get => adductVm;
        set { adductVm = value; OnPropertyChanged(nameof(AdductVm)); }
    }

    public RnaBigVm()
    {
        VisualizationVm = new();
        SearchVm = new();
        AdductVm = new();
    }
}
using System.Collections.ObjectModel;
using Omics.Fragmentation;

namespace MetaMorpheusGUI;

public class RnaVisualizationModel : RnaVisualizationVm
{
    public static RnaVisualizationModel Instance => new RnaVisualizationModel();

    public RnaVisualizationModel()
    {
        PossibleProducts = new ObservableCollection<RnaFragmentVm>()
        {
            new(false, ProductType.a),
            new(true, ProductType.aBaseLoss),
            new(false, ProductType.aWaterLoss),
            new(false, ProductType.b),
            new(false, ProductType.bBaseLoss),
            new(false, ProductType.bWaterLoss),
            new(true, ProductType.c),
            new(false, ProductType.cBaseLoss),
            new(false, ProductType.cWaterLoss),
            new(false, ProductType.d),
            new(false, ProductType.dBaseLoss),
            new(true, ProductType.dWaterLoss),
            new(false, ProductType.w),
            new(false, ProductType.wBaseLoss),
            new(false, ProductType.wWaterLoss),
            new(false, ProductType.x),
            new(false, ProductType.xBaseLoss),
            new(false, ProductType.xWaterLoss),
            new(true, ProductType.y),
            new(false, ProductType.yBaseLoss),
            new(false, ProductType.yWaterLoss),
            new(false, ProductType.z),
            new(false, ProductType.zBaseLoss),
            new(false, ProductType.zWaterLoss),
        };
    }
}
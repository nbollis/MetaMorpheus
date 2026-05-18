using System.Collections.Generic;
using Chemistry;
using MassSpectrometry;
using Omics.Fragmentation;

namespace EngineLayer.Util;
public static class ComplementaryIonConversion
{
    public static readonly Dictionary<DissociationType, List<double>> complementaryIonConversionDictionary = new Dictionary<DissociationType, List<double>>
    {
        { DissociationType.LowCID, new List<double>(){ Constants.ProtonMass } },
        { DissociationType.HCD, new List<double>(){ Constants.ProtonMass } },
        { DissociationType.ETD,new List<double>() {2 * Constants.ProtonMass } },
        { DissociationType.CID,new List<double>() {Constants.ProtonMass } },
        { DissociationType.EThcD,new List<double>() {Constants.ProtonMass, 2 * Constants.ProtonMass } },
    };

    public static readonly Dictionary<ProductType, ProductType> ProteinComplementaryConversion = new()
    {
        { ProductType.a, ProductType.x },
        { ProductType.aStar, ProductType.x },
        { ProductType.aDegree, ProductType.x },
        { ProductType.aWaterLoss, ProductType.x },
        { ProductType.aBaseLoss, ProductType.x },
        { ProductType.b, ProductType.y },
        { ProductType.bAmmoniaLoss, ProductType.y },
        { ProductType.bWaterLoss, ProductType.y },
        { ProductType.bBaseLoss, ProductType.y },
        { ProductType.c, ProductType.z },
        { ProductType.cWaterLoss, ProductType.z },
        { ProductType.cBaseLoss, ProductType.z },

        { ProductType.x, ProductType.a },
        { ProductType.xWaterLoss, ProductType.a },
        { ProductType.xBaseLoss, ProductType.a },
        { ProductType.y, ProductType.b },
        { ProductType.yAmmoniaLoss, ProductType.b },
        { ProductType.yWaterLoss, ProductType.b },
        { ProductType.yBaseLoss, ProductType.b },
        { ProductType.z, ProductType.c },
        { ProductType.zDot, ProductType.c },
        { ProductType.zPlusOne, ProductType.c },
        { ProductType.zWaterLoss, ProductType.c },
        { ProductType.zBaseLoss, ProductType.c },
    };

    public static readonly Dictionary<ProductType, ProductType> NucleotideComplementaryConversion = new()
    {
        { ProductType.a, ProductType.w },
        { ProductType.aWaterLoss, ProductType.wWaterLoss },
        { ProductType.aBaseLoss, ProductType.wBaseLoss },
        { ProductType.b, ProductType.x },
        { ProductType.bWaterLoss, ProductType.xWaterLoss },
        { ProductType.bBaseLoss, ProductType.xBaseLoss },
        { ProductType.c, ProductType.y },
        { ProductType.cWaterLoss, ProductType.yWaterLoss },
        { ProductType.cBaseLoss, ProductType.yBaseLoss },
        { ProductType.d, ProductType.z },
        { ProductType.dWaterLoss, ProductType.zWaterLoss },
        { ProductType.dBaseLoss, ProductType.zBaseLoss },

        { ProductType.w, ProductType.a },
        { ProductType.wWaterLoss, ProductType.aWaterLoss },
        { ProductType.wBaseLoss, ProductType.aBaseLoss },
        { ProductType.x, ProductType.b },
        { ProductType.xWaterLoss, ProductType.bWaterLoss },
        { ProductType.xBaseLoss, ProductType.bBaseLoss },
        { ProductType.y, ProductType.c },
        { ProductType.yWaterLoss, ProductType.cWaterLoss },
        { ProductType.yBaseLoss, ProductType.cBaseLoss },
        { ProductType.z, ProductType.d },
        { ProductType.zWaterLoss, ProductType.dWaterLoss },
        { ProductType.zBaseLoss, ProductType.dBaseLoss }
    };

    public static (ProductType complementType, FragmentationTerminus complementTerminus)? GetComplementInfo(ProductType productType, FragmentationTerminus originalTerminus)
    {
        ProductType complementType;

        if (ProteinComplementaryConversion.TryGetValue(productType, out complementType)
            || NucleotideComplementaryConversion.TryGetValue(productType, out complementType))
        {
            return (complementType, FlipTerminus(originalTerminus));
        }

        return null;
    }

    public static FragmentationTerminus FlipTerminus(FragmentationTerminus terminus) => terminus switch
    {
        FragmentationTerminus.N => FragmentationTerminus.C,
        FragmentationTerminus.C => FragmentationTerminus.N,
        FragmentationTerminus.FivePrime => FragmentationTerminus.ThreePrime,
        FragmentationTerminus.ThreePrime => FragmentationTerminus.FivePrime,
        _ => FragmentationTerminus.None,
    };
}

global using FragmentationTerminus = MassSpectrometry.FragmentationTerminus;
global using ProductType = MassSpectrometry.ProductType;

namespace EngineLayer
{
    public class GlobalSettings
    {
        public bool WriteExcelCompatibleTSVs { get; set; }
        public bool UserHasAgreedToThermoRawFileReaderLicence { get; set; }
    }
}
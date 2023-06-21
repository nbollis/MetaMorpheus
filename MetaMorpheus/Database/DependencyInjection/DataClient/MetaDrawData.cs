using EngineLayer;
using MassSpectrometry;

namespace MetaDrawBackend.DependencyInjection;
public class MetaDrawData
{
    public Lazy<List<PsmFromTsv>> PsmData { get; set; }
    public Lazy<List<MsDataFile>> MsDataFileData { get; set; }
    public Lazy<List<MsDataScan>> MsDataScanData { get; set; }
    public Lazy<List<LibrarySpectrum>> LibrarySpectraData { get; set; }
}
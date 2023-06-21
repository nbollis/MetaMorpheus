using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using MassSpectrometry;

namespace Database
{
    /// <summary>
    /// Used to resolve the types on the IOC container in the host application
    /// </summary>
    public interface IDataClient
    {
    }

    public class MetaDraw
    {
        public Lazy<List<PsmFromTsv>> PsmData { get; set; }
        public Lazy<List<MsDataFile>> MsDataFileData { get; set; }
        public Lazy<List<MsDataScan>> MsDataScanData { get; set; }
        public Lazy<List<LibrarySpectrum>> LibrarySpectraData { get; set; }
    }
}

using EngineLayer;
using MassSpectrometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Database
{
    public class MetaDrawMockDataClientClient : IDataClient
    {
        private MetaDraw? _data = null;
        private int _numberOfRecords = 0;

        public MetaDrawMockDataClientClient(int numberOfRecords)
        {
            _numberOfRecords = numberOfRecords;
        }

        public MetaDraw Data
        {
            get => _data ??= GetMetaDrawData();
            set => _data = value;
        }

        private MetaDraw GetMetaDrawData()
        {
            try
            {
                MetaDraw metadraw = new()
                {
                    PsmData = new Lazy<List<PsmFromTsv>>(() => MetaDrawMockData.GetPsms(_numberOfRecords)),
                    MsDataFileData = new Lazy<List<MsDataFile>>(() => MetaDrawMockData.GetMsDataFiles(_numberOfRecords)),
                    MsDataScanData = new Lazy<List<MsDataScan>>(() => MetaDrawMockData.GetMsDataScans(_numberOfRecords)),
                    LibrarySpectraData = new Lazy<List<LibrarySpectrum>>(() => MetaDrawMockData.GetLibrarySpectra(_numberOfRecords))
                };

                return metadraw;
            }
            catch (Exception e)
            {
                return null;
            }
        }
    }
}

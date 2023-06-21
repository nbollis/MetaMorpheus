using EngineLayer;
using MassSpectrometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetaDrawBackend.DependencyInjection
{
    public class MockMetaDrawDatabaseDirectClient : IMetaDrawData
    {
        private MetaDrawData? _data = null;
        private int _numberOfRecords = 0;

        public MockMetaDrawDatabaseDirectClient(int numberOfRecords)
        {
            _numberOfRecords = numberOfRecords;
        }

        public MetaDrawData Data
        {
            get => _data ??= GetMetaDrawData();
            set => _data = value;
        }

        private MetaDrawData GetMetaDrawData()
        {
            try
            {
                MetaDrawData metadraw = new()
                {
                    PsmData = new Lazy<List<PsmFromTsv>>(() => MockMetaDrawData.GetPsms(_numberOfRecords)),
                    MsDataFileData = new Lazy<List<MsDataFile>>(() => MockMetaDrawData.GetMsDataFiles(_numberOfRecords)),
                    MsDataScanData = new Lazy<List<MsDataScan>>(() => MockMetaDrawData.GetMsDataScans(_numberOfRecords)),
                    LibrarySpectraData = new Lazy<List<LibrarySpectrum>>(() => MockMetaDrawData.GetLibrarySpectra(_numberOfRecords))
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

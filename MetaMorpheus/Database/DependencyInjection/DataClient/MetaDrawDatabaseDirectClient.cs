using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using MassSpectrometry;

namespace MetaDrawBackend.DependencyInjection
{
    public class MetaDrawDatabaseDirectClient : IMetaDrawData
    {
        private MetaDrawDbAccess _dbAccess;
        private MetaDrawData? _data = null;

        public MetaDrawDatabaseDirectClient(bool getAllData)
        {
            _dbAccess = new MetaDrawDbAccess();
            if (getAllData)
                Data = GetMetaDrawData();
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
                    PsmData = new Lazy<List<PsmFromTsv>>(() => _dbAccess.GetPsms()),
                    MsDataFileData = new Lazy<List<MsDataFile>>(() => _dbAccess.GetMsDataFiles()),
                    MsDataScanData = new Lazy<List<MsDataScan>>(() => _dbAccess.GetMsDataScans()),
                    LibrarySpectraData = new Lazy<List<LibrarySpectrum>>(() => _dbAccess.GetLibrarySpectra())
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

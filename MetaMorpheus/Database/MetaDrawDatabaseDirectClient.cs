using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using MassSpectrometry;

namespace Database
{
    public class MetaDrawDatabaseDirectClient : IDataClient
    {
        private MetaDrawDbAccess _dbAccess;
        private MetaDraw? _data = null;

        public MetaDrawDatabaseDirectClient(Boolean getAllData)
        {
            _dbAccess = new MetaDrawDbAccess();
            if (getAllData)
                Data = GetMetaDrawData();
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

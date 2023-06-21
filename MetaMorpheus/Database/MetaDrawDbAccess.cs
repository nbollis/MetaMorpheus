using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using MassSpectrometry;

namespace Database
{
    public class MetaDrawDbAccess
    {
        private MetaDrawDbContext context = null;

        public MetaDrawDbAccess()
        {
            context = new MetaDrawDbContext();
        }

        public List<PsmFromTsv> GetPsms()
        {
            try
            {
                List<PsmFromTsv> psms = context.Psms.ToList();
                return psms;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public List<MsDataFile> GetMsDataFiles()
        {
            try
            {
                List<MsDataFile> dataFiles = context.MsDataFiles.ToList();
                return dataFiles;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public List<MsDataScan> GetMsDataScans()
        {
            try
            {
                List<MsDataScan> scans = context.MsDataScans.ToList();
                return scans;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public List<LibrarySpectrum> GetLibrarySpectra()
        {
            try
            {
                List<LibrarySpectrum> libSpectra = context.LibrarySpectra.ToList();
                return libSpectra;
            }
            catch (Exception e)
            {
                return null;
            }
        }
    }
}

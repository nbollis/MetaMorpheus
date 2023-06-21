using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using FizzWare.NBuilder;
using MassSpectrometry;

namespace Database
{
    public class MetaDrawMockData
    {
        public static List<PsmFromTsv> GetPsms(int size)
        {
            List<PsmFromTsv> psms = Builder<PsmFromTsv>.CreateListOfSize(size).Build().ToList();
            return psms;
        }

        public static List<MsDataFile> GetMsDataFiles(int size)
        {
            List<MsDataFile> psms = Builder<MsDataFile>.CreateListOfSize(size).Build().ToList();
            return psms;
        }

        public static List<MsDataScan> GetMsDataScans(int size)
        {
            List<MsDataScan> psms = Builder<MsDataScan>.CreateListOfSize(size).Build().ToList();
            return psms;
        }

        public static List<LibrarySpectrum> GetLibrarySpectra(int size)
        {
            List<LibrarySpectrum> psms = Builder<LibrarySpectrum>.CreateListOfSize(size).Build().ToList();
            return psms;
        }

    }
}

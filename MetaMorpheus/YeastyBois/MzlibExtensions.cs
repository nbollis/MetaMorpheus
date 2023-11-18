using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MassSpectrometry;
using YeastyBois.Database;
using YeastyBois.Models;

namespace YeastyBois
{
    public static class MzlibExtensions
    {
        public static DataFile ToDataFile(this MsDataFile file, YeastyBoiData data, int dataSetId) =>
            file.FilePath.ToDataFile(data, dataSetId);

        public static DataFile ToDataFile(this string filePath, YeastyBoiData data, int dataSetId)
        {
            int dataFileId = data.AllDataFiles.Value.Max(p => p.DataFileId) + 1;
            return new DataFile()
            {
                DataFileId = dataFileId,
                DataSetId = dataSetId,
                DataFilePath = filePath,
                ResultId = dataFileId
            };
        }
    }
}

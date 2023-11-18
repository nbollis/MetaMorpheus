using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YeastyBois.Models;

namespace YeastyBois.Database
{
    public class MockedYeastyBoiDataClient : IYeastyBoiData
    {
        private YeastyBoiData _data;
        private int numberOfRecrods = 0;

        public MockedYeastyBoiDataClient(int numberOfRecords)
        {
            this.numberOfRecrods = numberOfRecords;
        }

        public YeastyBoiData Data
        {
            get => _data ??= GetYeastyBoiData();
            set => _data = value;
        }

        private YeastyBoiData GetYeastyBoiData()
        {
            YeastyBoiData data = new();
            data.AllDataFiles = new Lazy<List<DataFile>>(MockData.GetDataFiles(numberOfRecrods));
            data.AllResults = new Lazy<List<Results>>(MockData.GetResults(numberOfRecrods));
            data.AllDataSets = new Lazy<List<DataSet>>(MockData.GetDataSets(numberOfRecrods));
            return data;
        }

        internal static class MockData
        {
            public static List<DataSet> GetDataSets(int size)
            {
                List<DataSet> dataSets = new();
                for (int i = 1; i < size; i++)
                {
                    var set = new DataSet()
                    {
                        DataSetId = i,
                        Date = DateTime.Now
                    };
                    dataSets.Add(set);
                }

                return dataSets;
            }

            public static List<DataFile> GetDataFiles(int size)
            {
                List<DataFile> files = new();
                int id = 1;
                for (int i = 1; i < size; i++)
                {
                    for (int j = 1; j < 4; j++)
                    {
                        var file = new DataFile()
                        {
                            DataFileId = id,
                            DataSetId = i,
                            ResultId = id,
                            DataFilePath = "tacos",
                        };
                        files.Add(file);
                        id++;
                    }
                }

                return files;
            }

            public static List<Results> GetResults(int size)
            {
                List<Results> results = new();
                int id = 1;
                for (int i = 1; i < size; i++)
                {
                    for (int j = 1; j < 4; j++)
                    {
                        var result = new Results()
                        {
                            ResultId = id,
                            DataFileId = id,
                            Ms1ScanCount = 1,
                            Ms1Tic = 2,
                            Ms1Time = 1,
                            Ms2ScanCount = 3,
                            Ms2Tic = 4,
                            Ms2Time = 5,
                            BasePeakIntensity = 14,
                            PeptideCount = 3,
                            PsmCount = 6,
                            ProteinGroupCount = 12,
                        };
                        results.Add(result);
                        id++;
                    }
                }
                return results;
            }
        }
    }
}

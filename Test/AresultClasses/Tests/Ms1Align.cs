using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MassSpectrometry;

namespace Readers
{
    public class Ms1Align : MsDataFile
    {


        public Ms1Align(string filePath) : base(filePath)
        {
        }

        public override MsDataFile LoadAllStaticData(FilteringParams filteringParams = null, int maxThreads = 1)
        {
            throw new NotImplementedException();
        }

        public override SourceFile GetSourceFile()
        {
            throw new NotImplementedException();
        }

        public override MsDataScan GetOneBasedScanFromDynamicConnection(int oneBasedScanNumber, IFilteringParams filterParams = null)
        {
            throw new NotImplementedException();
        }

        public override void CloseDynamicConnection()
        {
            throw new NotImplementedException();
        }

        public override void InitiateDynamicConnection()
        {
            throw new NotImplementedException();
        }

        private void ParseEntryLines(string[] entryLines)
        {
            int id;
            int fractionId;
            string fileName;
            int oneBasedScanNumber = 0;
            double retentionTime = 0;
            int msnOrder = 0;


            foreach (var headerLine in entryLines.Where(p => p.Contains('=')))
            {
                var splits = headerLine.Split('=');
                switch (splits[0])
                {
                    case "ID":
                        id = int.Parse(splits[1]);
                        break;
                    case "FRACTION_ID":
                        fractionId = int.Parse(splits[1]);
                        break;
                    case "FILE_NAME":
                        fileName = splits[1];
                        break;
                    case "SCANS":
                        oneBasedScanNumber = int.Parse(splits[1]);
                        break;
                    case "RETENTION_TIME":
                        retentionTime = double.Parse(splits[1]);
                        break;
                    case "LEVEL":
                        msnOrder = int.Parse(splits[1]);
                        break;
                }
            }



        }
    }
}

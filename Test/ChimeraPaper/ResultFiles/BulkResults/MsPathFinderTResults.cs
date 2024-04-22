using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.ChimeraPaper.ResultFiles
{
    internal class MsPathFinderTResults : BulkResult
    {
        public MsPathFinderTResults(string directoryPath) : base(directoryPath)
        {
        }

        public override BulkResultCountComparisonFile IndividualFileComparison(string path = null)
        {
            throw new NotImplementedException();
        }

        public override ChimeraCountingFile CountChimericPsms()
        {
            throw new NotImplementedException();
        }

        public override BulkResultCountComparisonFile GetBulkResultCountComparisonFile(string path = null)
        {
            throw new NotImplementedException();
        }
    }
}

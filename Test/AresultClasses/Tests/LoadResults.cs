using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using GuiFunctions;
using NUnit.Framework;

namespace Test
{
    [TestFixture]
    public class LoadResults
    {
        [Test]
        public static void TESTNAME()
        {
            var temp = new MetaMorpheusRun(
                @"D:\Projects\Top Down MetaMorpheus\NB_Replicate_1.0.2\WithoutLibrary\CaliAvg_BioMetArtGPTMD_SearchWithInternalAndTruncations");
            temp.ExportEngineResults();
            temp.ExportTaskTimeResults();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Readers;

namespace Test.Transcriptomics
{
    internal class SequenceCoverageCalculation
    {
        

        [Test]
        public static void BuildSequenceCoverage()
        {
            string resultPath = @"B:\Users\Nic\DataFromIsabella\FLuc Methylation Experiment\c5_variablenophospthioate_highermaxCharge\Task2-RnaSearchTask\AllOSMs.osmtsv";

            var osms = new OsmFromTsvFile(resultPath);
            osms.LoadResults();


        }
    }
}

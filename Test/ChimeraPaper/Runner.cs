using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Test.ChimeraPaper
{
    internal class Runner
    {
        internal static string DirectoryPath = @"D:\Projects\Chimeras\Mann_11cell_analysis";
        internal static bool RunOnAll = true;

        [Test]
        public void PerformOperations()
        {
            var datasets = Directory.GetDirectories(DirectoryPath).Select(datasetDirectory => new Dataset(datasetDirectory)).ToList();
            if (!RunOnAll)
                datasets = datasets.Take(1).ToList();
            
            // perform operations
            foreach (var dataset in datasets)
            {
                dataset.CountChimericPsms();
            }
        }


    }
}

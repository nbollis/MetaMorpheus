using System;
using System.Collections.Generic;
using System.IO;
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
            string dirPath = @"B:\Users\AlexanderS_Bison\230619_DataFromIsabella\NicProfiling";
            foreach (var dir in Directory.GetDirectories(dirPath).Where(p => !p.Contains("Input")))
            {
                new MetaMorpheusRun(dir).ExportAllTimeResults(true);
            }
        }
    }
}

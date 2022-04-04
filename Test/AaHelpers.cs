using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    [TestFixture]

    internal static class AaHelpers
    {

        [Test]
        public static void FindNumberOfMods()
        {
            int count = 0;
            string filepath = @"C:\Users\Nic\source\repos\MetaMorpheus\EngineLayer\Mods\Mods.txt";
            foreach (var line in File.ReadAllLines(filepath))
            {
                if (line.StartsWith("ID"))
                    count++;
            }

            Assert.AreEqual(count, 0);
        }

    }
}

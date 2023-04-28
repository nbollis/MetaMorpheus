using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Test.AveragingPaper
{
    internal static class DatabaseConstants
    {
    }

    [TestFixture]
    public static class TestDB
    {
        public static string DatabasePath = @"B:\Users\Nic\ScanAveraging\Averaging.sql";
        [Test]
        public static void TESTNAME()
        {
            var connection = new SQLiteConnection($"Data Source={DatabasePath}");
            connection.Open();
            

        }
    }
}

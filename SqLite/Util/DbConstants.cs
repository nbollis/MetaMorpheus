using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqLite
{
    public class DbConstants
    {
        public static string DatabasePath { get; set; }

        public static string ConnectionString { get; set; }

        static DbConstants()
        {
            DatabasePath = @"B:\Users\Nic\ScanAveraging\Averaging.sql";
            ConnectionString = $"Data Source={DatabasePath}";
        }
    }
}

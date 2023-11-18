using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using YeastyBois.Database;

namespace Test.YeastyBoi
{
    public class FirstTest
    {
        [Test]
        public static void TestDbContext_First()
        {
         

            YeastyBoiDataDirectClient client = new YeastyBoiDataDirectClient(true);
            var data = client.Data;
            var temp = data.AllResults.Value.ToList();
        }
    }
}

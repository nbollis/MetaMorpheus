using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YeastyBois.Database;
using YeastyBois.Models;

namespace YeastyBois
{
    public static class DataBaseOperations
    {
        public static void AddDataSet(YeastyBoiData data, DataSet set)
        {
            using (var context = new YeastyBoiDbContext())
            {
                // save to database
                context.DataSets.Add(set);
                context.SaveChanges();

                // update local
                data.AllDataSets.Value.Add(set);
            }
        }

        public static void AddDataFile(YeastyBoiData data, DataFile file)
        {
            using (var context = new YeastyBoiDbContext())
            {
                // save to database
                context.DataFiles.Add(file);
                context.SaveChanges();

                // update local
                data.AllDataFiles.Value.Add(file);
            }
        }

        public static void AddResult(YeastyBoiData data, Results result)
        {
            using (var context = new YeastyBoiDbContext())
            {
                // save to database
                context.Results.Add(result);
                context.SaveChanges();

                // update local
                data.AllResults.Value.Add(result);
            }
        }
    }
}

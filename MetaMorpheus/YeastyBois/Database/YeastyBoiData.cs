using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YeastyBois.Models;

namespace YeastyBois.Database
{
    public class YeastyBoiData
    {
        public Lazy<List<DataSet>> AllDataSets { get; set; }

        public Lazy<List<DataFile>> AllDataFiles { get; set; }

        public Lazy<List<Results>> AllResults { get; set; }
    }
}

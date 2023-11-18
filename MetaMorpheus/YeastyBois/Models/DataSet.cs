using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YeastyBois.Models
{
    public class DataSet
    {
        public int DataSetId { get; set; }

    }

    public class DataFile
    {
        public int DataFileId { get; set; }
        public int DataSetId { get; set; }
    }

    public class Results
    {
        public int ResultId { get; set; }
    }


    
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations.Schema;

namespace YeastyBois.Models
{
    public partial class DataSet
    {
        public DataSet()
        {
            //DataFiles = new HashSet<DataFile>();
        }

        public int DataSetId { get; set; }
        public DateTime Date { get; set; }

        //public virtual ICollection<DataFile> DataFiles { get; set; }

    }

    public partial class DataFile
    {
        public int DataFileId { get; set; }

        public int DataSetId { get; set; }

        public int ResultId { get; set; }

        public string DataFilePath { get; set; }
    }

    /// <summary>
    /// Represents the parsing of one datafile and its search results
    /// All values are averages besides those labeled Count
    /// </summary>
    public partial class Results
    {
        public int ResultId { get; set; }

        public int DataFileId { get; set; }

        public int Ms2ScanCount { get; set; }

        public int Ms1ScanCount { get; set; }

        public double Ms1Tic { get; set; }

        public double Ms2Tic { get; set; }

        public int PsmCount { get; set; }

        public int PeptideCount { get; set; }

        public int ProteinGroupCount { get; set; }

        public double BasePeakIntensity { get; set; }

        public double Ms1Time { get; set; }

        public double Ms2Time { get; set; }


    }


    
}

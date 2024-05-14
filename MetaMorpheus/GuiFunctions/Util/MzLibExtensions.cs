using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MassSpectrometry;

namespace GuiFunctions
{
    public static class MzLibExtensions
    {
        public static string FileNameWithoutExtension(this MsDataFile dataFile) 
            => System.IO.Path.GetFileNameWithoutExtension(dataFile.FilePath);
    }
}

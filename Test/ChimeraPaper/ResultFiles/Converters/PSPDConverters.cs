using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace Test.ChimeraPaper.ResultFiles.Converters
{
    public class PSPDMsOrderConverter : DefaultTypeConverter
    {
        public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
        {
            return int.Parse(text.Last().ToString());
        }

        public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
        {
            return $"MS{value}";
        }
    }
}

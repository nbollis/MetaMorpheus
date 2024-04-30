using System.Collections.Generic;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using MzLibUtil;

namespace Test.ChimeraPaper.ResultFiles.Converters;

public class CommaDelimitedToStringArrayTypeConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        return text.Split(',').Where(p => p != "").ToArray();
    }

    public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
    {
        var list = value as IEnumerable<string> ?? throw new MzLibException("Cannot convert input to IEnumerable<string>");
        return string.Join(',', list);
    }
}

using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using MzLibUtil;

namespace TaskLayer.ParallelSearch.Util;

public class CommaDelimitedToDoubleConcurrentBagTypeConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        var splits = text.Split(',');
        var toReturn = splits.Where(p => p != "");
        return new System.Collections.Concurrent.ConcurrentBag<double>(toReturn.Select(double.Parse));
    }
    public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
    {
        var bag = value as System.Collections.Concurrent.ConcurrentBag<double> ?? throw new MzLibException("Cannot convert input to ConcurrentBag<double>");
        return string.Join(',', bag.Select(p => p.ToString("F2 db")));
    }
}
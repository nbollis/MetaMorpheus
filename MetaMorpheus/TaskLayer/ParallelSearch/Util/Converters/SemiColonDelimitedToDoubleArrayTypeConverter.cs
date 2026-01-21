#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using MzLibUtil;

namespace TaskLayer.ParallelSearch.Util.Converters;

public class SemiColonDelimitedToDoubleArrayTypeConverter : DefaultTypeConverter
{
    public static int RoundingPlaces = 4;
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        var splits = text.Split(';');
        var toReturn = splits.Where(p => p != "");
        return toReturn.Select(double.Parse).ToArray();
    }

    public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
    {
        var list = value as IEnumerable<double> ?? throw new MzLibException("Cannot convert input to IEnumerable<double>");
        return string.Join(';', list.Select(p => Math.Round(p, RoundingPlaces)));
    }
}
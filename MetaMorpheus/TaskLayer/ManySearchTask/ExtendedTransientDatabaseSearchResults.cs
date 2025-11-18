#nullable enable
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using CsvHelper.TypeConversion;
using MzLibUtil;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;

namespace TaskLayer;

public interface ITransientDbResults
{
    public string DatabaseName { get; set; }
}
public class ExtendedTransientDatabaseSearchResults : ITransientDbResults
{
    public ExtendedTransientDatabaseSearchResults() { }


    public string DatabaseName { get; set; } = string.Empty;
    public int TotalProteins { get; set; }
    public int TransientProteinCount { get; set; }
    public int TransientPeptideCount { get; set; }

    // PSMs 
    public int PsmTargets { get; set; }
    public int PsmDecoys { get; set; }
    public int PsmBacterialTargets { get; set; }
    public int PsmBacterialDecoys { get; set; }
    public int PsmBacterialUnambiguousTargets { get; set; }
    public int PsmBacterialUnambiguousDecoys { get; set; }
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] PsmBacterialUnambiguousTargetScores { get; set; } = Array.Empty<double>();
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] PsmBacterialUnambiguousDecoyScores { get; set; } = Array.Empty<double>();

    // Peptides 
    public int PeptideTargets { get; set; }
    public int PeptideDecoys { get; set; }
    public int PeptideBacterialTargets { get; set; }
    public int PeptideBacterialDecoys { get; set; }
    public int PeptideBacterialUnambiguousTargets { get; set; }
    public int PeptideBacterialUnambiguousDecoys { get; set; }
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] PeptideBacterialUnambiguousTargetScores { get; set; } = Array.Empty<double>();
    [TypeConverter(typeof(SemiColonDelimitedToDoubleArrayTypeConverter))]
    public double[] PeptideBacterialUnambiguousDecoyScores { get; set; } = Array.Empty<double>();

    // Proteins
    public int ProteinGroupTargets { get; set; }
    public int ProteinGroupDecoys { get; set; }
    public int ProteinGroupBacterialTargets { get; set; }
    public int ProteinGroupBacterialDecoys { get; set; }
    public int ProteinGroupBacterialUnambiguousTargets { get; set; }
    public int ProteinGroupBacterialUnambiguousDecoys { get; set; }

    private class SemiColonDelimitedToDoubleArrayTypeConverter : DefaultTypeConverter
    {
        public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
        {
            var splits = text.Split(';');
            var toReturn = splits.Where(p => p != "");
            return toReturn.Select(double.Parse).ToArray();
        }

        public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
        {
            var list = value as IEnumerable<double> ?? throw new MzLibException("Cannot convert input to IEnumerable<double>");
            return string.Join(';', list);
        }
    }
}
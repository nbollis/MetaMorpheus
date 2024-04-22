using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using CsvHelper.Configuration;
using Readers;

namespace Test.RyanJulain;
public class PrecursorFragmentMassSet : IEquatable<PrecursorFragmentMassSet>
{
    public static CsvConfiguration CsvConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        MissingFieldFound = null,
        Delimiter = ",",
        HasHeaderRecord = true,
        IgnoreBlankLines = true,
        TrimOptions = TrimOptions.Trim,
        BadDataFound = null
    };

    [Name("Accession")]
    public string Accession { get; set; }

    [Name("Full Sequence")]
    public string FullSequence { get; set; }
    [Name("PrecursorMass")]
    public double PrecursorMass { get; set; }

    [Name("FragmentMasses")]
    [TypeConverter(typeof(SemicolonDelimitedToDoubleListConverter))]
    public List<double> FragmentMasses { get; set; }

    [Name("FragmentCount")]
    public int FragmentCount { get; set; }



    [NotMapped] private HashSet<double> _fragmentMassesHashSet;
    [NotMapped] public HashSet<double> FragmentMassesHashSet => _fragmentMassesHashSet ??= new HashSet<double>(FragmentMasses);

    public PrecursorFragmentMassSet(double precursorMass, string accession, List<double> fragmentMasses, string fullSequence)
    {
        PrecursorMass = precursorMass;
        Accession = accession;
        FragmentMasses = fragmentMasses;
        FragmentCount = fragmentMasses.Count;
        FullSequence = fullSequence;
    }

    public PrecursorFragmentMassSet()
    {
    }

    public bool Equals(PrecursorFragmentMassSet other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return PrecursorMass.Equals(other.PrecursorMass) && Accession == other.Accession && FragmentMasses.SequenceEqual(other.FragmentMasses) && FragmentCount == other.FragmentCount;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((PrecursorFragmentMassSet)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(PrecursorMass, Accession, FragmentMasses, FragmentCount);
    }
}
public class PrecursorFragmentMassFile : ResultFile<PrecursorFragmentMassSet>, IResultFile
{
    public PrecursorFragmentMassFile(string filePath) : base(filePath) { }
    public PrecursorFragmentMassFile() : base() { }
    public override void LoadResults()
    {
        var csv = new CsvReader(new StreamReader(FilePath), PrecursorFragmentMassSet.CsvConfiguration);
        Results = csv.GetRecords<PrecursorFragmentMassSet>().ToList();
    }

    public override void WriteResults(string outputPath)
    {
        var csv = new CsvWriter(new StreamWriter(outputPath), PrecursorFragmentMassSet.CsvConfiguration);

        csv.WriteHeader<PrecursorFragmentMassSet>();
        foreach (var result in Results)
        {
            csv.NextRecord();
            csv.WriteRecord(result);
        }

        csv.Dispose();
    }

    public override SupportedFileType FileType { get; }
    public override Software Software { get; set; }
}
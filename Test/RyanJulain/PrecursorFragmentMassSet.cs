using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Linq;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
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
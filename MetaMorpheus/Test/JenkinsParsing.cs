using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EngineLayer;
using NUnit.Framework;

namespace Test;

[TestFixture]
internal class JenkinsParsing
{
    [Test]
    public void GetUniquePsmsFromRuns()
    {
        string oldDir = @"B:\Users\Nic\MiscOneOffs\GutFragDictSideEffects\Classic_BeforeChange";
        string newDir = @"B:\Users\Nic\MiscOneOffs\GutFragDictSideEffects\Classic_AfterChange";

        var oldJenkinsDir = new JenkinsBottomUpDirectory(oldDir, "BeforeChange");
        var newJenkinsDir = new JenkinsBottomUpDirectory(newDir, "AfterChange");
        var level1Comparer = new PsmLevel1Comparer();


        var unique = JenkinsDirectory.ParseUniquePsms(oldJenkinsDir.InitialPsms.AllPsms, newJenkinsDir.InitialPsms.AllPsms, level1Comparer);
        var uniqueFiltered = JenkinsDirectory.ParseUniquePsms(oldJenkinsDir.InitialPsms.FilteredPsms.ToList(), newJenkinsDir.InitialPsms.FilteredPsms.ToList(), level1Comparer);

        var uniquePostCalib = JenkinsDirectory.ParseUniquePsms(oldJenkinsDir.PostCalibPsms.AllPsms, newJenkinsDir.PostCalibPsms.AllPsms, level1Comparer);
        var uniquePostCalibFiltered = JenkinsDirectory.ParseUniquePsms(oldJenkinsDir.PostCalibPsms.FilteredPsms.ToList(), newJenkinsDir.PostCalibPsms.FilteredPsms.ToList(), level1Comparer);

        var uniquePostGptmd = JenkinsDirectory.ParseUniquePsms(oldJenkinsDir.PostGptmdPsms.AllPsms, newJenkinsDir.PostGptmdPsms.AllPsms, level1Comparer);
        var uniquePostGptmdFiltered = JenkinsDirectory.ParseUniquePsms(oldJenkinsDir.PostGptmdPsms.FilteredPsms.ToList(), newJenkinsDir.PostGptmdPsms.FilteredPsms.ToList(), level1Comparer);
    }
}

internal class PsmLevel1Comparer : IEqualityComparer<PsmFromTsv>
{
    public bool Equals(PsmFromTsv x, PsmFromTsv y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null) return false;
        if (y is null) return false;
        if (x.GetType() != y.GetType()) return false;
        return x.FullSequence == y.FullSequence
               && x.Ms2ScanNumber == y.Ms2ScanNumber
               && x.FileNameWithoutExtension == y.FileNameWithoutExtension
               && x.PrecursorScanNum == y.PrecursorScanNum
               && x.PrecursorCharge == y.PrecursorCharge
               && x.PrecursorMass.Equals(y.PrecursorMass)
               && x.Score.Equals(y.Score);
    }

    public int GetHashCode(PsmFromTsv obj)
    {
        return HashCode.Combine(obj.FullSequence, obj.Ms2ScanNumber, obj.FileNameWithoutExtension, obj.PrecursorScanNum, obj.PrecursorCharge, obj.PrecursorMass, obj.Score);
    }
}

public class PsmWrapper(string psmPath) : IEnumerable<PsmFromTsv>
{
    public Func<PsmFromTsv, bool> Filter { get; set; } = psm => psm.QValue <= 0.01;

    private readonly string PsmPath = psmPath;
    private List<PsmFromTsv>? _allPsms;
    public List<PsmFromTsv> AllPsms => _allPsms ??= PsmTsvReader.ReadTsv(PsmPath, out _);
    public IEnumerable<PsmFromTsv> FilteredPsms => AllPsms.Where(Filter).ToList();
    public IEnumerable<PsmFromTsv> Targets => AllPsms.Where(p => p.DecoyContamTarget == "T");
    public IEnumerable<PsmFromTsv> Decoys => AllPsms.Where(p => p.DecoyContamTarget == "D");
    public IEnumerable<PsmFromTsv> Contaminants => AllPsms.Where(p => p.DecoyContamTarget == "C");
    public IEnumerable<PsmFromTsv> FilteredTargets => AllPsms.Where(p => p.DecoyContamTarget == "T" && Filter(p)).ToList();



    public IEnumerable<PsmFromTsv> FilterPsms(Func<PsmFromTsv, bool>? filter = null)
    {
        filter ??= Filter;
        return AllPsms.Where(filter).ToList();
    }


    public IEnumerator<PsmFromTsv> GetEnumerator() => AllPsms.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class JenkinsBottomUpDirectory : JenkinsDirectory
{
    private readonly string PostCalibPsmPath;
    private readonly string PostGptmdPsmPath;
    public PsmWrapper PostCalibPsms { get; set; }
    public PsmWrapper PostGptmdPsms { get; set; }


    public JenkinsBottomUpDirectory(string directoryPath, string identifier) : base(directoryPath, identifier)
    {
        var classicPostCalibDir = Directory.GetDirectories(directoryPath).First(p => p.Contains("Task3"));
        PostCalibPsmPath = Path.Combine(classicPostCalibDir, "AllPSMs.psmtsv");
        PostCalibPsms = new PsmWrapper(PostCalibPsmPath);

        var classicPostGptmdDir = Directory.GetDirectories(directoryPath).First(p => p.Contains("Task5"));
        PostGptmdPsmPath = Path.Combine(classicPostGptmdDir, "AllPSMs.psmtsv");
        PostGptmdPsms = new PsmWrapper(PostGptmdPsmPath);
    }
}

public class JenkinsDirectory
{
    public readonly string DirectoryPath;
    public readonly string Identifier;

    protected readonly string InitialPsmPath;

    public PsmWrapper InitialPsms { get; set; }

    public JenkinsDirectory(string directoryPath, string identifier)
    {
        DirectoryPath = directoryPath;
        Identifier = identifier;

        var classicInitialDir = Directory.GetDirectories(directoryPath).First(p => p.Contains("Task1"));

        InitialPsmPath = Path.Combine(classicInitialDir, "AllPSMs.psmtsv");
        InitialPsms = new PsmWrapper(InitialPsmPath);
    }


    public static (List<PsmFromTsv> InitialNotInSecond, List<PsmFromTsv> SecondNotInInitial) ParseUniquePsms(List<PsmFromTsv> initial, List<PsmFromTsv> second, 
        IEqualityComparer<PsmFromTsv> comparer)
    {
        var secondSet = new HashSet<PsmFromTsv>(second, comparer);
        var initialNotInSecond = initial.Where(p => !secondSet.Contains(p)).ToList();

        var initialSet = new HashSet<PsmFromTsv>(initial, comparer);
        var secondNotInInitial = second.Where(p => !initialSet.Contains(p)).ToList();

        return (initialNotInSecond, secondNotInInitial);
    }

    public static IEnumerable<PsmFromTsv> GetUniquePsms(IEnumerable<PsmFromTsv> first, IEnumerable<PsmFromTsv> second, IEqualityComparer<PsmFromTsv> comparer)
    {
        return first.Where(p => !second.Contains(p, comparer));
    }

    public static IEnumerable<int> GetUniquePrecursorScanNumbers(IEnumerable<PsmFromTsv> first,
        IEnumerable<PsmFromTsv> second)
    {
        return first.Select(p => p.PrecursorScanNum).Except(second.Select(p => p.PrecursorScanNum));
    }

    public static IEnumerable<int> GetUniqueMs2ScanNumbers(IEnumerable<PsmFromTsv> first,
        IEnumerable<PsmFromTsv> second)
    {
        return first.Select(p => p.Ms2ScanNumber).Except(second.Select(p => p.Ms2ScanNumber));
    }

    public static IEnumerable<string> GetUniqueFullSequences(IEnumerable<PsmFromTsv> first,
        IEnumerable<PsmFromTsv> second)
    {
        return first.Select(p => p.FullSequence).Except(second.Select(p => p.FullSequence));
    }
}
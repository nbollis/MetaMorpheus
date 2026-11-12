using System.Collections.Generic;
using System.IO;
using System.Linq;
using EngineLayer.DatabaseLoading;
using NUnit.Framework;
using Proteomics.ProteolyticDigestion;
using TaskLayer.ParallelSearch;
using TaskLayer.ParallelSearch.Twins;

namespace Test.ParallelSearchTask.Twins;

[TestFixture]
public class TwinEntrapmentTests
{
    private static readonly string Suffix = TwinEntrapment.TwinSuffix;

    private static string NewFolder(string prefix)
    {
        string folder = Path.Combine(TestContext.CurrentContext.TestDirectory, $"{prefix}_{Path.GetRandomFileName()}");
        Directory.CreateDirectory(folder);
        return folder;
    }

    [Test]
    public void TwinName_RoundTripsToTarget()
    {
        string twin = TwinEntrapment.GetTwinName("UP000464024");

        Assert.That(twin, Is.EqualTo("UP000464024" + Suffix));
        Assert.That(TwinEntrapment.IsTwinName(twin), Is.True);
        Assert.That(TwinEntrapment.TryGetTargetName(twin, out string target), Is.True);
        Assert.That(target, Is.EqualTo("UP000464024"));
    }

    [Test]
    public void NonTwinNames_AreNotTwins()
    {
        foreach (string? name in new[] { "UP000464024", Suffix, "", null })
        {
            Assert.That(TwinEntrapment.IsTwinName(name), Is.False, name);
            Assert.That(TwinEntrapment.TryGetTargetName(name, out string target), Is.False, name);
            Assert.That(target, Is.Empty);
        }
    }

    [Test]
    public void NameConflicts_FindTwinLikeNamesAndCollisions()
    {
        var conflicts = TwinEntrapment.FindNameConflicts(new[] { "A", "A" + Suffix, "B", "C" + Suffix });

        Assert.That(conflicts, Is.EquivalentTo(new[] { "A", "A" + Suffix, "C" + Suffix }));
        Assert.That(TwinEntrapment.FindNameConflicts(new[] { "A", "B" }), Is.Empty);
    }

    [Test]
    public void UseTwinEntrapment_DefaultsOff()
    {
        Assert.That(new ParallelSearchParameters().UseTwinEntrapment, Is.False);
    }

    [Test]
    public void SettingsKey_IsStable_AndChangesWithDigestionOrBaseline()
    {
        string folder = NewFolder("twin_settings");
        try
        {
            string baselinePath = Path.Combine(folder, "human.fasta");
            File.WriteAllText(baselinePath, ">sp|P1|A\nPEPTIDEK\n");
            var baseline = new List<DbForTask> { new(baselinePath, false) };

            var settings = TwinEntrapment.DescribeSettings(new DigestionParams(), baseline);
            var again = TwinEntrapment.DescribeSettings(new DigestionParams(), baseline);
            var otherDigestion = TwinEntrapment.DescribeSettings(new DigestionParams(maxMissedCleavages: 5), baseline);

            File.AppendAllText(baselinePath, ">sp|P2|B\nANOTHERK\n");
            var otherBaseline = TwinEntrapment.DescribeSettings(new DigestionParams(), baseline);

            Assert.That(settings.Key, Has.Length.EqualTo(12));
            Assert.That(again.Key, Is.EqualTo(settings.Key));
            Assert.That(otherDigestion.Key, Is.Not.EqualTo(settings.Key));
            Assert.That(otherBaseline.Key, Is.Not.EqualTo(settings.Key));
            Assert.That(settings.Description, Does.Contain($"Seed: {TwinEntrapment.Seed}"));
            Assert.That(settings.Description, Does.Contain("human.fasta"));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Test]
    public void TwinDatabases_LiveBesideTheirTargets_InASettingsFolder_AndNameTheirTarget()
    {
        string folder = NewFolder("twin_paths");
        try
        {
            string databases = Directory.CreateDirectory(Path.Combine(folder, "dbs")).FullName;
            var targets = new List<DbForTask>
            {
                new(Path.Combine(databases, "A.fasta"), false),
                new(Path.Combine(databases, "B.xml"), false),
            };

            var twins = TwinEntrapment.GetTwinDatabases(targets, "abc123", Path.Combine(folder, "out"));

            Assert.That(twins.Select(t => t.FilePath), Is.EqualTo(new[]
            {
                Path.Combine(databases, TwinEntrapment.FolderName, "abc123", "A" + Suffix + ".fasta"),
                Path.Combine(databases, TwinEntrapment.FolderName, "abc123", "B" + Suffix + ".fasta"),
            }));
            Assert.That(twins.Select(t => t.DerivedFrom.Single().FilePath), Is.EqualTo(targets.Select(t => t.FilePath)));
            Assert.That(twins.All(t => !t.IsContaminant), Is.True);
            Assert.That(Directory.GetFiles(Path.Combine(databases, TwinEntrapment.FolderName, "abc123")), Is.Empty,
                "the write check leaves nothing behind");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Test]
    public void TwinDatabases_FallBackToTheOutputFolder_WhenTheTargetFolderCannotBeWritten()
    {
        string folder = NewFolder("twin_fallback");
        try
        {
            string databases = Directory.CreateDirectory(Path.Combine(folder, "dbs")).FullName;
            // a file where the twin folder should go makes that folder impossible to create
            File.WriteAllText(Path.Combine(databases, TwinEntrapment.FolderName), "not a folder");
            string output = Path.Combine(folder, "out");

            var twin = TwinEntrapment.GetTwinDatabases([new DbForTask(Path.Combine(databases, "A.fasta"), false)], "abc123", output).Single();

            Assert.That(twin.FilePath, Is.EqualTo(Path.Combine(output, TwinEntrapment.FolderName, "abc123", "A" + Suffix + ".fasta")));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Test]
    public void UnionReadOnlySet_ContainsEitherSet_AndCountsEachMemberOnce()
    {
        var union = new UnionReadOnlySet(new HashSet<string> { "A", "B" }, new HashSet<string> { "B", "C" });

        Assert.That(union.Contains("A") && union.Contains("B") && union.Contains("C"), Is.True);
        Assert.That(union.Contains("D"), Is.False);
        Assert.That(union.Count, Is.EqualTo(3));
        Assert.That(union, Is.EquivalentTo(new[] { "A", "B", "C" }));
        Assert.That(union.SetEquals(new[] { "C", "B", "A" }), Is.True);
        Assert.That(union.Overlaps(new[] { "Z", "C" }), Is.True);
    }
}

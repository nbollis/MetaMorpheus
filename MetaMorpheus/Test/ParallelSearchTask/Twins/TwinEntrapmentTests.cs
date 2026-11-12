using System.Collections.Generic;
using System.IO;
using System.Linq;
using EngineLayer.DatabaseLoading;
using NUnit.Framework;
using TaskLayer.ParallelSearch;
using TaskLayer.ParallelSearch.Twins;

namespace Test.ParallelSearchTask.Twins;

[TestFixture]
public class TwinEntrapmentTests
{
    [Test]
    public void TwinName_RoundTripsToTarget()
    {
        string twin = TwinEntrapment.GetTwinName("UP000464024");

        Assert.That(twin, Is.EqualTo("UP000464024_twin"));
        Assert.That(TwinEntrapment.IsTwinName(twin), Is.True);
        Assert.That(TwinEntrapment.TryGetTargetName(twin, out string target), Is.True);
        Assert.That(target, Is.EqualTo("UP000464024"));
    }

    [TestCase("UP000464024")]
    [TestCase("_twin")]
    [TestCase("")]
    [TestCase(null)]
    public void NonTwinNames_AreNotTwins(string? name)
    {
        Assert.That(TwinEntrapment.IsTwinName(name), Is.False);
        Assert.That(TwinEntrapment.TryGetTargetName(name, out string target), Is.False);
        Assert.That(target, Is.Empty);
    }

    [Test]
    public void TwinDatabases_FollowTargetsInOrder_InsideTheTwinFolder()
    {
        var targets = new List<DbForTask>
        {
            new(Path.Combine("dbs", "A.fasta"), false),
            new(Path.Combine("dbs", "B.xml"), false),
        };

        var twins = TwinEntrapment.GetTwinDatabases(targets, "out");

        Assert.That(twins.Select(t => t.FilePath), Is.EqualTo(new[]
        {
            Path.Combine("out", TwinEntrapment.FolderName, "A_twin.fasta"),
            Path.Combine("out", TwinEntrapment.FolderName, "B_twin.fasta"),
        }));
        Assert.That(twins.All(t => !t.IsContaminant), Is.True);
    }

    [Test]
    public void NameConflicts_FindTwinLikeNamesAndCollisions()
    {
        var conflicts = TwinEntrapment.FindNameConflicts(new[] { "A", "A_twin", "B", "C_twin" });

        Assert.That(conflicts, Is.EquivalentTo(new[] { "A", "A_twin", "C_twin" }));
        Assert.That(TwinEntrapment.FindNameConflicts(new[] { "A", "B" }), Is.Empty);
    }

    [Test]
    public void UseTwinEntrapment_DefaultsOff()
    {
        Assert.That(new ParallelSearchParameters().UseTwinEntrapment, Is.False);
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

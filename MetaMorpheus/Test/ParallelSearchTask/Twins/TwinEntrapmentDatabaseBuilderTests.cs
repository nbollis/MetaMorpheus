using System.Collections.Generic;
using System.IO;
using System.Linq;
using EngineLayer.DatabaseLoading;
using NUnit.Framework;
using Omics;
using Omics.Modifications;
using Proteomics;
using Proteomics.ProteolyticDigestion;
using TaskLayer.ParallelSearch.Twins;
using UsefulProteomicsDatabases;

namespace Test.ParallelSearchTask.Twins;

[TestFixture]
public class TwinEntrapmentDatabaseBuilderTests
{
    private static readonly DigestionParams Trypsin = new(minPeptideLength: 5);

    private static List<IBioPolymer> Targets() =>
    [
        new Protein("MAGLLVETSRPEKWYLQGNAIDHFTRCSVYEGLNPKAWTDLSIMEHR", "T1"),
        new Protein("MSDQEVLAGHKPYTFNWEDLRVSIGAMCTQEPLKHYDFAWNGSER", "T2"),
    ];

    private static List<IBioPolymer> Baseline() =>
    [
        new Protein("MKTAYIAKQRQISFVKSHFSRQLEERLGLIEVQAPILSRVGDGTQDNLSGAEK", "B1"),
    ];

    private static IEnumerable<string> Peptides(IEnumerable<Protein> proteins) =>
        proteins.SelectMany(p => p.Digest(Trypsin, new List<Modification>(), new List<Modification>()))
            .Select(p => p.BaseSequence);

    [Test]
    public void Twin_HasOnePartnerPerTarget_WithSameLengthAndEntrapmentAccessions()
    {
        var baselinePeptides = TwinEntrapmentDatabaseBuilder.CollectBaselinePeptides(Baseline(), Trypsin);
        var twin = TwinEntrapmentDatabaseBuilder.GenerateTwin(Targets(), baselinePeptides, Trypsin);

        Assert.That(twin, Has.Count.EqualTo(2));
        Assert.That(twin.All(p => p.IsEntrapment), Is.True);
        Assert.That(twin.All(p => p.Accession.StartsWith(ProteinDbLoader.DefaultEntrapmentIdentifier)), Is.True);
        Assert.That(twin.Select(p => p.Length).OrderBy(l => l),
            Is.EqualTo(Targets().Select(p => p.Length).OrderBy(l => l)));
    }

    [Test]
    public void Twin_HasNoPeptideOfTheBaselineOrItsOwnTarget()
    {
        var baselinePeptides = TwinEntrapmentDatabaseBuilder.CollectBaselinePeptides(Baseline(), Trypsin);
        var twin = TwinEntrapmentDatabaseBuilder.GenerateTwin(Targets(), baselinePeptides, Trypsin);

        var forbidden = baselinePeptides.Concat(Peptides(Targets().Cast<Protein>())).ToHashSet();
        Assert.That(Peptides(twin).Where(forbidden.Contains), Is.Empty);
    }

    [Test]
    public void Twin_IsDeterministic_AndIgnoresDecoys()
    {
        var baselinePeptides = TwinEntrapmentDatabaseBuilder.CollectBaselinePeptides(Baseline(), Trypsin);
        var withDecoy = Targets();
        withDecoy.Add(new Protein("KRAMLEVSTGYPDHFWNQIC", "DECOY_T3", isDecoy: true));

        var first = TwinEntrapmentDatabaseBuilder.GenerateTwin(Targets(), baselinePeptides, Trypsin);
        var second = TwinEntrapmentDatabaseBuilder.GenerateTwin(withDecoy, baselinePeptides, Trypsin);

        Assert.That(second.Select(p => (p.Accession, p.BaseSequence)), Is.EqualTo(first.Select(p => (p.Accession, p.BaseSequence))));
    }

    [Test]
    public void EnsureTwin_WritesALoadableFasta_AndReusesAnExistingFile()
    {
        string folder = Path.Combine(TestContext.CurrentContext.TestDirectory, $"twin_builder_{Path.GetRandomFileName()}");
        string path = Path.Combine(folder, "T_twin.fasta");
        try
        {
            var baselinePeptides = TwinEntrapmentDatabaseBuilder.CollectBaselinePeptides(Baseline(), Trypsin);
            int loads = 0;

            bool written = TwinEntrapmentDatabaseBuilder.EnsureTwin(path, () => { loads++; return Targets(); }, baselinePeptides, Trypsin);
            bool rewritten = TwinEntrapmentDatabaseBuilder.EnsureTwin(path, () => { loads++; return Targets(); }, baselinePeptides, Trypsin);

            Assert.That(written, Is.True);
            Assert.That(rewritten, Is.False);
            Assert.That(loads, Is.EqualTo(1), "an existing twin file must not be rebuilt");
            Assert.That(Directory.GetFiles(folder, "*.partial"), Is.Empty);

            var loaded = ProteinDbLoader.LoadProteinFasta(path, true, DecoyType.None, false, out var errors);
            var expected = TwinEntrapmentDatabaseBuilder.GenerateTwin(Targets(), baselinePeptides, Trypsin);
            Assert.That(errors, Is.Empty);
            Assert.That(loaded.Select(p => (p.Accession, p.BaseSequence)), Is.EquivalentTo(expected.Select(p => (p.Accession, p.BaseSequence))));
            Assert.That(loaded.All(p => p.IsEntrapment), Is.True, "the loader must recognise twin entries as entrapment");
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    [Test]
    public void EnsureTwins_BuildsBesideTheTarget_WritesTheSettings_AndRebuildsWhenTheTargetChanges()
    {
        string folder = Path.Combine(TestContext.CurrentContext.TestDirectory, $"twin_ensure_{Path.GetRandomFileName()}");
        Directory.CreateDirectory(folder);
        try
        {
            string targetPath = Path.Combine(folder, "T.fasta");
            File.WriteAllText(targetPath, ">sp|T1|T\nMAGLLVETSRPEKWYLQGNAIDHFTR\n");
            var settings = new TwinEntrapment.TwinSettings("abc123", "Seed: test");
            var twin = TwinEntrapment.GetTwinDatabases([new DbForTask(targetPath, false)], settings.Key, Path.Combine(folder, "out")).Single();
            int loads = 0;
            int Ensure() => TwinEntrapmentDatabaseBuilder.EnsureTwins([twin], settings, Baseline(), Trypsin,
                _ => { loads++; return Targets(); }, maxDegreeOfParallelism: 2);

            Assert.That(Ensure(), Is.EqualTo(1));
            Assert.That(File.Exists(twin.FilePath), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(Path.GetDirectoryName(twin.FilePath)!, TwinEntrapment.SettingsFileName)),
                Does.Contain("Seed: test"));

            Assert.That(Ensure(), Is.EqualTo(0), "a current twin is reused");

            // the target changes after its twin was built: the twin is stale and rebuilt
            File.SetLastWriteTimeUtc(targetPath, File.GetLastWriteTimeUtc(twin.FilePath).AddMinutes(1));
            Assert.That(TwinEntrapmentDatabaseBuilder.IsTwinCurrent(twin.FilePath, targetPath), Is.False);
            Assert.That(Ensure(), Is.EqualTo(1));
            Assert.That(loads, Is.EqualTo(2));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}

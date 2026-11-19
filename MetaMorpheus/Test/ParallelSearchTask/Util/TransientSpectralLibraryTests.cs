using System.IO;
using System.Linq;
using NUnit.Framework;
using Omics.Fragmentation;
using Omics.SpectralMatch.MslSpectralLibrary;
using Readers.SpectralLibrary;
using TaskLayer.ParallelSearch.Util;

namespace Test.ParallelSearchTask.Util;

[TestFixture]
public class TransientSpectralLibraryTests
{
    private static MslLibraryEntry Entry(string db, string sequence, float qValue, bool proteotypic = true) => new()
    {
        FullSequence = sequence,
        BaseSequence = sequence,
        PrecursorMz = 500.25,
        ChargeState = 2,
        RetentionTime = 12.5,
        RtIsCalibrated = true,
        ProteinAccession = db + TransientSpectralLibrary.AccessionSeparator + "P1",
        ProteinName = "protein",
        GeneName = db,
        IsProteotypic = proteotypic,
        QValue = qValue,
        Source = MslFormat.SourceType.Empirical,
        MatchedFragmentIons =
        [
            new MslFragmentIon { Mz = 300.1f, Intensity = 1f, ProductType = ProductType.b, FragmentNumber = 3, Charge = 1 },
            new MslFragmentIon { Mz = 400.2f, Intensity = 0.5f, ProductType = ProductType.y, FragmentNumber = 4, Charge = 1 },
        ]
    };

    private static string WriteDatabase(string outputFolder, string db, params MslLibraryEntry[] entries)
    {
        string folder = Path.Combine(outputFolder, db);
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, db + TransientSpectralLibrary.DatabaseFileSuffix);
        Assert.That(TransientSpectralLibrary.WriteDatabaseLibrary(path, entries), Is.True);
        return path;
    }

    private static string FreshFolder(string name)
    {
        string folder = Path.Combine(TestContext.CurrentContext.TestDirectory, name);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }

        Directory.CreateDirectory(folder);
        return folder;
    }

    [Test]
    public void WriteDatabaseLibrary_WithNoEntries_WritesNothing()
    {
        string folder = FreshFolder("TransientLibraryEmpty");
        try
        {
            string path = Path.Combine(folder, "empty" + TransientSpectralLibrary.DatabaseFileSuffix);
            Assert.That(TransientSpectralLibrary.WriteDatabaseLibrary(path, []), Is.False);
            Assert.That(File.Exists(path), Is.False);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Test]
    public void Combine_KeepsTheLowestQValuePerPeptideAndKeepsTheDatabaseName()
    {
        string folder = FreshFolder("TransientLibraryCombine");
        try
        {
            WriteDatabase(folder, "dbA", Entry("dbA", "PEPTIDEK", 0.008f), Entry("dbA", "ONLYINAK", 0.001f));
            WriteDatabase(folder, "dbB", Entry("dbB", "PEPTIDEK", 0.002f, proteotypic: false));
            string combinedPath = Path.Combine(folder, TransientSpectralLibrary.CombinedFileName);

            var result = TransientSpectralLibrary.Combine(folder, combinedPath);

            Assert.That(result, Is.Not.Null);
            using var combined = MslLibrary.Load(combinedPath);
            var entries = combined.GetAllEntries().ToDictionary(e => e.FullSequence);
            Assert.Multiple(() =>
            {
                Assert.That(entries, Has.Count.EqualTo(2));
                Assert.That(entries["PEPTIDEK"].GeneName, Is.EqualTo("dbB"));
                Assert.That(entries["PEPTIDEK"].ProteinAccession, Is.EqualTo("dbB|P1"));
                Assert.That(entries["PEPTIDEK"].IsProteotypic, Is.False);
                Assert.That(entries["PEPTIDEK"].QValue, Is.EqualTo(0.002f));
                Assert.That(entries["ONLYINAK"].GeneName, Is.EqualTo("dbA"));
                Assert.That(entries["ONLYINAK"].IsProteotypic, Is.True);
            });
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Test]
    public void Combine_WithMoreFilesThanOneBatch_GivesTheSameLibraryAndLeavesNoTempFiles()
    {
        string folder = FreshFolder("TransientLibraryBatches");
        try
        {
            for (int i = 0; i < 5; i++)
            {
                string db = $"db{i}";
                WriteDatabase(folder, db, Entry(db, "SHAREDPEPK", 0.01f - i * 0.001f), Entry(db, $"UNIQUE{i}PEPK", 0.005f));
            }

            string combinedPath = Path.Combine(folder, TransientSpectralLibrary.CombinedFileName);
            TransientSpectralLibrary.Combine(folder, combinedPath, filesPerBatch: 2);

            using var combined = MslLibrary.Load(combinedPath);
            var entries = combined.GetAllEntries().ToDictionary(e => e.FullSequence);
            Assert.Multiple(() =>
            {
                Assert.That(entries, Has.Count.EqualTo(6));
                Assert.That(entries["SHAREDPEPK"].GeneName, Is.EqualTo("db4"));
                Assert.That(Directory.GetFiles(folder, "*.tmp"), Is.Empty);
            });
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Test]
    public void Combine_WithNoLibraries_ReturnsNull()
    {
        string folder = FreshFolder("TransientLibraryNone");
        try
        {
            Assert.That(TransientSpectralLibrary.Combine(folder, Path.Combine(folder, "x.msl")), Is.Null);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}

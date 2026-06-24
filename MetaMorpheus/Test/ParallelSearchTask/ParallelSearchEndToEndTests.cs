using System.Collections.Generic;
using System.IO;
using System.Linq;
using EngineLayer.DatabaseLoading;
using NUnit.Framework;
using TaskLayer;
using PSTask = TaskLayer.ParallelSearch.ParallelSearchTask;

namespace Test.ParallelSearchTask;

/// <summary>
/// End-to-end smoke test that drives a real ParallelSearchTask once over small bundled data, so the
/// orchestration that the focused unit tests can't reach — base search, PEP training, the per-database
/// search + PEP assignment + confident-output writing, the statistics, and the family-aware writer — is
/// exercised. Base db = smalldb.fasta (the search that feeds PEP training); transient db = gapdh.fasta
/// (abundant yeast GAPDH, so the transient search produces hits).
/// </summary>
[TestFixture]
public class ParallelSearchEndToEndTests
{
    [Test]
    public void RunTask_SmallYeast_ProducesSummaryWithPepColumns()
    {
        string testData = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData");
        string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "ParallelSearchE2E");
        if (Directory.Exists(outputFolder))
            Directory.Delete(outputFolder, true);
        Directory.CreateDirectory(outputFolder);

        // Work on copies of the spectra file (the search writes file-specific artifacts next to it). Two copies
        // are pooled into the base search so it clears the >=100-PSM bar that gates PEP-model training.
        string src = Path.Combine(testData, "SmallCalibratible_Yeast.mzML");
        string mzml1 = Path.Combine(outputFolder, "Yeast_1.mzML");
        string mzml2 = Path.Combine(outputFolder, "Yeast_2.mzML");
        File.Copy(src, mzml1, true);
        File.Copy(src, mzml2, true);

        string baseDb = Path.Combine(testData, "smalldb.fasta");        // base (yeast) -> PEP training set
        string transientDb = Path.Combine(testData, "smalldb.fasta");   // transient db (same yeast proteins) -> guaranteed hits

        var task = new PSTask(new List<DbForTask> { new DbForTask(transientDb, false) });
        task.ParallelSearchParameters.MaxSearchesInParallel = 1;

        Assert.DoesNotThrow(() =>
            task.RunTask(outputFolder, new List<DbForTask> { new DbForTask(baseDb, false) }, new List<string> { mzml1, mzml2 }, "test"));

        // The cross-database summary must have been written, and must carry the new PEP 1%/5% columns.
        var summary = Directory.EnumerateFiles(outputFolder, "ManySearchSummary.csv", SearchOption.AllDirectories).FirstOrDefault();
        Assert.That(summary, Is.Not.Null, "ManySearchSummary.csv was not written");

        var lines = File.ReadAllLines(summary);
        var header = lines[0].Split(',');
        int Col(string name) => System.Array.IndexOf(header, name);
        Assert.Multiple(() =>
        {
            Assert.That(Col("TargetPeptidesFromTransientDbAtPepQ01"), Is.GreaterThanOrEqualTo(0));
            Assert.That(Col("TargetPeptidesFromTransientDbAtPepQ05"), Is.GreaterThanOrEqualTo(0));
            Assert.That(Col("TargetPsmsFromTransientDbAtPepQ01"), Is.GreaterThanOrEqualTo(0));
            Assert.That(Col("TargetPsmsFromTransientDbAtPepQ05"), Is.GreaterThanOrEqualTo(0));
        });

        var row = lines.Skip(1).First(l => l.Trim().Length > 0).Split(',');
        int transientPep = int.Parse(row[Col("TargetPeptidesFromTransientDb")]);
        int pepQ05 = int.Parse(row[Col("TargetPeptidesFromTransientDbAtPepQ05")]);

        // The transient search produced hits, and the base-trained PEP model assigned a confident PEP_QValue to
        // them (this is what fails if PEP training or the per-database PEP-assignment block stops running).
        Assert.That(transientPep, Is.GreaterThanOrEqualTo(1), "transient search should produce hits");
        Assert.That(pepQ05, Is.GreaterThanOrEqualTo(1), "PEP model should assign a confident PEP_QValue to transient peptides");
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using EngineLayer.DatabaseLoading;
using NUnit.Framework;
using TaskLayer;
using PSTask = TaskLayer.ParallelSearch.ParallelSearchTask;

namespace Test.ParallelSearchTask;

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

        string src = Path.Combine(testData, "SmallCalibratible_Yeast.mzML");
        string mzml1 = Path.Combine(outputFolder, "Yeast_1.mzML");
        string mzml2 = Path.Combine(outputFolder, "Yeast_2.mzML");
        File.Copy(src, mzml1, true);
        File.Copy(src, mzml2, true);

        string baseDb = Path.Combine(testData, "smalldb.fasta");
        string transientDb = Path.Combine(testData, "smalldb.fasta");

        var task = new PSTask(new List<DbForTask> { new DbForTask(transientDb, false) });
        task.ParallelSearchParameters.MaxSearchesInParallel = 1;

        Assert.DoesNotThrow(() =>
            task.RunTask(outputFolder, new List<DbForTask> { new DbForTask(baseDb, false) }, new List<string> { mzml1, mzml2 }, "test"));

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

        Assert.That(transientPep, Is.GreaterThanOrEqualTo(1), "transient search should produce hits");
        Assert.That(pepQ05, Is.GreaterThanOrEqualTo(1), "PEP model should assign a confident PEP_QValue to transient peptides");
    }
}

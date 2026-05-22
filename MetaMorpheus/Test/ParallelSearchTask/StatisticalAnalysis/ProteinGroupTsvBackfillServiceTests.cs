using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Analysis.Collectors;

namespace Test.ParallelSearchTask.StatisticalAnalysis;

[TestFixture]
public class ProteinGroupTsvBackfillServiceTests
{
    private string _tempDir = null!;

    [SetUp]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void Teardown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void BackfillIfNeeded_ParsesTsvAndPopulatesMetrics()
    {
        string dbName = "TestDb";
        string dbDir = Path.Combine(_tempDir, dbName);
        Directory.CreateDirectory(dbDir);

        string tsvContent = 
            "Protein Accession\tGene\tOrganism\tProtein Full Name\tProtein Unmodified Mass\t" +
            "Number of Proteins in Group\tUnique Peptides\tShared Peptides\t" +
            "Number of Peptides\tNumber of Unique Peptides\tSequence Coverage Fraction\t" +
            "Sequence Coverage\tSequence Coverage with Mods\tFragment Sequence Coverage\t" +
            "Modification Info List\tNumber of PSMs\tProtein Decoy/Contaminant/Target\t" +
            "Protein Cumulative Target\tProtein Cumulative Decoy\tProtein QValue\t" +
            "Best Peptide Score\tBest Peptide Notch QValue\tBest Peptide PEP\n" +
            "PROT1\t\tOrganism1\tProtein 1\t10000\t1\t\t\t3\t3\t\t\t\t\t\t10\tT\t1\t0\t0.01\t1.0\t0.01\t0\n" +
            "PROT2\t\tOrganism1\tProtein 2\t20000\t1\t\t\t5\t2\t\t\t\t\t\t20\tT\t2\t0\t0.01\t2.0\t0.01\t0\n" +
            "DECOY1\t\tOrganism1\tDecoy 1\t15000\t1\t\t\t1\t1\t\t\t\t\t\t2\tD\t0\t1\t-\t0.5\t-\t-\n" +
            "PROT3\t\tOrganism1\tProtein 3\t30000\t1\t\t\t7\t4\t\t\t\t\t\t30\tT\t3\t0\t0.01\t3.0\t0.01\t0";

        File.WriteAllText(Path.Combine(dbDir, $"{dbName}_AllProteinGroups.tsv"), tsvContent);

        var metric = new TransientDatabaseMetrics(dbName);

        var service = new ProteinGroupTsvBackfillService();
        service.BackfillIfNeeded(_tempDir, new() { metric });

        Assert.Multiple(() =>
        {
            Assert.That(metric.AllPeptidesPerProteinGroup, Is.EqualTo(new double[] { 3, 5, 7 }));
            Assert.That(metric.AllUniquePeptidesPerProteinGroup, Is.EqualTo(new double[] { 3, 2, 4 }));
            Assert.That(metric.AllPsmsPerProteinGroup, Is.EqualTo(new double[] { 10, 20, 30 }));
            Assert.That(metric.MedianPeptidesPerProteinGroup, Is.EqualTo(5.0));
            Assert.That(metric.MedianUniquePeptidesPerProteinGroup, Is.EqualTo(3.0));
            Assert.That(metric.MedianPsmsPerProteinGroup, Is.EqualTo(20));
        });
    }

    [Test]
    public void BackfillIfNeeded_SkipsWhenAlreadyPopulated()
    {
        string dbName = "AlreadyPopulated";
        string dbDir = Path.Combine(_tempDir, dbName);
        Directory.CreateDirectory(dbDir);

        // Write TSV even though it won't be read
        string tsvContent = "Protein Accession\tNumber of Peptides\tNumber of PSMs\tProtein Decoy/Contaminant/Target\nPROT\t5\t20\tT\n";
        File.WriteAllText(Path.Combine(dbDir, $"{dbName}_AllProteinGroups.tsv"), tsvContent);

        var metric = new TransientDatabaseMetrics(dbName)
        {
            AllPeptidesPerProteinGroup = new double[] { 42.0 },
            MedianPeptidesPerProteinGroup = 42.0
        };

        var service = new ProteinGroupTsvBackfillService();
        service.BackfillIfNeeded(_tempDir, new() { metric });

        Assert.That(metric.MedianPeptidesPerProteinGroup, Is.EqualTo(42.0),
            "Should NOT overwrite already-populated data");
    }

    [Test]
    public void BackfillIfNeeded_MissingDirectory_SkipsGracefully()
    {
        var metric = new TransientDatabaseMetrics("NonExistent");

        var service = new ProteinGroupTsvBackfillService();
        service.BackfillIfNeeded(_tempDir, new() { metric });

        Assert.That(metric.AllPeptidesPerProteinGroup, Is.Empty);
    }

    [Test]
    public void BackfillIfNeeded_MissingFile_SkipsGracefully()
    {
        string dbName = "NoTsvDb";
        Directory.CreateDirectory(Path.Combine(_tempDir, dbName));
        // No TSV file written

        var metric = new TransientDatabaseMetrics(dbName);

        var service = new ProteinGroupTsvBackfillService();
        service.BackfillIfNeeded(_tempDir, new() { metric });

        Assert.That(metric.AllPeptidesPerProteinGroup, Is.Empty);
    }

    [Test]
    public void BackfillIfNeeded_AllDecoys_ProducesEmptyArrays()
    {
        string dbName = "DecoyOnly";
        string dbDir = Path.Combine(_tempDir, dbName);
        Directory.CreateDirectory(dbDir);

        string tsvContent = 
            "Protein Accession\tNumber of Peptides\tNumber of PSMs\tProtein Decoy/Contaminant/Target\n" +
            "DECOY1\t5\t20\tD\n" +
            "DECOY2\t3\t10\tD\n" +
            "CON1\t2\t5\tC\n";

        File.WriteAllText(Path.Combine(dbDir, $"{dbName}_AllProteinGroups.tsv"), tsvContent);

        var metric = new TransientDatabaseMetrics(dbName);

        var service = new ProteinGroupTsvBackfillService();
        service.BackfillIfNeeded(_tempDir, new() { metric });

        Assert.Multiple(() =>
        {
            Assert.That(metric.AllPeptidesPerProteinGroup, Is.Empty);
            Assert.That(metric.AllUniquePeptidesPerProteinGroup, Is.Empty);
            Assert.That(metric.AllPsmsPerProteinGroup, Is.Empty);
            Assert.That(metric.MedianPeptidesPerProteinGroup, Is.EqualTo(0.0));
            Assert.That(metric.MedianPsmsPerProteinGroup, Is.EqualTo(0));
        });
    }

    [Test]
    public void BackfillIfNeeded_EmptyTsv_ProducesEmptyArrays()
    {
        string dbName = "EmptyTsv";
        string dbDir = Path.Combine(_tempDir, dbName);
        Directory.CreateDirectory(dbDir);

        // Only header, no data
        string tsvContent = "Protein Accession\tNumber of Peptides\tNumber of PSMs\tProtein Decoy/Contaminant/Target\n";
        File.WriteAllText(Path.Combine(dbDir, $"{dbName}_AllProteinGroups.tsv"), tsvContent);

        var metric = new TransientDatabaseMetrics(dbName);

        var service = new ProteinGroupTsvBackfillService();
        service.BackfillIfNeeded(_tempDir, new() { metric });

        Assert.That(metric.AllPeptidesPerProteinGroup, Is.Empty);
    }

    [Test]
    public void BackfillIfNeeded_MissingUniquePeptideColumn_DoesNotFail()
    {
        string dbName = "NoUniqueCol";
        string dbDir = Path.Combine(_tempDir, dbName);
        Directory.CreateDirectory(dbDir);

        // TSV without unique peptide column
        string tsvContent = 
            "Protein Accession\tNumber of Peptides\tNumber of PSMs\tProtein Decoy/Contaminant/Target\n" +
            "PROT1\t5\t20\tT\n";

        File.WriteAllText(Path.Combine(dbDir, $"{dbName}_AllProteinGroups.tsv"), tsvContent);

        var metric = new TransientDatabaseMetrics(dbName);

        var service = new ProteinGroupTsvBackfillService();
        service.BackfillIfNeeded(_tempDir, new() { metric });

        Assert.Multiple(() =>
        {
            Assert.That(metric.AllPeptidesPerProteinGroup, Is.EqualTo(new double[] { 5 }));
            Assert.That(metric.AllUniquePeptidesPerProteinGroup, Is.Empty);
            Assert.That(metric.AllPsmsPerProteinGroup, Is.EqualTo(new double[] { 20 }));
        });
    }

    [Test]
    public void BackfillIfNeeded_WithRealData_ParsesCorrectly()
    {
        string dbName = "RealDataDb";
        string dbDir = Path.Combine(_tempDir, dbName);
        Directory.CreateDirectory(dbDir);

        // Simulated real protein group data
        string tsvContent = 
            "Protein Accession\tNumber of Peptides\tNumber of Unique Peptides\tNumber of PSMs\tProtein Decoy/Contaminant/Target\n" +
            "PROT1\t4\t4\t14\tT\n" +
            "PROT2\t4\t1\t5\tT\n";

        File.WriteAllText(Path.Combine(dbDir, $"{dbName}_AllProteinGroups.tsv"), tsvContent);

        var metric = new TransientDatabaseMetrics(dbName);

        var service = new ProteinGroupTsvBackfillService();
        service.BackfillIfNeeded(_tempDir, new() { metric });

        Assert.Multiple(() =>
        {
            Assert.That(metric.AllPeptidesPerProteinGroup, Is.EqualTo(new double[] { 4, 4 }));
            Assert.That(metric.MedianPeptidesPerProteinGroup, Is.EqualTo(4.0));
            Assert.That(metric.AllUniquePeptidesPerProteinGroup, Is.EqualTo(new double[] { 4, 1 }));
            Assert.That(metric.MedianUniquePeptidesPerProteinGroup, Is.EqualTo(2.5));
            Assert.That(metric.AllPsmsPerProteinGroup, Is.EqualTo(new double[] { 14, 5 }));
            Assert.That(metric.MedianPsmsPerProteinGroup, Is.EqualTo(10));
        });
    }

    [Test]
    public void BackfillIfNeeded_NullOutputFolder_DoesNotThrow()
    {
        var metric = new TransientDatabaseMetrics("Db1");

        var service = new ProteinGroupTsvBackfillService();
        Assert.DoesNotThrow(() => service.BackfillIfNeeded(null!, new() { metric }));
    }

    [Test]
    public void BackfillIfNeeded_NullMetricsList_DoesNotThrow()
    {
        var service = new ProteinGroupTsvBackfillService();
        Assert.DoesNotThrow(() => service.BackfillIfNeeded(_tempDir, null!));
    }

    [Test]
    public void BackfillIfNeeded_EmptyMetricsList_DoesNotThrow()
    {
        var service = new ProteinGroupTsvBackfillService();
        Assert.DoesNotThrow(() => service.BackfillIfNeeded(_tempDir, new()));
    }
}

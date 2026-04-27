using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EngineLayer;
using EngineLayer.DatabaseLoading;
using EngineLayer.ParallelSearch;
using EngineLayer.ParallelSearch.PersistentCache;
using MassSpectrometry;
using NUnit.Framework;
using Omics.BioPolymer;
using Omics.Digestion;
using Omics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using UsefulProteomicsDatabases;

namespace Test.ParallelSearchTask.PersistentCache;

[TestFixture]
public class TransientDatabaseLoadingEngineTests
{
    private string _tempDir = null!;
    private string _fastaPath = null!;
    private TransientCacheStorageLayout _storageLayout = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _fastaPath = Path.Combine(_tempDir, "test.fasta");
        File.WriteAllText(_fastaPath, ">P1|TEST_PROTEIN\nPEPTIDEKPEPTIDER\n>P2|TEST_PROTEIN2\nMKRGISVQR\n");

        string cacheRoot = Path.Combine(_tempDir, "Cache");
        _storageLayout = TransientCacheStorageLayout.Create(cacheRoot);
        _storageLayout.EnsureDirectoriesExist();
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch
        {
            // ignore cleanup failures
        }
    }

    [Test]
    public void Load_WithCacheEnabled_PopulatesCacheAndReturnsProteins()
    {
        var engine = CreateEngine(useCache: true);
        var results = engine.Run() as DatabaseLoadingEngineResults;

        Assert.That(results, Is.Not.Null);
        Assert.That(results!.BioPolymers.Count, Is.GreaterThan(0));

        // Verify cache manifest was created
        Assert.That(File.Exists(_storageLayout.ManifestPath), Is.True);
    }

    [Test]
    public void Load_WithCacheEnabled_SecondRunHitsCacheAndReturnsSameProteins()
    {
        var engine1 = CreateEngine(useCache: true);
        var results1 = engine1.Run() as DatabaseLoadingEngineResults;
        Assert.That(results1, Is.Not.Null);
        int firstCount = results1!.BioPolymers.Count;

        var engine2 = CreateEngine(useCache: true);
        var results2 = engine2.Run() as DatabaseLoadingEngineResults;
        Assert.That(results2, Is.Not.Null);
        Assert.That(results2!.BioPolymers.Count, Is.EqualTo(firstCount));

        // Verify sequences are preserved
        var firstSequences = results1.BioPolymers.Select(p => p.BaseSequence).OrderBy(s => s).ToList();
        var secondSequences = results2.BioPolymers.Select(p => p.BaseSequence).OrderBy(s => s).ToList();
        Assert.That(secondSequences, Is.EqualTo(firstSequences));
    }

    [Test]
    public void Load_WithCacheDisabled_DoesNotPopulateCache()
    {
        var engine = CreateEngine(useCache: false);
        var results = engine.Run() as DatabaseLoadingEngineResults;

        Assert.That(results, Is.Not.Null);
        Assert.That(results!.BioPolymers.Count, Is.GreaterThan(0));

        // Manifest should not exist because cache is disabled
        Assert.That(File.Exists(_storageLayout.ManifestPath), Is.False);
    }

    private TransientDatabaseLoadingEngine CreateEngine(bool useCache)
    {
        var digestionParams = new DigestionParams(
            protease: "trypsin",
            maxMissedCleavages: 2,
            minPeptideLength: 5,
            searchModeType: CleavageSpecificity.Full,
            fragmentationTerminus: FragmentationTerminus.Both);

        var commonParams = new CommonParameters(
            dissociationType: DissociationType.HCD,
            digestionParams: digestionParams);

        var dbList = new List<DbForTask>
        {
            new DbForTask(_fastaPath, isContaminant: false)
        };

        return new TransientDatabaseLoadingEngine(
            commonParams,
            new List<(string, CommonParameters)>(),
            new List<string>(),
            dbList,
            taskId: "TestTask",
            decoyType: DecoyType.None,
            generateTargets: true,
            localizableMods: new List<string>(),
            tcAmbiguity: TargetContaminantAmbiguity.RemoveContaminant,
            writeTargetDecoyFasta: false,
            outputFolder: _tempDir,
            useCache: useCache,
            storageLayout: _storageLayout);
    }
}

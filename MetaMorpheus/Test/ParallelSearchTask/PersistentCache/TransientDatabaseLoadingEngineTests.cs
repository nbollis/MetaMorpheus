using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EngineLayer;
using EngineLayer.DatabaseLoading;
using EngineLayer.ParallelSearch;
using EngineLayer.ParallelSearch.PersistentCache;
using EngineLayer.ParallelSearch.PersistentCache.Manifest;
using EngineLayer.ParallelSearch.PersistentCache.Payloads;
using MassSpectrometry;
using NUnit.Framework;
using Omics;
using Omics.BioPolymer;
using Omics.Digestion;
using Omics.Fragmentation;
using Omics.Modifications;
using Proteomics.ProteolyticDigestion;
using System.Text;
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
        var engine = new DatabaseLoadingEngine(
            CreateCommonParameters(),
            new List<(string, CommonParameters)>(),
            new List<string>(),
            new List<DbForTask> { new DbForTask(_fastaPath, isContaminant: false) },
            taskId: "TestTask",
            decoyType: DecoyType.None,
            generateTargets: true,
            localizableMods: new List<string>(),
            tcAmbiguity: TargetContaminantAmbiguity.RemoveContaminant,
            writeTargetDecoyFasta: false,
            outputFolder: _tempDir);
        var results = engine.Run() as DatabaseLoadingEngineResults;

        Assert.That(results, Is.Not.Null);
        Assert.That(results!.BioPolymers.Count, Is.GreaterThan(0));

        // Manifest should not exist because cache is disabled
        Assert.That(File.Exists(_storageLayout.ManifestPath), Is.False);
    }

    [TestCase(@"TestData\smalldb.fasta")]
    [TestCase(@"TestData\gapdh.fasta")]
    [TestCase(@"TestData\semiTest.fasta")]
    public void Load_CachedParity_MatchesNormalLoad(string relativeDbPath)
    {
        string dbPath = Path.Combine(TestContext.CurrentContext.TestDirectory, relativeDbPath);
        Assert.That(File.Exists(dbPath), Is.True, $"Test database not found: {dbPath}");

        var digestionParams = new DigestionParams(
            protease: "trypsin",
            maxMissedCleavages: 2,
            minPeptideLength: 5,
            searchModeType: CleavageSpecificity.Full,
            fragmentationTerminus: FragmentationTerminus.Both);

        var commonParams = new CommonParameters(
            dissociationType: DissociationType.HCD,
            digestionParams: digestionParams,
            listOfModsFixed: new List<(string, string)>(),
            listOfModsVariable: new List<(string, string)>());

        var dbList = new List<DbForTask> { new DbForTask(dbPath, isContaminant: false) };

        // 1. Normal load
        var normalEngine = new DatabaseLoadingEngine(
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
            outputFolder: _tempDir);
        var normalResults = normalEngine.Run() as DatabaseLoadingEngineResults;
        Assert.That(normalResults, Is.Not.Null);
        var normalProteins = normalResults!.BioPolymers;

        var sharedCache = CreateCache(commonParams, _storageLayout);

        // 2. First cached load (miss -> build)
        var cachedEngine1 = new TransientDatabaseLoadingEngine(
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
            cache: sharedCache);
        var cachedResults1 = cachedEngine1.Run() as DatabaseLoadingEngineResults;
        Assert.That(cachedResults1, Is.Not.Null);
        var cachedProteins1 = cachedResults1!.BioPolymers;

        // 3. Second cached load (hit -> use)
        var cachedEngine2 = new TransientDatabaseLoadingEngine(
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
            cache: sharedCache);
        var cachedResults2 = cachedEngine2.Run() as DatabaseLoadingEngineResults;
        Assert.That(cachedResults2, Is.Not.Null);
        var cachedProteins2 = cachedResults2!.BioPolymers;

        // Compare proteins
        CompareProteinSets(normalProteins, cachedProteins1, "normal vs cached miss");
        CompareProteinSets(normalProteins, cachedProteins2, "normal vs cached hit");

        // Compare peptides and fragments
        var normalPeptides = DigestAll(normalProteins, digestionParams);
        var cachedPeptides1 = DigestAll(cachedProteins1, digestionParams);
        var cachedPeptides2 = DigestAll(cachedProteins2, digestionParams);

        ComparePeptideSets(normalPeptides, cachedPeptides1, "normal vs cached miss");
        ComparePeptideSets(normalPeptides, cachedPeptides2, "normal vs cached hit");

        CompareFragmentSets(normalPeptides, cachedPeptides1, commonParams.DissociationType, digestionParams.FragmentationTerminus, "normal vs cached miss");
        CompareFragmentSets(normalPeptides, cachedPeptides2, commonParams.DissociationType, digestionParams.FragmentationTerminus, "normal vs cached hit");
    }

    private static void CompareProteinSets(List<IBioPolymer> expected, List<IBioPolymer> actual, string context)
    {
        var expectedSeqs = expected.Select(p => p.BaseSequence).OrderBy(s => s).ToList();
        var actualSeqs = actual.Select(p => p.BaseSequence).OrderBy(s => s).ToList();
        Assert.That(actualSeqs, Is.EqualTo(expectedSeqs), $"Protein base sequences mismatch ({context})");

        var expectedAccessions = expected.Select(p => p.Accession).OrderBy(s => s).ToList();
        var actualAccessions = actual.Select(p => p.Accession).OrderBy(s => s).ToList();
        Assert.That(actualAccessions, Is.EqualTo(expectedAccessions), $"Protein accessions mismatch ({context})");
    }

    private static List<IBioPolymerWithSetMods> DigestAll(List<IBioPolymer> proteins, DigestionParams digestionParams)
    {
        return proteins
            .SelectMany(p => p.Digest(digestionParams, new List<Modification>(), new List<Modification>()))
            .OrderBy(p => p.FullSequence)
            .ThenBy(p => p.OneBasedStartResidue)
            .ThenBy(p => p.Parent?.Accession ?? string.Empty)
            .ToList();
    }

    private static void ComparePeptideSets(List<IBioPolymerWithSetMods> expected, List<IBioPolymerWithSetMods> actual, string context)
    {
        Assert.That(actual.Count, Is.EqualTo(expected.Count), $"Peptide count mismatch ({context})");
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.That(actual[i].FullSequence, Is.EqualTo(expected[i].FullSequence), $"Peptide full sequence mismatch at index {i} ({context})");
            Assert.That(actual[i].OneBasedStartResidue, Is.EqualTo(expected[i].OneBasedStartResidue), $"Peptide start residue mismatch at index {i} ({context})");
            Assert.That(actual[i].OneBasedEndResidue, Is.EqualTo(expected[i].OneBasedEndResidue), $"Peptide end residue mismatch at index {i} ({context})");
            Assert.That(actual[i].MissedCleavages, Is.EqualTo(expected[i].MissedCleavages), $"Peptide missed cleavages mismatch at index {i} ({context})");
            Assert.That(actual[i].Parent?.Accession, Is.EqualTo(expected[i].Parent?.Accession), $"Peptide parent accession mismatch at index {i} ({context})");
        }
    }

    private static void CompareFragmentSets(List<IBioPolymerWithSetMods> expectedPeptides, List<IBioPolymerWithSetMods> actualPeptides, DissociationType dissociationType, FragmentationTerminus terminus, string context)
    {
        Assert.That(actualPeptides.Count, Is.EqualTo(expectedPeptides.Count), $"Fragment comparison peptide count mismatch ({context})");
        for (int i = 0; i < expectedPeptides.Count; i++)
        {
            var expectedProducts = new List<Product>();
            expectedPeptides[i].Fragment(dissociationType, terminus, expectedProducts);
            expectedProducts = expectedProducts.OrderBy(p => p.ProductType).ThenBy(p => p.FragmentNumber).ThenBy(p => p.NeutralMass).ToList();

            var actualProducts = new List<Product>();
            actualPeptides[i].Fragment(dissociationType, terminus, actualProducts);
            actualProducts = actualProducts.OrderBy(p => p.ProductType).ThenBy(p => p.FragmentNumber).ThenBy(p => p.NeutralMass).ToList();

            Assert.That(actualProducts.Count, Is.EqualTo(expectedProducts.Count), $"Product count mismatch for peptide {expectedPeptides[i].FullSequence} ({context})");
            for (int j = 0; j < expectedProducts.Count; j++)
            {
                Assert.That(actualProducts[j].ProductType, Is.EqualTo(expectedProducts[j].ProductType), $"Product type mismatch at peptide {i}, product {j} ({context})");
                Assert.That(actualProducts[j].Terminus, Is.EqualTo(expectedProducts[j].Terminus), $"Product terminus mismatch at peptide {i}, product {j} ({context})");
                Assert.That(actualProducts[j].NeutralMass, Is.EqualTo(expectedProducts[j].NeutralMass).Within(1e-6), $"Product neutral mass mismatch at peptide {i}, product {j} ({context})");
                Assert.That(actualProducts[j].FragmentNumber, Is.EqualTo(expectedProducts[j].FragmentNumber), $"Product fragment number mismatch at peptide {i}, product {j} ({context})");
                Assert.That(actualProducts[j].ResiduePosition, Is.EqualTo(expectedProducts[j].ResiduePosition), $"Product residue position mismatch at peptide {i}, product {j} ({context})");
                Assert.That(actualProducts[j].NeutralLoss, Is.EqualTo(expectedProducts[j].NeutralLoss).Within(1e-6), $"Product neutral loss mismatch at peptide {i}, product {j} ({context})");
            }
        }
    }

    [Test]
    public void Load_CacheCorrupt_FallsBackToBaseAndRepairs()
    {
        // First run: populate cache
        var engine1 = CreateEngine(useCache: true);
        var results1 = engine1.Run() as DatabaseLoadingEngineResults;
        Assert.That(results1, Is.Not.Null);

        // Corrupt the occurrence segment file that hydrate must read up front.
        string occurrenceDirectory = Path.Combine(_storageLayout.PayloadDirectory, "occurrence");
        var segmentFiles = Directory.GetFiles(occurrenceDirectory, "*.bin", SearchOption.AllDirectories);
        Assert.That(segmentFiles.Length, Is.GreaterThan(0), "Expected at least one occurrence segment file");
        File.WriteAllText(segmentFiles[0], "CORRUPTED");

        // Second run: should fall back to base behavior
        var engine2 = CreateEngine(useCache: true);
        var results2 = engine2.Run() as DatabaseLoadingEngineResults;
        Assert.That(results2, Is.Not.Null);
        Assert.That(results2!.BioPolymers.Count, Is.EqualTo(results1!.BioPolymers.Count));

        // Telemetry should record a corrupt entry and fallback
        Assert.That(engine2.Telemetry.CorruptEntries, Is.GreaterThanOrEqualTo(1));
        Assert.That(engine2.Telemetry.Fallbacks, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void Load_CacheSettingsMismatch_FallsBackToBase()
    {
        // First run: populate cache with specific settings
        var engine1 = CreateEngine(useCache: true);
        var results1 = engine1.Run() as DatabaseLoadingEngineResults;
        Assert.That(results1, Is.Not.Null);

        // Second run: different dissociation type -> different CacheSettingsId
        var digestionParams = new DigestionParams(
            protease: "trypsin",
            maxMissedCleavages: 2,
            minPeptideLength: 5,
            searchModeType: CleavageSpecificity.Full,
            fragmentationTerminus: FragmentationTerminus.Both);

        var differentCommonParams = new CommonParameters(
            dissociationType: DissociationType.CID,
            digestionParams: digestionParams);

        var dbList = new List<DbForTask>
        {
            new DbForTask(_fastaPath, isContaminant: false)
        };

        var sharedCache = CreateCache(differentCommonParams, _storageLayout);

        var engine2 = new TransientDatabaseLoadingEngine(
            differentCommonParams,
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
            cache: sharedCache);

        var results2 = engine2.Run() as DatabaseLoadingEngineResults;
        Assert.That(results2, Is.Not.Null);
        Assert.That(results2!.BioPolymers.Count, Is.EqualTo(results1!.BioPolymers.Count));

        // Telemetry: miss (not settings mismatch in this case because the entry simply doesn't exist)
        Assert.That(engine2.Telemetry.CacheMisses, Is.EqualTo(1));
    }

    [Test]
    public void Load_CacheSurvivesProcessRestart()
    {
        // Simulate first process: populate cache
        var engine1 = CreateEngine(useCache: true);
        var results1 = engine1.Run() as DatabaseLoadingEngineResults;
        Assert.That(results1, Is.Not.Null);
        int firstCount = results1!.BioPolymers.Count;

        // Simulate process restart: new engine instance, same storage layout
        var engine2 = CreateEngine(useCache: true);
        var results2 = engine2.Run() as DatabaseLoadingEngineResults;
        Assert.That(results2, Is.Not.Null);
        Assert.That(results2!.BioPolymers.Count, Is.EqualTo(firstCount));

        // Telemetry should record a hit
        Assert.That(engine2.Telemetry.CacheHits, Is.EqualTo(1));
    }

    [Test]
    public void Load_HydratedParentIdentity_MatchesOriginalProteins()
    {
        // Cache miss then hit
        var missEngine = CreateEngine(useCache: true);
        var missResults = missEngine.Run() as DatabaseLoadingEngineResults;
        Assert.That(missResults, Is.Not.Null);

        var hitEngine = CreateEngine(useCache: true);
        var hitResults = hitEngine.Run() as DatabaseLoadingEngineResults;
        Assert.That(hitResults, Is.Not.Null);
        var cachedProteins = hitResults!.BioPolymers;

        Assert.That(cachedProteins.Count, Is.GreaterThan(0));

        var digestionParams = hitEngine.CommonParameters.DigestionParams;
        var emptyMods = new List<Modification>();

        foreach (var cachedProtein in cachedProteins)
        {
            var cachedPeptides = cachedProtein.Digest(digestionParams, emptyMods, emptyMods).ToList();
            Assert.That(cachedPeptides.Count, Is.GreaterThan(0),
                $"Expected at least one peptide for protein {cachedProtein.Accession}");

            foreach (var cachedPeptide in cachedPeptides)
            {
                // Parent reference identity: cached peptide's parent must be the cached protein
                Assert.That(cachedPeptide.Parent, Is.SameAs(cachedProtein),
                    $"Parent identity mismatch for peptide {cachedPeptide.FullSequence} of protein {cachedProtein.Accession}");
            }
        }
    }

    [Test]
    public void Load_CacheHit_DefersFragmentReadsUntilFragmentAccess()
    {
        var missEngine = CreateEngine(useCache: true);
        var missResults = missEngine.Run() as DatabaseLoadingEngineResults;
        Assert.That(missResults, Is.Not.Null);

        string fragmentDirectory = Path.Combine(_storageLayout.PayloadDirectory, "fragment");
        string[] fragmentSegmentFiles = Directory.GetFiles(fragmentDirectory, "*.bin", SearchOption.AllDirectories);
        Assert.That(fragmentSegmentFiles.Length, Is.GreaterThan(0), "Expected fragment segment files after cache publish.");

        foreach (string fragmentSegmentFile in fragmentSegmentFiles)
        {
            File.Delete(fragmentSegmentFile);
        }

        var hitEngine = CreateEngine(useCache: true);
        var hitResults = hitEngine.Run() as DatabaseLoadingEngineResults;
        Assert.That(hitResults, Is.Not.Null);
        Assert.That(hitEngine.Telemetry.CacheHits, Is.EqualTo(1), "Cache hit should succeed before any fragment bytes are requested.");

        var cachedProtein = hitResults!.BioPolymers.First();
        var cachedPeptide = cachedProtein.Digest(
                hitEngine.CommonParameters.DigestionParams,
                new List<Modification>(),
                new List<Modification>())
            .First();

        var products = new List<Product>();
        Assert.That(
            () => cachedPeptide.Fragment(hitEngine.CommonParameters.DissociationType, hitEngine.CommonParameters.DigestionParams.FragmentationTerminus, products),
            Throws.InstanceOf<IOException>());
    }

    [Test]
    public void Load_LazyFragmentFailure_QuarantinesSharedSequences()
    {
        var missEngine = CreateEngine(useCache: true);
        var missResults = missEngine.Run() as DatabaseLoadingEngineResults;
        Assert.That(missResults, Is.Not.Null);

        string fragmentDirectory = Path.Combine(_storageLayout.PayloadDirectory, "fragment");
        string[] fragmentSegmentFiles = Directory.GetFiles(fragmentDirectory, "*.bin", SearchOption.AllDirectories);
        Assert.That(fragmentSegmentFiles.Length, Is.GreaterThan(0), "Expected fragment segment files after cache publish.");
        File.WriteAllText(fragmentSegmentFiles[0], "CORRUPTED");

        var hitEngine = CreateEngine(useCache: true);
        var hitResults = hitEngine.Run() as DatabaseLoadingEngineResults;
        Assert.That(hitResults, Is.Not.Null);
        Assert.That(hitEngine.Telemetry.CacheHits, Is.EqualTo(1));

        var cachedPeptide = hitResults!.BioPolymers.First()
            .Digest(hitEngine.CommonParameters.DigestionParams, new List<Modification>(), new List<Modification>())
            .First();

        var products = new List<Product>();
        Assert.That(
            () => cachedPeptide.Fragment(hitEngine.CommonParameters.DissociationType, hitEngine.CommonParameters.DigestionParams.FragmentationTerminus, products),
            Throws.Exception);

        var manifestStore = new TransientCacheManifestStore(_storageLayout.ManifestPath);
        var cacheKey = CreateCacheKey(_fastaPath, hitEngine.CommonParameters);
        var resolvedSequences = manifestStore.GetResolvedEntrySequenceReferences(cacheKey);

        Assert.That(resolvedSequences.Any(r => r.IsQuarantined), Is.True);
        Assert.That(hitEngine.Telemetry.QuarantinedSharedSequenceCount, Is.GreaterThan(0));
    }

    [Test]
    public void Load_QuarantinedSharedSequences_FallBackAndRebuild()
    {
        var missEngine = CreateEngine(useCache: true);
        var missResults = missEngine.Run() as DatabaseLoadingEngineResults;
        Assert.That(missResults, Is.Not.Null);

        string fragmentDirectory = Path.Combine(_storageLayout.PayloadDirectory, "fragment");
        string[] fragmentSegmentFiles = Directory.GetFiles(fragmentDirectory, "*.bin", SearchOption.AllDirectories);
        Assert.That(fragmentSegmentFiles.Length, Is.GreaterThan(0), "Expected fragment segment files after cache publish.");
        File.WriteAllText(fragmentSegmentFiles[0], "CORRUPTED");

        var quarantineEngine = CreateEngine(useCache: true);
        var quarantineResults = quarantineEngine.Run() as DatabaseLoadingEngineResults;
        Assert.That(quarantineResults, Is.Not.Null);

        var cachedPeptide = quarantineResults!.BioPolymers.First()
            .Digest(quarantineEngine.CommonParameters.DigestionParams, new List<Modification>(), new List<Modification>())
            .First();

        var products = new List<Product>();
        Assert.That(
            () => cachedPeptide.Fragment(quarantineEngine.CommonParameters.DissociationType, quarantineEngine.CommonParameters.DigestionParams.FragmentationTerminus, products),
            Throws.Exception);

        var rebuildEngine = CreateEngine(useCache: true);
        var rebuildResults = rebuildEngine.Run() as DatabaseLoadingEngineResults;
        Assert.That(rebuildResults, Is.Not.Null);
        Assert.That(rebuildResults!.BioPolymers.Count, Is.EqualTo(missResults!.BioPolymers.Count));
        Assert.That(rebuildEngine.Telemetry.CorruptEntries, Is.GreaterThanOrEqualTo(1));
        Assert.That(rebuildEngine.Telemetry.Fallbacks, Is.GreaterThanOrEqualTo(1));
        Assert.That(rebuildEngine.Telemetry.CacheMisses, Is.EqualTo(1));

        var manifestStore = new TransientCacheManifestStore(_storageLayout.ManifestPath);
        var cacheKey = CreateCacheKey(_fastaPath, rebuildEngine.CommonParameters);
        var resolvedSequences = manifestStore.GetResolvedEntrySequenceReferences(cacheKey);
        Assert.That(resolvedSequences.All(r => !r.IsQuarantined), Is.True);
        Assert.That(resolvedSequences.All(r => r.FragmentShardId.HasValue), Is.True);
    }

    [Test]
    public void Load_Performance_CachedLoadIsRepeatableAndFast()
    {
        // Warm up: populate cache
        var warmupEngine = CreateEngine(useCache: true);
        warmupEngine.Run();

        // Measure multiple cached loads
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 5; i++)
        {
            var cachedEngine = CreateEngine(useCache: true);
            var results = cachedEngine.Run() as DatabaseLoadingEngineResults;
            Assert.That(results, Is.Not.Null);
            Assert.That(results!.BioPolymers.Count, Is.GreaterThan(0));
        }
        sw.Stop();

        // All cached loads should complete in reasonable time (< 1 second total for 5 tiny DB loads)
        Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(1)),
            $"5 cached loads took {sw.Elapsed.TotalMilliseconds}ms, expected under 1000ms");
    }

    [Test]
    public void Load_Telemetry_RecordsMetrics()
    {
        var engine = CreateEngine(useCache: true);
        engine.Run();

        var metrics = engine.Telemetry.ToMetrics();

        Assert.That(metrics.ContainsKey("CacheHits"), Is.True);
        Assert.That(metrics.ContainsKey("CacheMisses"), Is.True);
        Assert.That(metrics.ContainsKey("Fallbacks"), Is.True);
        Assert.That(metrics.ContainsKey("ReusedFragmentShardCount"), Is.True);
        Assert.That(metrics.ContainsKey("QuarantinedSharedSequenceCount"), Is.True);
        Assert.That(metrics.ContainsKey("PublishedSharedSequenceCount"), Is.True);
        Assert.That(metrics.ContainsKey("PayloadBytesWritten"), Is.True);
        Assert.That(metrics.ContainsKey("OccurrencePayloadBytesWritten"), Is.True);
        Assert.That(metrics.ContainsKey("FragmentPayloadBytesWritten"), Is.True);
        Assert.That(metrics.ContainsKey("TotalHydrateTimeMs"), Is.True);
        Assert.That(metrics.ContainsKey("TotalFallbackTimeMs"), Is.True);
        Assert.That(metrics.ContainsKey("TotalPublishTimeMs"), Is.True);

        // First run is a miss
        Assert.That(engine.Telemetry.CacheMisses, Is.EqualTo(1));
        Assert.That(engine.Telemetry.CacheHits, Is.EqualTo(0));
        Assert.That(engine.Telemetry.PayloadBytesWritten, Is.GreaterThan(0));
        Assert.That(engine.Telemetry.OccurrencePayloadBytesWritten, Is.GreaterThan(0));
        Assert.That(engine.Telemetry.FragmentPayloadBytesWritten, Is.GreaterThan(0));
        Assert.That(engine.Telemetry.PublishedSharedSequenceCount, Is.GreaterThan(0));
        Assert.That(engine.Telemetry.PayloadBytesWritten, Is.EqualTo(engine.Telemetry.OccurrencePayloadBytesWritten + engine.Telemetry.FragmentPayloadBytesWritten));
        Assert.That(engine.Telemetry.TotalFallbackTime, Is.GreaterThan(TimeSpan.Zero));
        Assert.That(engine.Telemetry.TotalPublishTime, Is.GreaterThan(TimeSpan.Zero));
    }

    [Test]
    public void Load_Telemetry_HitRecordsNoFallback()
    {
        // Populate cache
        var missEngine = CreateEngine(useCache: true);
        missEngine.Run();

        // Hit cache
        var hitEngine = CreateEngine(useCache: true);
        hitEngine.Run();

        Assert.That(hitEngine.Telemetry.CacheHits, Is.EqualTo(1));
        Assert.That(hitEngine.Telemetry.CacheMisses, Is.EqualTo(0));
        Assert.That(hitEngine.Telemetry.Fallbacks, Is.EqualTo(0));
        Assert.That(hitEngine.Telemetry.ReusedFragmentShardCount, Is.EqualTo(0));
        Assert.That(hitEngine.Telemetry.PublishedSharedSequenceCount, Is.EqualTo(0));
        Assert.That(hitEngine.Telemetry.PayloadBytesWritten, Is.EqualTo(0));
        Assert.That(hitEngine.Telemetry.OccurrencePayloadBytesWritten, Is.EqualTo(0));
        Assert.That(hitEngine.Telemetry.FragmentPayloadBytesWritten, Is.EqualTo(0));
    }

    [Test]
    public void ManifestStore_GrowthSummary_AfterPublish()
    {
        var engine = CreateEngine(useCache: true);
        engine.Run();

        var manifestStore = new TransientCacheManifestStore(_storageLayout.ManifestPath);
        var summary = manifestStore.GetCacheGrowthSummary();

        Assert.That(summary.EntryCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(summary.PublishedEntryCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(summary.SharedSequenceCount, Is.GreaterThan(0));
        Assert.That(summary.QuarantinedSharedSequenceCount, Is.GreaterThanOrEqualTo(0));
        Assert.That(summary.OccurrenceSegmentCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(summary.FragmentSegmentCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(summary.OccurrenceShardCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(summary.FragmentShardCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(summary.OccurrencePayloadBytes, Is.GreaterThan(0));
        Assert.That(summary.FragmentPayloadBytes, Is.GreaterThan(0));
        Assert.That(summary.TotalPayloadBytes, Is.EqualTo(summary.OccurrencePayloadBytes + summary.FragmentPayloadBytes));
    }

    [Test]
    public void Load_Publish_PopulatesSharedSequenceCatalogMappings()
    {
        var engine = CreateEngine(useCache: true);
        var results = engine.Run() as DatabaseLoadingEngineResults;
        Assert.That(results, Is.Not.Null);

        var cacheKey = CreateCacheKey(_fastaPath, engine.CommonParameters);
        var manifestStore = new TransientCacheManifestStore(_storageLayout.ManifestPath);
        var resolvedShards = manifestStore.GetResolvedEntryShardReferences(cacheKey);
        var resolvedSequences = manifestStore.GetResolvedEntrySequenceReferences(cacheKey);

        var expectedFullSequences = DigestAll(results!.BioPolymers, engine.CommonParameters)
            .Select(p => p.FullSequence)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(resolvedShards, Has.Count.EqualTo(1));
            Assert.That(resolvedShards[0].PayloadKind, Is.EqualTo(TransientCachePayloadKind.Occurrence));
            Assert.That(resolvedShards[0].RelativePath, Does.StartWith($"occurrence{Path.DirectorySeparatorChar}"));
            Assert.That(resolvedSequences, Has.Count.EqualTo(expectedFullSequences.Count));
            Assert.That(resolvedSequences.Select(r => r.LocalOrdinal), Is.EqualTo(Enumerable.Range(0, expectedFullSequences.Count)));
            Assert.That(resolvedSequences.Select(r => r.FullSequence).OrderBy(s => s), Is.EqualTo(expectedFullSequences));
            Assert.That(resolvedSequences.All(r => r.SequenceHash == ComputeSharedSequenceHash(r.FullSequence)), Is.True);
            Assert.That(resolvedSequences.All(r => r.FragmentShardId.HasValue), Is.True);
        });
    }

    [Test]
    public void Load_Publish_ReusesFragmentShardsAcrossDifferentDatabases()
    {
        string secondFastaPath = Path.Combine(_tempDir, "test2.fasta");
        File.WriteAllText(secondFastaPath, ">Q1|ALT_PROTEIN\nPEPTIDEKPEPTIDER\n>Q2|ALT_PROTEIN2\nMPEPTIDERK\n");

        var firstEngine = CreateEngine(_fastaPath, useCache: true);
        var firstResults = firstEngine.Run() as DatabaseLoadingEngineResults;
        Assert.That(firstResults, Is.Not.Null);

        var secondEngine = CreateEngine(secondFastaPath, useCache: true);
        var secondResults = secondEngine.Run() as DatabaseLoadingEngineResults;
        Assert.That(secondResults, Is.Not.Null);
        Assert.That(secondEngine.Telemetry.ReusedFragmentShardCount, Is.GreaterThan(0));
        Assert.That(secondEngine.Telemetry.OccurrencePayloadBytesWritten, Is.GreaterThan(0));
        Assert.That(secondEngine.Telemetry.PayloadBytesWritten, Is.EqualTo(secondEngine.Telemetry.OccurrencePayloadBytesWritten + secondEngine.Telemetry.FragmentPayloadBytesWritten));

        var manifestStore = new TransientCacheManifestStore(_storageLayout.ManifestPath);
        var firstKey = CreateCacheKey(_fastaPath, firstEngine.CommonParameters);
        var secondKey = CreateCacheKey(secondFastaPath, secondEngine.CommonParameters);
        var firstResolved = manifestStore.GetResolvedEntrySequenceReferences(firstKey);
        var secondResolved = manifestStore.GetResolvedEntrySequenceReferences(secondKey);

        var overlappingSequences = firstResolved
            .Select(r => r.FullSequence)
            .Intersect(secondResolved.Select(r => r.FullSequence))
            .OrderBy(s => s)
            .ToList();

        Assert.That(overlappingSequences, Is.Not.Empty, "Expected at least one shared full sequence across the two DB entries.");

        foreach (string fullSequence in overlappingSequences)
        {
            var firstSequence = firstResolved.Single(r => r.FullSequence == fullSequence);
            var secondSequence = secondResolved.Single(r => r.FullSequence == fullSequence);

            Assert.That(secondSequence.SequenceId, Is.EqualTo(firstSequence.SequenceId), $"Shared sequence catalog should reuse the same record for {fullSequence}.");
            Assert.That(secondSequence.FragmentShardId, Is.EqualTo(firstSequence.FragmentShardId), $"Shared fragment shard should be reused for {fullSequence}.");

            var sharedFragmentShard = manifestStore.GetPayloadShard(firstSequence.FragmentShardId!.Value);
            Assert.That(sharedFragmentShard, Is.Not.Null);
            Assert.That(sharedFragmentShard!.Value.ReferenceCount, Is.GreaterThanOrEqualTo(2), $"Fragment shard refcount should grow when {fullSequence} is reused across DB entries.");
        }
    }

    [Test]
    public void Load_SchemaScopedLayoutChange_RebuildsCacheInNewRoot()
    {
        var commonParameters = CreateCommonParameters();
        var schemaV1Layout = TransientCacheStorageLayout.Create(Path.Combine(_tempDir, "Cache", "v1"));
        var schemaV2Layout = TransientCacheStorageLayout.Create(Path.Combine(_tempDir, "Cache", "v2"));
        schemaV1Layout.EnsureDirectoriesExist();
        schemaV2Layout.EnsureDirectoriesExist();

        var originalEngine = CreateEngine(_fastaPath, useCache: true, commonParameters, schemaV1Layout);
        var originalResults = originalEngine.Run() as DatabaseLoadingEngineResults;
        Assert.That(originalResults, Is.Not.Null);
        Assert.That(originalEngine.Telemetry.CacheMisses, Is.EqualTo(1));

        var rebuiltEngine = CreateEngine(_fastaPath, useCache: true, commonParameters, schemaV2Layout);
        var rebuiltResults = rebuiltEngine.Run() as DatabaseLoadingEngineResults;

        Assert.Multiple(() =>
        {
            Assert.That(rebuiltResults, Is.Not.Null);
            Assert.That(rebuiltEngine.Telemetry.CacheHits, Is.EqualTo(0));
            Assert.That(rebuiltEngine.Telemetry.CacheMisses, Is.EqualTo(1));
            Assert.That(File.Exists(schemaV1Layout.ManifestPath), Is.True);
            Assert.That(File.Exists(schemaV2Layout.ManifestPath), Is.True);
        });
    }

    [Test]
    public void Load_Publish_SeparatesSharedSequencesByCacheSettings()
    {
        var hcdParameters = CreateCommonParameters(DissociationType.HCD);
        var cidParameters = CreateCommonParameters(DissociationType.CID);

        var hcdEngine = CreateEngine(_fastaPath, useCache: true, hcdParameters);
        var hcdResults = hcdEngine.Run() as DatabaseLoadingEngineResults;
        Assert.That(hcdResults, Is.Not.Null);

        var cidEngine = CreateEngine(_fastaPath, useCache: true, cidParameters);
        var cidResults = cidEngine.Run() as DatabaseLoadingEngineResults;
        Assert.That(cidResults, Is.Not.Null);

        var manifestStore = new TransientCacheManifestStore(_storageLayout.ManifestPath);
        var hcdKey = CreateCacheKey(_fastaPath, hcdParameters);
        var cidKey = CreateCacheKey(_fastaPath, cidParameters);
        var hcdSequences = manifestStore.GetResolvedEntrySequenceReferences(hcdKey);
        var cidSequences = manifestStore.GetResolvedEntrySequenceReferences(cidKey);
        var overlappingSequences = hcdSequences.Select(s => s.FullSequence).Intersect(cidSequences.Select(s => s.FullSequence)).ToList();

        Assert.That(overlappingSequences, Is.Not.Empty);

        foreach (string fullSequence in overlappingSequences)
        {
            var hcdSequence = hcdSequences.Single(s => s.FullSequence == fullSequence);
            var cidSequence = cidSequences.Single(s => s.FullSequence == fullSequence);

            Assert.Multiple(() =>
            {
                Assert.That(hcdSequence.SequenceId, Is.Not.EqualTo(cidSequence.SequenceId), $"Shared sequence '{fullSequence}' should be settings-scoped.");
                Assert.That(manifestStore.TryGetSharedSequence(hcdKey.CacheSettingsId, ComputeSharedSequenceHash(fullSequence), fullSequence), Is.Not.Null);
                Assert.That(manifestStore.TryGetSharedSequence(cidKey.CacheSettingsId, ComputeSharedSequenceHash(fullSequence), fullSequence), Is.Not.Null);
            });
        }
    }

    [Test]
    public void Load_Publish_PreservesDbLocalOrdinalRoundTripAcrossEntries()
    {
        string secondFastaPath = Path.Combine(_tempDir, "ordinal-test.fasta");
        File.WriteAllText(secondFastaPath, ">Q1|ORDINAL_SHIFT\nAAAAAKPEPTIDEK\n>Q2|ORDINAL_SHIFT_2\nMPEPTIDER\n");

        var firstEngine = CreateEngine(_fastaPath, useCache: true);
        var firstResults = firstEngine.Run() as DatabaseLoadingEngineResults;
        Assert.That(firstResults, Is.Not.Null);

        var secondEngine = CreateEngine(secondFastaPath, useCache: true);
        var secondResults = secondEngine.Run() as DatabaseLoadingEngineResults;
        Assert.That(secondResults, Is.Not.Null);

        var manifestStore = new TransientCacheManifestStore(_storageLayout.ManifestPath);
        var firstKey = CreateCacheKey(_fastaPath, firstEngine.CommonParameters);
        var secondKey = CreateCacheKey(secondFastaPath, secondEngine.CommonParameters);
        var firstResolved = manifestStore.GetResolvedEntrySequenceReferences(firstKey).OrderBy(r => r.LocalOrdinal).ToList();
        var secondResolved = manifestStore.GetResolvedEntrySequenceReferences(secondKey).OrderBy(r => r.LocalOrdinal).ToList();
        var expectedFirstOrder = GetEncounterOrderedUniqueFullSequences(firstResults!.BioPolymers, firstEngine.CommonParameters);
        var expectedSecondOrder = GetEncounterOrderedUniqueFullSequences(secondResults!.BioPolymers, secondEngine.CommonParameters);

        Assert.Multiple(() =>
        {
            Assert.That(firstResolved.Select(r => r.FullSequence), Is.EqualTo(expectedFirstOrder));
            Assert.That(secondResolved.Select(r => r.FullSequence), Is.EqualTo(expectedSecondOrder));
            Assert.That(firstResolved.Select(r => r.LocalOrdinal), Is.EqualTo(Enumerable.Range(0, firstResolved.Count)));
            Assert.That(secondResolved.Select(r => r.LocalOrdinal), Is.EqualTo(Enumerable.Range(0, secondResolved.Count)));
        });

        const string sharedFullSequence = "PEPTIDEK";
        var firstShared = firstResolved.Single(r => r.FullSequence == sharedFullSequence);
        var secondShared = secondResolved.Single(r => r.FullSequence == sharedFullSequence);

        Assert.Multiple(() =>
        {
            Assert.That(firstShared.SequenceId, Is.EqualTo(secondShared.SequenceId));
            Assert.That(firstShared.LocalOrdinal, Is.Not.EqualTo(secondShared.LocalOrdinal), "Local ordinals should remain entry-local even when shared sequence identity is reused.");
        });
    }

    [Test]
    public void Load_Publish_UsesSharedSegmentsInsteadOfPerEntryFiles()
    {
        string secondFastaPath = Path.Combine(_tempDir, "segments-test-2.fasta");
        string thirdFastaPath = Path.Combine(_tempDir, "segments-test-3.fasta");
        File.WriteAllText(secondFastaPath, ">Q1|SEGMENT_TWO\nAAAAAKPEPTIDEK\n");
        File.WriteAllText(thirdFastaPath, ">Q1|SEGMENT_THREE\nPEPTIDERAAAAAK\n");

        var commonParameters = CreateCommonParameters();
        CreateEngine(_fastaPath, useCache: true, commonParameters).Run();
        CreateEngine(secondFastaPath, useCache: true, commonParameters).Run();
        CreateEngine(thirdFastaPath, useCache: true, commonParameters).Run();

        var payloadFiles = Directory.GetFiles(_storageLayout.PayloadDirectory, "*.bin", SearchOption.AllDirectories);
        var manifestStore = new TransientCacheManifestStore(_storageLayout.ManifestPath);
        var summary = manifestStore.GetCacheGrowthSummary();

        Assert.Multiple(() =>
        {
            Assert.That(summary.EntryCount, Is.EqualTo(3));
            Assert.That(summary.OccurrenceSegmentCount, Is.EqualTo(1));
            Assert.That(summary.FragmentSegmentCount, Is.EqualTo(1));
            Assert.That(payloadFiles.Length, Is.EqualTo(2), "V2 should accumulate payload shards into shared occurrence/fragment segment files instead of one file per entry.");
            Assert.That(summary.OccurrenceShardCount, Is.GreaterThan(1));
            Assert.That(summary.FragmentShardCount, Is.GreaterThan(1));
        });
    }

    [Test]
    public void SharedCache_Dispose_PreventsFurtherUse()
    {
        var commonParameters = CreateCommonParameters();
        var cache = CreateCache(commonParameters, _storageLayout);

        cache.Dispose();

        Assert.Multiple(() =>
        {
            Assert.Throws<ObjectDisposedException>(() => cache.Prewarm(new[] { _fastaPath }));
            Assert.Throws<ObjectDisposedException>(() => cache.Resolve(_fastaPath));
            Assert.Throws<ObjectDisposedException>(() => cache.GetGrowthSummary());
        });
    }

    private TransientDatabaseLoadingEngine CreateEngine(bool useCache)
    {
        return CreateEngine(_fastaPath, useCache, CreateCommonParameters());
    }

    private TransientDatabaseLoadingEngine CreateEngine(string fastaPath, bool useCache, CommonParameters? commonParameters = null, TransientCacheStorageLayout? storageLayout = null)
    {
        if (!useCache)
        {
            throw new ArgumentException("TransientDatabaseLoadingEngineTests should use DatabaseLoadingEngine directly for uncached paths.", nameof(useCache));
        }

        var dbList = new List<DbForTask>
        {
            new DbForTask(fastaPath, isContaminant: false)
        };

        var cache = CreateCache(commonParameters ?? CreateCommonParameters(), storageLayout ?? _storageLayout);

        return new TransientDatabaseLoadingEngine(
            commonParameters ?? CreateCommonParameters(),
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
            cache: cache);
    }

    private static TransientDatabaseCache CreateCache(CommonParameters commonParameters, TransientCacheStorageLayout storageLayout)
    {
        return new TransientDatabaseCache(
            commonParameters,
            DecoyType.None,
            true,
            new List<string>(),
            TargetContaminantAmbiguity.RemoveContaminant,
            storageLayout);
    }

    private static CommonParameters CreateCommonParameters(DissociationType dissociationType = DissociationType.HCD)
    {
        var digestionParams = new DigestionParams(
            protease: "trypsin",
            maxMissedCleavages: 2,
            minPeptideLength: 5,
            searchModeType: CleavageSpecificity.Full,
            fragmentationTerminus: FragmentationTerminus.Both);

        return new CommonParameters(
            dissociationType: dissociationType,
            digestionParams: digestionParams);
    }

    private static TransientCacheKey CreateCacheKey(string fastaPath, CommonParameters commonParameters)
    {
        var settingsDescriptor = TransientCacheSettingsDescriptor.Create(
            commonParameters,
            DecoyType.None,
            generateTargets: true,
            localizableModificationTypes: new List<string>(),
            targetContaminantAmbiguity: TargetContaminantAmbiguity.RemoveContaminant);

        string databaseContentHash = TransientCacheHashing.ComputeDatabaseContentHash(fastaPath);
        return new TransientCacheKey(databaseContentHash, settingsDescriptor.CacheSettingsId);
    }

    private static List<IBioPolymerWithSetMods> DigestAll(List<IBioPolymer> proteins, CommonParameters commonParameters)
    {
        var fixedMods = ResolveSelectedMods(commonParameters.ListOfModsFixed);
        var variableMods = ResolveSelectedMods(commonParameters.ListOfModsVariable);

        return proteins
            .SelectMany(p => p.Digest(commonParameters.DigestionParams, fixedMods, variableMods))
            .OrderBy(p => p.FullSequence)
            .ThenBy(p => p.OneBasedStartResidue)
            .ThenBy(p => p.Parent?.Accession ?? string.Empty)
            .ToList();
    }

    private static List<string> GetEncounterOrderedUniqueFullSequences(List<IBioPolymer> proteins, CommonParameters commonParameters)
    {
        var fixedMods = ResolveSelectedMods(commonParameters.ListOfModsFixed);
        var variableMods = ResolveSelectedMods(commonParameters.ListOfModsVariable);
        var uniqueSequences = new HashSet<string>();
        var orderedSequences = new List<string>();

        foreach (var peptide in proteins.SelectMany(p => p.Digest(commonParameters.DigestionParams, fixedMods, variableMods)))
        {
            if (uniqueSequences.Add(peptide.FullSequence))
            {
                orderedSequences.Add(peptide.FullSequence);
            }
        }

        return orderedSequences;
    }

    private static List<Modification> ResolveSelectedMods(IEnumerable<(string, string)> selectedMods)
    {
        return GlobalVariables.AllModsKnown.Where(m =>
            selectedMods.Any(selected => selected.Item1 == m.ModificationType && selected.Item2 == m.IdWithMotif)).ToList();
    }

    private static string ComputeSharedSequenceHash(string fullSequence)
    {
        return TransientCacheHashing.ComputeSha256Hex(Encoding.UTF8.GetBytes(fullSequence));
    }
}

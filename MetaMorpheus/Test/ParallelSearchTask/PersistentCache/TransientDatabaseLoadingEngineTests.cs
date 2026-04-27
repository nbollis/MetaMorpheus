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
using Omics;
using Omics.BioPolymer;
using Omics.Digestion;
using Omics.Fragmentation;
using Omics.Modifications;
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
            useCache: true,
            storageLayout: _storageLayout);
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
            useCache: true,
            storageLayout: _storageLayout);
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

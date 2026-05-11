using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EngineLayer;
using EngineLayer.DatabaseLoading;
using NUnit.Framework;
using Omics;
using Omics.BioPolymer;
using Omics.Digestion;
using Omics.Modifications;
using TaskLayer;
using Transcriptomics;
using Transcriptomics.Digestion;
using UsefulProteomicsDatabases;
using UsefulProteomicsDatabases.Transcriptomics;
using Test.Mocks;

namespace Test.Transcriptomics;

[TestFixture]
public class RnaDatabaseLoadingTests
{
    [Test]
    [TestCase("20mer1.fasta")]
    [TestCase("20mer1.fasta.gz")]
    [TestCase("20mer1.xml")]
    [TestCase("20mer1.xml.gz")]
    public static void TestDbReadingDifferentExtensions(string databaseFileName)
    {
        GlobalVariables.AnalyteType = AnalyteType.Oligo;
        var dbPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Transcriptomics", "TestData", databaseFileName);
        var dbForTask = new List<DbForTask> { new DbForTask(dbPath, false) };
        var commonParameters = new CommonParameters(digestionParams: new RnaDigestionParams());

        var loader = new DatabaseLoadingEngine(commonParameters, [], [], dbForTask, "TestTaskId", DecoyType.None);
        var results = (DatabaseLoadingEngineResults)loader.Run()!;
        var bioPolymers = results.BioPolymers;

        Assert.That(dbForTask[0].BioPolymerCount, Is.EqualTo(1));   
        Assert.That(dbForTask[0].TargetCount, Is.EqualTo(1));
        Assert.That(dbForTask[0].DecoyCount, Is.EqualTo(0));

        Assert.That(bioPolymers![0], Is.TypeOf<RNA>());
        Assert.That(bioPolymers.Count, Is.EqualTo(1));
        Assert.That(bioPolymers.First().BaseSequence, Is.EqualTo("GUACUGCCUCUAGUGAAGCA"));
    }

    [Test]
    [TestCase(TargetContaminantAmbiguity.RenameProtein, new[] { "20mer1_D1", "20mer1_D2" }, new[] {false, true})]
    [TestCase(TargetContaminantAmbiguity.RemoveTarget, new[] { "20mer1" }, new[] { true })]
    [TestCase(TargetContaminantAmbiguity.RemoveContaminant, new[] { "20mer1" }, new[] { false })]
    public static void DbReadingHandleAccessionCollisions(TargetContaminantAmbiguity type, string[] expectedAccessions, bool[] expectedIsContaminant)
    {
        GlobalVariables.AnalyteType = AnalyteType.Oligo;
        var task = new SearchTask();
        var commonParameters = new CommonParameters(digestionParams: new RnaDigestionParams());
        string baseSequence = "GUACUGCCUCUAGUGAAGCA";

        // Create two instances of the same database, one as a contaminant, one as a target. 
        var dbPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Transcriptomics", "TestData", "20mer1.fasta");
        var dbsForTask = new List<DbForTask> { new DbForTask(dbPath, false), new DbForTask(dbPath, true) };
        var loader = new DatabaseLoadingEngine(commonParameters, [], [], dbsForTask, "TestTaskId", DecoyType.None, tcAmbiguity: type);
        var results = (DatabaseLoadingEngineResults)loader.Run()!;
        var bioPolymers = results.BioPolymers;

        Assert.That(bioPolymers.Count, Is.EqualTo(expectedAccessions.Length));
        Assert.That(bioPolymers[0].Accession, Is.EqualTo(expectedAccessions[0]));
        Assert.That(bioPolymers[0].IsContaminant, Is.EqualTo(expectedIsContaminant[0]));

        for (int i = 0; i < bioPolymers.Count; i++)
        {
            Assert.That(bioPolymers[i].BaseSequence, Is.EqualTo(baseSequence));
            Assert.That(bioPolymers[i].Accession, Is.EqualTo(expectedAccessions[i]));
            Assert.That(bioPolymers[i].IsContaminant, Is.EqualTo(expectedIsContaminant[i]));
        }
    }

    [Test]
    public static void DatabaseSanitization_ThrowsWhenNewBioPolymerIntroduced()
    {
        List<IBioPolymer> notImplementedBioPolymers = new()
        {
            new TestBioPolymer() {Accession= "Testing"},
            new TestBioPolymer() {Accession= "Testing"},
            new TestBioPolymer() {Accession= "TestingA"},
        };

        var exception = Assert.Throws<ArgumentException>(() =>
        {
            DatabaseLoadingEngine.SanitizeBioPolymerDatabase(notImplementedBioPolymers, TargetContaminantAmbiguity.RenameProtein, out _);
        });

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception.Message, Does.Contain("Database sanitization assumed BioPolymer was a protein when it was Test.Mocks.TestBioPolymer"));
    }

    [Test]
    public void TwoTruncationsAndSequenceVariant_DbLoading()
    {
        GlobalVariables.AnalyteType = AnalyteType.Oligo;
        var task = new SearchTask();
        var commonParameters = new CommonParameters(digestionParams: new RnaDigestionParams("RNase T1"));
        string dbPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Transcriptomics", "TestData", "TruncationAndVariantMods.xml");
        var dbsForTask = new List<DbForTask> { new DbForTask(dbPath, false) };

        // Use reflection to access the protected LoadModifications method
        var loadModificationsMethod = typeof(MetaMorpheusTask).GetMethod("LoadModifications", BindingFlags.NonPublic | BindingFlags.Instance);
        object[] modArgs = new object[]
        {
            "TestTaskId",
            null, // variableModifications (out)
            null, // fixedModifications (out)
            null  // localizableModificationTypes (out)
        };
        loadModificationsMethod.Invoke(task, modArgs);
        var localizableModificationTypes = (List<string>)modArgs[3];

        var loader = new DatabaseLoadingEngine(commonParameters, [], [], dbsForTask, "TestTaskId", DecoyType.Reverse, true, localizableModificationTypes);
        var results = (DatabaseLoadingEngineResults)loader.Run()!;
        var rna = results.BioPolymers;

        Assert.That(rna.All(p => p.SequenceVariations.Count == 1));
        Assert.That(rna.All(p => p.OriginalNonVariantModifications.Count == 2));

        List<IBioPolymer> targets = rna.Where(p => p.IsDecoy == false).ToList();
        IBioPolymer variantTarget = targets.First(p => p.AppliedSequenceVariations.Count >= 1);
        IBioPolymer nonVariantTarget = targets.First(p => p.AppliedSequenceVariations.Count == 0);

        Assert.That(variantTarget.OneBasedPossibleLocalizedModifications.Count, Is.EqualTo(1));
        Assert.That(nonVariantTarget.OneBasedPossibleLocalizedModifications.Count, Is.EqualTo(2));

        List<IBioPolymer> decoys = rna.Where(p => p.IsDecoy).ToList();
        IBioPolymer variantDecoy = decoys.First(p => p.AppliedSequenceVariations.Count >= 1);
        IBioPolymer nonVariantDecoy = decoys.First(p => p.AppliedSequenceVariations.Count == 0);

        Assert.That(variantDecoy.OneBasedPossibleLocalizedModifications.Count, Is.EqualTo(1));
        Assert.That(nonVariantDecoy.OneBasedPossibleLocalizedModifications.Count, Is.EqualTo(2));
    }
}

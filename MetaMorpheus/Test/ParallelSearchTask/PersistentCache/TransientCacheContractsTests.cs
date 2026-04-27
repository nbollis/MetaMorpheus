using System;
using System.Collections.Generic;
using System.IO;
using EngineLayer;
using EngineLayer.Indexing;
using EngineLayer.ParallelSearch.PersistentCache;
using MassSpectrometry;
using NUnit.Framework;
using Omics.Digestion;
using Omics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using UsefulProteomicsDatabases;

namespace Test.ParallelSearchTask.PersistentCache;

[TestFixture]
public class TransientCacheContractsTests
{
    [Test]
    public void DatabaseContentHash_DependsOnFileContentNotPath()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        string firstPath = Path.Combine(tempDirectory, "alpha.fasta");
        string secondPath = Path.Combine(tempDirectory, "beta.fasta");

        try
        {
            File.WriteAllText(firstPath, ">P1\nPEPTIDE\n");
            File.WriteAllText(secondPath, ">P1\nPEPTIDE\n");

            string firstHash = TransientCacheHashing.ComputeDatabaseContentHash(firstPath);
            string secondHash = TransientCacheHashing.ComputeDatabaseContentHash(secondPath);

            File.WriteAllText(secondPath, ">P2\nPEPTIDER\n");
            string updatedHash = TransientCacheHashing.ComputeDatabaseContentHash(secondPath);

            Assert.Multiple(() =>
            {
                Assert.That(firstHash, Is.EqualTo(secondHash));
                Assert.That(updatedHash, Is.Not.EqualTo(firstHash));
            });
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Test]
    public void CacheSettingsId_IsStableWhenInputCollectionsAreReordered()
    {
        CommonParameters firstParameters = CreateCommonParameters(
            fixedMods: [
                ("Common Fixed", "Carbamidomethyl on C"),
                ("Common Fixed", "Carbamidomethyl on U")],
            variableMods: [
                ("Common Variable", "Oxidation on M"),
                ("Common Variable", "Phosphorylation on S")]);

        CommonParameters secondParameters = CreateCommonParameters(
            fixedMods: [
                ("Common Fixed", "Carbamidomethyl on U"),
                ("Common Fixed", "Carbamidomethyl on C")],
            variableMods: [
                ("Common Variable", "Phosphorylation on S"),
                ("Common Variable", "Oxidation on M")]);

        var firstDescriptor = TransientCacheSettingsDescriptor.Create(
            firstParameters,
            DecoyType.Reverse,
            generateTargets: true,
            localizableModificationTypes: ["Common Biological", "Metal"],
            TargetContaminantAmbiguity.RemoveContaminant);

        var secondDescriptor = TransientCacheSettingsDescriptor.Create(
            secondParameters,
            DecoyType.Reverse,
            generateTargets: true,
            localizableModificationTypes: ["Metal", "Common Biological"],
            TargetContaminantAmbiguity.RemoveContaminant);

        Assert.That(firstDescriptor.CacheSettingsId, Is.EqualTo(secondDescriptor.CacheSettingsId));
    }

    [Test]
    public void CacheSettingsId_ChangesWhenRelevantSettingsChange()
    {
        CommonParameters commonParameters = CreateCommonParameters();

        var baselineDescriptor = TransientCacheSettingsDescriptor.Create(
            commonParameters,
            DecoyType.Reverse,
            generateTargets: true,
            localizableModificationTypes: ["Common Biological"],
            TargetContaminantAmbiguity.RemoveContaminant);

        var changedDecoyDescriptor = TransientCacheSettingsDescriptor.Create(
            commonParameters,
            DecoyType.None,
            generateTargets: true,
            localizableModificationTypes: ["Common Biological"],
            TargetContaminantAmbiguity.RemoveContaminant);

        CommonParameters changedDissociationParameters = CreateCommonParameters(dissociationType: DissociationType.ETD);
        var changedDissociationDescriptor = TransientCacheSettingsDescriptor.Create(
            changedDissociationParameters,
            DecoyType.Reverse,
            generateTargets: true,
            localizableModificationTypes: ["Common Biological"],
            TargetContaminantAmbiguity.RemoveContaminant);

        Assert.Multiple(() =>
        {
            Assert.That(changedDecoyDescriptor.CacheSettingsId, Is.Not.EqualTo(baselineDescriptor.CacheSettingsId));
            Assert.That(changedDissociationDescriptor.CacheSettingsId, Is.Not.EqualTo(baselineDescriptor.CacheSettingsId));
        });
    }

    [Test]
    public void LookupMessages_UseCachePrefixAndFallbackWording()
    {
        string message = TransientCacheMessages.FormatLookupMessage(
            TransientCacheLookupOutcome.Corrupt,
            @"C:\temp\transient-db.fasta",
            "checksum mismatch");

        Assert.Multiple(() =>
        {
            Assert.That(message, Does.StartWith("[TransientCache]"));
            Assert.That(message, Does.Contain("transient-db.fasta"));
            Assert.That(message, Does.Not.Contain(@"C:\temp\transient-db.fasta"));
            Assert.That(message, Does.Contain("Falling back to base behavior."));
            Assert.That(message, Does.Contain("checksum mismatch"));
            Assert.That(TransientCacheMessages.ShouldWarn(TransientCacheLookupOutcome.Corrupt), Is.True);
            Assert.That(TransientCacheMessages.ShouldWarn(TransientCacheLookupOutcome.Hit), Is.False);
        });
    }

    private static CommonParameters CreateCommonParameters(
        DissociationType dissociationType = DissociationType.HCD,
        IEnumerable<(string, string)> fixedMods = null,
        IEnumerable<(string, string)> variableMods = null)
    {
        DigestionParams digestionParams = new(
            protease: "trypsin",
            maxMissedCleavages: 2,
            minPeptideLength: 5,
            searchModeType: CleavageSpecificity.Semi,
            fragmentationTerminus: FragmentationTerminus.N);

        return new CommonParameters(
            dissociationType: dissociationType,
            digestionParams: digestionParams,
            listOfModsFixed: fixedMods,
            listOfModsVariable: variableMods,
            addTruncations: true);
    }
}

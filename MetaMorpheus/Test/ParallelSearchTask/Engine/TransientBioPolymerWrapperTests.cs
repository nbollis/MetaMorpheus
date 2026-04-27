using System;
using System.Collections.Generic;
using System.Linq;
using EngineLayer.ParallelSearch;
using MassSpectrometry;
using NUnit.Framework;
using Omics;
using Omics.BioPolymer;
using Omics.Digestion;
using Omics.Fragmentation;
using Omics.Modifications;
using Proteomics;
using Proteomics.ProteolyticDigestion;

namespace Test.ParallelSearchTask.Engine;

[TestFixture]
public class TransientBioPolymerWrapperTests
{
    [Test]
    public void Digest_UsesFactoryOnceAndAssignsTransientParent()
    {
        Protein rawProtein = new("PEPTIDEKPEPTIDEK", "P1");
        PeptideWithSetModifications rawPeptide1 = CreatePeptide(rawProtein, 1, "PEPTIDEK");
        PeptideWithSetModifications rawPeptide2 = CreatePeptide(rawProtein, 9, "PEPTIDEK");
        int factoryCalls = 0;

        TransientBioPolymer wrapper = new(
            rawProtein,
            peptideCount: 2,
            digestionProductFactory: _ =>
            {
                factoryCalls++;
                return [rawPeptide1, rawPeptide2];
            });

        List<IBioPolymerWithSetMods> firstDigest = wrapper.Digest(CreateDigestionParams(), new List<Modification>(), new List<Modification>()).ToList();
        List<IBioPolymerWithSetMods> secondDigest = wrapper.Digest(CreateDigestionParams(), new List<Modification>(), new List<Modification>()).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(wrapper.PeptideCount, Is.EqualTo(2));
            Assert.That(factoryCalls, Is.EqualTo(1));
            Assert.That(firstDigest.Count, Is.EqualTo(2));
            Assert.That(ReferenceEquals(((TransientBioPolymerWithSetMods)firstDigest[0]).Parent, wrapper), Is.True);
            Assert.That(ReferenceEquals(firstDigest[0], secondDigest[0]), Is.True);
        });
    }

    [Test]
    public void Digest_ThrowsWhenFactoryReturnsWrapperBoundToDifferentParent()
    {
        Protein rawProtein = new("PEPTIDEKPEPTIDEK", "P1");
        TransientBioPolymer expectedParent = new(rawProtein);
        TransientBioPolymer wrongParent = new(rawProtein);
        PeptideWithSetModifications rawPeptide = CreatePeptide(rawProtein, 1, "PEPTIDEK");

        TransientBioPolymer wrapper = new(
            rawProtein,
            peptideCount: 1,
            digestionProductFactory: _ =>
            [
                new TransientBioPolymerWithSetMods(rawPeptide, wrongParent)
            ]);

        Assert.That(
            () => wrapper.Digest(CreateDigestionParams(), new List<Modification>(), new List<Modification>()),
            Throws.TypeOf<InvalidOperationException>());
        Assert.That(expectedParent.DigestionProducts, Is.Null);
    }

    [Test]
    public void CloneWithNewSequenceAndMods_ReturnsFreshWrapperWithoutDigestCache()
    {
        Protein rawProtein = new("PEPTIDEKPEPTIDEK", "P1");
        PeptideWithSetModifications rawPeptide = CreatePeptide(rawProtein, 1, "PEPTIDEK");
        TransientBioPolymer wrapper = new(rawProtein, peptideCount: 1, digestionProducts: [rawPeptide]);

        TransientBioPolymer clonedWrapper = (TransientBioPolymer)wrapper.CloneWithNewSequenceAndMods("PEPTIDER", null);

        Assert.Multiple(() =>
        {
            Assert.That(clonedWrapper, Is.Not.SameAs(wrapper));
            Assert.That(clonedWrapper.BaseSequence, Is.EqualTo("PEPTIDER"));
            Assert.That(clonedWrapper.DigestionProducts, Is.Null);
            Assert.That(clonedWrapper.PeptideCount, Is.Null);
        });
    }

    [Test]
    public void Fragment_UsesLazyFactoryOnceAndPreservesParentIdentityAndProtein()
    {
        Protein rawProtein = new("PEPTIDEKPEPTIDEK", "P1");
        TransientBioPolymer transientParent = new(rawProtein);
        PeptideWithSetModifications rawPeptide = CreatePeptide(rawProtein, 1, "PEPTIDEK");
        int factoryCalls = 0;

        TransientBioPolymerWithSetMods wrapper = new(
            rawPeptide,
            transientParent,
            fragmentFactory: () =>
            {
                factoryCalls++;
                List<Product> products = [];
                rawPeptide.Fragment(DissociationType.HCD, FragmentationTerminus.N, products);
                return products;
            });

        List<Product> firstProducts = [];
        List<Product> secondProducts = [];
        wrapper.Fragment(DissociationType.HCD, FragmentationTerminus.N, firstProducts);
        wrapper.Fragment(DissociationType.HCD, FragmentationTerminus.N, secondProducts);

        Assert.Multiple(() =>
        {
            Assert.That(factoryCalls, Is.EqualTo(1));
            Assert.That(firstProducts.Count, Is.GreaterThan(0));
            Assert.That(secondProducts.Count, Is.EqualTo(firstProducts.Count));
            Assert.That(ReferenceEquals(wrapper.Parent, transientParent), Is.True);
            Assert.That(wrapper.Protein, Is.SameAs(rawProtein));
        });
    }

    [Test]
    public void EqualsAndHashCode_FollowPeptidoformSemanticsAcrossWrappers()
    {
        Protein rawProtein1 = new("PEPTIDEK", "P1");
        Protein rawProtein2 = new("PEPTIDEK", "P1");
        PeptideWithSetModifications rawPeptide1 = CreatePeptide(rawProtein1, 1, "PEPTIDEK");
        PeptideWithSetModifications rawPeptide2 = CreatePeptide(rawProtein2, 1, "PEPTIDEK");

        TransientBioPolymerWithSetMods firstWrapper = new(rawPeptide1, new TransientBioPolymer(rawProtein1));
        TransientBioPolymerWithSetMods secondWrapper = new(rawPeptide2, new TransientBioPolymer(rawProtein2));

        Assert.Multiple(() =>
        {
            Assert.That(firstWrapper.Equals(secondWrapper), Is.True);
            Assert.That(firstWrapper.Equals((IBioPolymerWithSetMods)rawPeptide2), Is.True);
            Assert.That(firstWrapper.GetHashCode(), Is.EqualTo(secondWrapper.GetHashCode()));
        });
    }

    private static DigestionParams CreateDigestionParams()
        => new(protease: "trypsin", maxMissedCleavages: 2, minPeptideLength: 5, fragmentationTerminus: FragmentationTerminus.N);

    private static PeptideWithSetModifications CreatePeptide(Protein parent, int startResidue, string sequence)
        => new(
            parent,
            CreateDigestionParams(),
            startResidue,
            startResidue + sequence.Length - 1,
            CleavageSpecificity.Full,
            sequence,
            0,
            new Dictionary<int, Modification>(),
            0);
}

using Chemistry;
using Omics;
using Omics.BioPolymer;
using Omics.Fragmentation;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace EngineLayer.ParallelSearch.PersistentCache;

public static class TransientCachePayloadSerializer
{
    private const int DigestPayloadSchemaVersion = 1;
    private const int FragmentPayloadSchemaVersion = 1;

    public static byte[] SerializeDigestPayload(
        List<IBioPolymer> proteins,
        Dictionary<int, List<(int peptidoformIndex, int oneBasedStartResidue, int oneBasedEndResidue, int missedCleavages, int cleavageSpecificity, string peptideDescription)>> proteinOccurrences,
        List<(string fullSequence, string baseSequence, string digestionAgent, double monoisotopicMass, Dictionary<int, string> mods)> peptidoforms)
    {
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms, Encoding.UTF8);

        writer.Write(DigestPayloadSchemaVersion);
        writer.Write(proteins.Count);
        foreach (var kvp in proteinOccurrences.OrderBy(k => k.Key))
        {
            writer.Write(kvp.Key);
            writer.Write(kvp.Value.Count);
            foreach (var occ in kvp.Value)
            {
                writer.Write(occ.peptidoformIndex);
                writer.Write(occ.oneBasedStartResidue);
                writer.Write(occ.oneBasedEndResidue);
                writer.Write(occ.missedCleavages);
                writer.Write(occ.cleavageSpecificity);
                WriteUtf8String(writer, occ.peptideDescription);
            }
        }

        writer.Write(peptidoforms.Count);
        foreach (var pf in peptidoforms)
        {
            WriteUtf8String(writer, pf.fullSequence);
            WriteUtf8String(writer, pf.baseSequence);
            WriteUtf8String(writer, pf.digestionAgent);
            writer.Write(pf.monoisotopicMass);
            writer.Write(pf.mods.Count);
            foreach (var mod in pf.mods.OrderBy(m => m.Key))
            {
                writer.Write(mod.Key);
                WriteUtf8String(writer, mod.Value);
            }
        }

        writer.Flush();
        return ms.ToArray();
    }

    public static byte[] SerializeFragmentPayload(List<List<Product>> peptidoformFragments)
    {
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms, Encoding.UTF8);

        writer.Write(FragmentPayloadSchemaVersion);
        writer.Write(peptidoformFragments.Count);
        foreach (var fragments in peptidoformFragments)
        {
            writer.Write(fragments.Count);
            foreach (var product in fragments)
            {
                writer.Write((int)product.ProductType);
                writer.Write((int)product.Terminus);
                writer.Write(product.NeutralMass);
                writer.Write(product.FragmentNumber);
                writer.Write(product.ResiduePosition);
                writer.Write(product.NeutralLoss);
                writer.Write(product.SecondaryProductType.HasValue ? (int)product.SecondaryProductType.Value : -1);
                writer.Write(product.SecondaryFragmentNumber);
            }
        }

        writer.Flush();
        return ms.ToArray();
    }

    public static (
        Dictionary<int, List<(int peptidoformIndex, int oneBasedStartResidue, int oneBasedEndResidue, int missedCleavages, int cleavageSpecificity, string peptideDescription)>> proteinOccurrences,
        List<(string fullSequence, string baseSequence, string digestionAgent, double monoisotopicMass, Dictionary<int, string> mods)> peptidoforms)
        DeserializeDigestPayload(byte[] bytes)
    {
        using MemoryStream ms = new(bytes);
        using BinaryReader reader = new(ms, Encoding.UTF8);

        int schemaVersion = reader.ReadInt32();
        if (schemaVersion != DigestPayloadSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported digest payload schema version {schemaVersion}. Expected {DigestPayloadSchemaVersion}.");
        }

        int proteinCount = reader.ReadInt32();
        var proteinOccurrences = new Dictionary<int, List<(int, int, int, int, int, string)>>();
        for (int i = 0; i < proteinCount; i++)
        {
            int proteinIndex = reader.ReadInt32();
            int occurrenceCount = reader.ReadInt32();
            var occurrences = new List<(int, int, int, int, int, string)>();
            for (int j = 0; j < occurrenceCount; j++)
            {
                int peptidoformIndex = reader.ReadInt32();
                int oneBasedStartResidue = reader.ReadInt32();
                int oneBasedEndResidue = reader.ReadInt32();
                int missedCleavages = reader.ReadInt32();
                int cleavageSpecificity = reader.ReadInt32();
                string peptideDescription = ReadUtf8String(reader);
                occurrences.Add((peptidoformIndex, oneBasedStartResidue, oneBasedEndResidue, missedCleavages, cleavageSpecificity, peptideDescription));
            }
            proteinOccurrences[proteinIndex] = occurrences;
        }

        int peptidoformCount = reader.ReadInt32();
        var peptidoforms = new List<(string, string, string, double, Dictionary<int, string>)>();
        for (int i = 0; i < peptidoformCount; i++)
        {
            string fullSequence = ReadUtf8String(reader);
            string baseSequence = ReadUtf8String(reader);
            string digestionAgent = ReadUtf8String(reader);
            double monoisotopicMass = reader.ReadDouble();
            int modCount = reader.ReadInt32();
            var mods = new Dictionary<int, string>();
            for (int j = 0; j < modCount; j++)
            {
                int position = reader.ReadInt32();
                string modId = ReadUtf8String(reader);
                mods[position] = modId;
            }
            peptidoforms.Add((fullSequence, baseSequence, digestionAgent, monoisotopicMass, mods));
        }

        return (proteinOccurrences, peptidoforms);
    }

    public static List<List<Product>> DeserializeFragmentPayload(byte[] bytes)
    {
        using MemoryStream ms = new(bytes);
        using BinaryReader reader = new(ms, Encoding.UTF8);

        int schemaVersion = reader.ReadInt32();
        if (schemaVersion != FragmentPayloadSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported fragment payload schema version {schemaVersion}. Expected {FragmentPayloadSchemaVersion}.");
        }

        int peptidoformCount = reader.ReadInt32();
        var peptidoformFragments = new List<List<Product>>();
        for (int i = 0; i < peptidoformCount; i++)
        {
            int productCount = reader.ReadInt32();
            var fragments = new List<Product>(productCount);
            for (int j = 0; j < productCount; j++)
            {
                ProductType productType = (ProductType)reader.ReadInt32();
                FragmentationTerminus terminus = (FragmentationTerminus)reader.ReadInt32();
                double neutralMass = reader.ReadDouble();
                int fragmentNumber = reader.ReadInt32();
                int residuePosition = reader.ReadInt32();
                double neutralLoss = reader.ReadDouble();
                int secondaryProductTypeValue = reader.ReadInt32();
                int secondaryFragmentNumber = reader.ReadInt32();

                ProductType? secondaryProductType = secondaryProductTypeValue >= 0 ? (ProductType)secondaryProductTypeValue : null;
                fragments.Add(new Product(productType, terminus, neutralMass, fragmentNumber, residuePosition, neutralLoss, secondaryProductType, secondaryFragmentNumber));
            }
            peptidoformFragments.Add(fragments);
        }

        return peptidoformFragments;
    }

    private static void WriteUtf8String(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string ReadUtf8String(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length < 0)
        {
            throw new InvalidDataException("Invalid UTF-8 string length in transient cache payload.");
        }
        byte[] bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
        {
            throw new InvalidDataException("Truncated UTF-8 string in transient cache payload.");
        }
        return Encoding.UTF8.GetString(bytes);
    }
}

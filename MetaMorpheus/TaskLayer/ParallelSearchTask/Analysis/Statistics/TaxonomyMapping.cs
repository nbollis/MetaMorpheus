#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TaskLayer.ParallelSearchTask.Analysis.Statistics;

/// <summary>
/// Represents taxonomic information for a proteome
/// </summary>
public class TaxonomyInfo
{
    public string ProteomeId { get; set; } = string.Empty;
    public string Organism { get; set; } = string.Empty;
    public string OrganismId { get; set; } = string.Empty;
    public string ProteinCount { get; set; } = string.Empty;
    public string Busco { get; set; } = string.Empty;
    public string Cpd { get; set; } = string.Empty;
    public string Kingdom { get; set; } = string.Empty;
    public string Phylum { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public string Order { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string Genus { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
}

/// <summary>
/// Static class for loading and accessing taxonomic mappings from embedded TSV resources
/// </summary>
public static class TaxonomyMapping
{
    private static readonly Lazy<Dictionary<string, TaxonomyInfo>> _mapping = new(LoadTaxonomyMappings);

    /// <summary>
    /// Get taxonomy information for a proteome ID
    /// </summary>
    public static TaxonomyInfo? GetTaxonomyInfo(string proteomeId)
    {
        return _mapping.Value.TryGetValue(proteomeId, out var info) ? info : null;
    }

    /// <summary>
    /// Check if taxonomy information is available
    /// </summary>
    public static bool HasTaxonomyInfo(string proteomeId)
    {
        return _mapping.Value.ContainsKey(proteomeId);
    }

    /// <summary>
    /// Get all available proteome IDs
    /// </summary>
    public static IEnumerable<string> GetAvailableProteomeIds()
    {
        return _mapping.Value.Keys;
    }

    /// <summary>
    /// Load taxonomy mappings from embedded TSV resources
    /// </summary>
    private static Dictionary<string, TaxonomyInfo> LoadTaxonomyMappings()
    {
        var mapping = new Dictionary<string, TaxonomyInfo>();
        var assembly = Assembly.GetExecutingAssembly();

        // Get all embedded resources that match the taxonomy TSV pattern
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.Contains("Taxonomy") && name.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (resourceNames.Count == 0)
        {
            Console.WriteLine("Warning: No taxonomy TSV files found in embedded resources.");
            return mapping;
        }

        foreach (var resourceName in resourceNames)
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    Console.WriteLine($"Warning: Could not load resource {resourceName}");
                    continue;
                }

                using var reader = new StreamReader(stream);
                ParseTsvFile(reader, mapping);
                Console.WriteLine($"Loaded taxonomy mappings from {resourceName}: {mapping.Count} entries");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading taxonomy resource {resourceName}: {ex.Message}");
            }
        }

        return mapping;
    }

    /// <summary>
    /// Parse TSV file and populate mapping dictionary
    /// </summary>
    private static void ParseTsvFile(StreamReader reader, Dictionary<string, TaxonomyInfo> mapping)
    {
        // Read header line
        string? headerLine = reader.ReadLine();
        if (headerLine == null)
            return;

        var headers = headerLine.Split('\t');

        // Find column indices
        int proteomeIdIndex = Array.IndexOf(headers, "Proteome Id");
        int organismIndex = Array.IndexOf(headers, "Organism");
        int organismIdIndex = Array.IndexOf(headers, "Organism Id");
        int proteinCountIndex = Array.IndexOf(headers, "Protein count");
        int buscoIndex = Array.IndexOf(headers, "BUSCO");
        int cpdIndex = Array.IndexOf(headers, "CPD");
        int kingdomIndex = Array.IndexOf(headers, "Kingdom");
        int phylumIndex = Array.IndexOf(headers, "Phylum");
        int classIndex = Array.IndexOf(headers, "Class");
        int orderIndex = Array.IndexOf(headers, "Order");
        int familyIndex = Array.IndexOf(headers, "Family");
        int genusIndex = Array.IndexOf(headers, "Genus");
        int speciesIndex = Array.IndexOf(headers, "Species");

        if (proteomeIdIndex < 0)
        {
            Console.WriteLine("Warning: 'Proteome Id' column not found in taxonomy TSV");
            return;
        }

        // Read data lines
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var fields = line.Split('\t');

            if (fields.Length <= proteomeIdIndex)
                continue;

            string proteomeId = fields[proteomeIdIndex].Trim();
            if (string.IsNullOrEmpty(proteomeId))
                continue;

            var info = new TaxonomyInfo
            {
                ProteomeId = proteomeId,
                Organism = GetField(fields, organismIndex),
                OrganismId = GetField(fields, organismIdIndex),
                ProteinCount = GetField(fields, proteinCountIndex),
                Busco = GetField(fields, buscoIndex),
                Cpd = GetField(fields, cpdIndex),
                Kingdom = GetField(fields, kingdomIndex),
                Phylum = GetField(fields, phylumIndex),
                Class = GetField(fields, classIndex),
                Order = GetField(fields, orderIndex),
                Family = GetField(fields, familyIndex),
                Genus = GetField(fields, genusIndex),
                Species = GetField(fields, speciesIndex)
            };

            // Add or update mapping (later files can override earlier ones)
            mapping[proteomeId] = info;
        }
    }

    /// <summary>
    /// Safely get field from array
    /// </summary>
    private static string GetField(string[] fields, int index)
    {
        if (index < 0 || index >= fields.Length)
            return string.Empty;

        return fields[index].Trim();
    }

    /// <summary>
    /// Load taxonomy mappings from an external file (for testing or user-provided files)
    /// </summary>
    public static Dictionary<string, TaxonomyInfo> LoadFromFile(string filePath)
    {
        var mapping = new Dictionary<string, TaxonomyInfo>();

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Taxonomy file not found: {filePath}");
        }

        using var reader = new StreamReader(filePath);
        ParseTsvFile(reader, mapping);

        return mapping;
    }
}

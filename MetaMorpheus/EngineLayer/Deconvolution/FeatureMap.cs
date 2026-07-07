using Nett;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EngineLayer.Deconvolution;

/// <summary>
/// A persisted raw-to-feature association document. One file can contain many raw
/// files and many feature-file conditions, so the same spectra can be searched
/// against multiple feature sets without creating raw-adjacent TOMLs.
/// </summary>
public class FeatureMap : IEnumerable<FeatureSpectraEntry>
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Human-readable name for this map file, e.g. a project, batch, or experiment.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The named conditions available in this map, e.g. FlashDeconv vs Dinosaur.
    /// </summary>
    public List<FeatureMapCondition> Conditions { get; set; } = new();

    /// <summary>
    /// One entry per spectra file, each with zero or more condition-specific feature files.
    /// </summary>
    public List<FeatureSpectraEntry> SpectraFiles { get; set; } = new();

    [TomlIgnore]
    public int Count => SpectraFiles.Count;

    public IEnumerator<FeatureSpectraEntry> GetEnumerator() => SpectraFiles.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void AddOrReplaceCondition(FeatureMapCondition condition)
    {
        ArgumentNullException.ThrowIfNull(condition);

        int existingIndex = Conditions.FindIndex(p => p.MatchesKey(condition.ConditionKey));
        if (existingIndex >= 0)
        {
            Conditions[existingIndex] = condition;
        }
        else
        {
            Conditions.Add(condition);
        }

        LastModifiedUtc = DateTime.UtcNow;
    }

    public void AddOrReplaceSpectraEntry(FeatureSpectraEntry spectraEntry)
    {
        ArgumentNullException.ThrowIfNull(spectraEntry);

        int existingIndex = SpectraFiles.FindIndex(p => p.MatchesRawFile(spectraEntry.MassSpecFilePath));
        if (existingIndex >= 0)
        {
            SpectraFiles[existingIndex] = spectraEntry;
        }
        else
        {
            SpectraFiles.Add(spectraEntry);
        }

        LastModifiedUtc = DateTime.UtcNow;
    }

    public bool TryGetSpectraEntry(string rawFilePath, out FeatureSpectraEntry entry)
    {
        entry = SpectraFiles.FirstOrDefault(p => p.MatchesRawFile(rawFilePath));
        return entry is not null;
    }

    public bool TryGetFeatureFile(string rawFilePath, string conditionKey, out string featureFilePath)
    {
        featureFilePath = null;

        if (!TryGetSpectraEntry(rawFilePath, out var spectraEntry))
        {
            return false;
        }

        if (!spectraEntry.TryGetConditionFile(conditionKey, out var conditionFile))
        {
            return false;
        }

        featureFilePath = conditionFile.FeatureFilePath;
        return true;
    }
}

/// <summary>
/// Declares one named condition that can be applied across many spectra files.
/// </summary>
public class FeatureMapCondition
{
    /// <summary>
    /// Stable machine-readable key, e.g. <c>flashdeconv-default</c> or <c>dinosaur-tight-rt</c>.
    /// </summary>
    public string ConditionKey { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name shown in UI/CLI selection surfaces.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string FeatureGenerationMethod { get; set; } = string.Empty;

    public string FeatureGenerationVersion { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public bool MatchesKey(string conditionKey)
        => string.Equals(ConditionKey, conditionKey, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// One raw spectra file and all of its condition-specific feature-file associations.
/// </summary>
public class FeatureSpectraEntry
{
    public string MassSpecFilePath { get; set; } = string.Empty;

    public string MassSpecFileName { get; set; } = string.Empty;

    public string MassSpecFileNameWithoutExtension { get; set; } = string.Empty;

    public long? MassSpecFileSizeBytes { get; set; }

    public DateTime? MassSpecFileLastWriteTimeUtc { get; set; }

    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// All feature-file variants available for this raw file, keyed by condition.
    /// </summary>
    public List<FeatureFileConditionEntry> FeatureFiles { get; set; } = new();

    public static FeatureSpectraEntry Create(string rawFilePath)
    {
        return new FeatureSpectraEntry
        {
            MassSpecFilePath = rawFilePath,
            MassSpecFileName = Path.GetFileName(rawFilePath),
            MassSpecFileNameWithoutExtension = Path.GetFileNameWithoutExtension(rawFilePath),
            MassSpecFileSizeBytes = File.Exists(rawFilePath) ? new FileInfo(rawFilePath).Length : null,
            MassSpecFileLastWriteTimeUtc = File.Exists(rawFilePath) ? File.GetLastWriteTimeUtc(rawFilePath) : null,
        };
    }

    public bool MatchesRawFile(string rawFilePath)
        => string.Equals(MassSpecFilePath, rawFilePath, StringComparison.OrdinalIgnoreCase);

    public void AddOrReplaceConditionFile(FeatureFileConditionEntry conditionEntry)
    {
        ArgumentNullException.ThrowIfNull(conditionEntry);

        int existingIndex = FeatureFiles.FindIndex(p => p.MatchesCondition(conditionEntry.ConditionKey));
        if (existingIndex >= 0)
        {
            FeatureFiles[existingIndex] = conditionEntry;
        }
        else
        {
            FeatureFiles.Add(conditionEntry);
        }
    }

    public bool TryGetConditionFile(string conditionKey, out FeatureFileConditionEntry entry)
    {
        entry = FeatureFiles.FirstOrDefault(p => p.MatchesCondition(conditionKey));
        return entry is not null;
    }
}

/// <summary>
/// A condition-specific feature-file association for a single raw spectra file.
/// </summary>
public class FeatureFileConditionEntry
{
    public string ConditionKey { get; set; } = string.Empty;

    public string FeatureFilePath { get; set; } = string.Empty;

    public string FeatureFileName { get; set; } = string.Empty;

    public long? FeatureFileSizeBytes { get; set; }

    public DateTime? FeatureFileLastWriteTimeUtc { get; set; }

    public string Notes { get; set; } = string.Empty;

    public static FeatureFileConditionEntry Create(string conditionKey, string featureFilePath)
    {
        return new FeatureFileConditionEntry
        {
            ConditionKey = conditionKey,
            FeatureFilePath = featureFilePath,
            FeatureFileName = Path.GetFileName(featureFilePath),
            FeatureFileSizeBytes = File.Exists(featureFilePath) ? new FileInfo(featureFilePath).Length : null,
            FeatureFileLastWriteTimeUtc = File.Exists(featureFilePath) ? File.GetLastWriteTimeUtc(featureFilePath) : null,
        };
    }

    public bool MatchesCondition(string conditionKey)
        => string.Equals(ConditionKey, conditionKey, StringComparison.OrdinalIgnoreCase);
}

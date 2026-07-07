using System;
using System.IO;

namespace TaskLayer.Deconvolution.FeatureFileMapping;

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

using System;
using System.IO;

namespace EngineLayer.ParallelSearch.PersistentCache;

public static class TransientCacheMessages
{
    public static string FormatLookupMessage(TransientCacheLookupOutcome outcome, string databasePath, string detail = null)
    {
        string databaseName = GetDatabaseDisplayName(databasePath);
        string coreMessage = outcome switch
        {
            TransientCacheLookupOutcome.Hit => $"Hit for '{databaseName}'.",
            TransientCacheLookupOutcome.Miss => $"Miss for '{databaseName}'. Falling back to base behavior.",
            TransientCacheLookupOutcome.SettingsMismatch => $"Settings mismatch for '{databaseName}'. Falling back to base behavior.",
            TransientCacheLookupOutcome.Corrupt => $"Corrupt cache entry for '{databaseName}'. Falling back to base behavior.",
            TransientCacheLookupOutcome.IdentityMismatch => $"Parent identity validation failed for '{databaseName}'. Falling back to base behavior.",
            TransientCacheLookupOutcome.Disabled => $"Disabled for '{databaseName}'. Falling back to base behavior.",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
        };

        return AppendDetail($"{TransientCacheSchema.MessagePrefix} {coreMessage}", detail);
    }

    public static string FormatPublishMessage(TransientCachePublishState publishState, string databasePath, string detail = null)
    {
        string databaseName = GetDatabaseDisplayName(databasePath);
        string coreMessage = publishState switch
        {
            TransientCachePublishState.Pending => $"Building cache entry for '{databaseName}'.",
            TransientCachePublishState.Published => $"Published cache entry for '{databaseName}'.",
            TransientCachePublishState.Failed => $"Failed to publish cache entry for '{databaseName}'. Falling back to base behavior.",
            TransientCachePublishState.Corrupt => $"Rejected corrupt cache entry for '{databaseName}'. Falling back to base behavior.",
            _ => throw new ArgumentOutOfRangeException(nameof(publishState), publishState, null),
        };

        return AppendDetail($"{TransientCacheSchema.MessagePrefix} {coreMessage}", detail);
    }

    public static bool ShouldWarn(TransientCacheLookupOutcome outcome)
    {
        return outcome is TransientCacheLookupOutcome.SettingsMismatch or TransientCacheLookupOutcome.Corrupt or TransientCacheLookupOutcome.IdentityMismatch;
    }

    private static string GetDatabaseDisplayName(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            return "<unknown>";
        }

        string fileName = Path.GetFileName(databasePath);
        return string.IsNullOrWhiteSpace(fileName) ? databasePath : fileName;
    }

    private static string AppendDetail(string message, string detail)
    {
        return string.IsNullOrWhiteSpace(detail) ? message : $"{message} Detail: {detail}";
    }
}

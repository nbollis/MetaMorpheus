using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using UsefulProteomicsDatabases;

namespace EngineLayer.ParallelSearch.PersistentCache;

// TODO: Separate digestion params from fragmentation params in cache settings descriptor, and only include digestion params in the cache settings descriptor. Fragmentation params can be included in the cache value, and the cache key can be based solely on digestion params. This would allow for more cache hits when fragmentation params change but digestion params stay the same.

public sealed class TransientCacheSettingsDescriptor
{
    public string CacheSettingsId { get; }
    public string CanonicalSettingsPayload { get; }

    private TransientCacheSettingsDescriptor(string cacheSettingsId, string canonicalSettingsPayload)
    {
        CacheSettingsId = cacheSettingsId;
        CanonicalSettingsPayload = canonicalSettingsPayload;
    }

    public static TransientCacheSettingsDescriptor Create(
        CommonParameters commonParameters,
        DecoyType decoyType,
        bool generateTargets,
        IEnumerable<string> localizableModificationTypes,
        TargetContaminantAmbiguity targetContaminantAmbiguity = TargetContaminantAmbiguity.RemoveContaminant)
    {
        ArgumentNullException.ThrowIfNull(commonParameters);
        ArgumentNullException.ThrowIfNull(commonParameters.DigestionParams);

        string canonicalPayload = BuildCanonicalPayload(
            commonParameters,
            decoyType,
            generateTargets,
            localizableModificationTypes,
            targetContaminantAmbiguity);

        return new TransientCacheSettingsDescriptor(
            TransientCacheHashing.ComputeCacheSettingsId(canonicalPayload),
            canonicalPayload);
    }

    private static string BuildCanonicalPayload(
        CommonParameters commonParameters,
        DecoyType decoyType,
        bool generateTargets,
        IEnumerable<string> localizableModificationTypes,
        TargetContaminantAmbiguity targetContaminantAmbiguity)
    {
        StringBuilder builder = new();
        // Schema
        builder.AppendLine($"SchemaVersion={TransientCacheSchema.CurrentSchemaVersion}");
        builder.AppendLine($"HashAlgorithm={TransientCacheSchema.HashAlgorithmName}");

        // Digestion 
        builder.AppendLine($"GenerateTargets={generateTargets}");
        builder.AppendLine($"DecoyType={decoyType}");
        builder.AppendLine($"TargetContaminantAmbiguity={targetContaminantAmbiguity}");
        builder.AppendLine($"AddTruncations={commonParameters.AddTruncations}");
        builder.AppendLine($"DigestionParamsType={commonParameters.DigestionParams.GetType().FullName ?? commonParameters.DigestionParams.GetType().Name}");
        builder.AppendLine($"VariableModifications={NormalizeModificationTuples(commonParameters.ListOfModsVariable)}");
        builder.AppendLine($"LocalizableModificationTypes={NormalizeStrings(localizableModificationTypes)}");
        builder.AppendLine($"CustomIons={NormalizeEnums(commonParameters.CustomIons)}");
        builder.AppendLine($"FixedModifications={NormalizeModificationTuples(commonParameters.ListOfModsFixed)}");

        // Fragmentation
        builder.AppendLine($"DissociationType={commonParameters.DissociationType}");

        AppendDigestionParams(builder, commonParameters.DigestionParams);
        AppendFragmentationParams(builder, commonParameters.FragmentationParameters);
        return builder.ToString();
    }

    private static void AppendDigestionParams(StringBuilder builder, object digestionParams)
    {
        foreach (PropertyInfo property in digestionParams.GetType()
                     .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                     .OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            object value = property.GetValue(digestionParams);
            builder.Append("DigestionParams.")
                .Append(property.Name)
                .Append('=')
                .AppendLine(CanonicalizeValue(value));
        }
    }

    private static void AppendFragmentationParams(StringBuilder builder, object fragmentationParams)
    {
        foreach (PropertyInfo property in fragmentationParams.GetType()
                     .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                     .OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            object value = property.GetValue(fragmentationParams);
            builder.Append("FragmentationParams.")
                .Append(property.Name)
                .Append('=')
                .AppendLine(CanonicalizeValue(value));
        }
    }

    private static string NormalizeModificationTuples(IEnumerable<(string, string)> modifications)
    {
        if (modifications == null)
        {
            return string.Empty;
        }

        return string.Join("|", modifications
            .Select(m => $"{m.Item1}::{m.Item2}")
            .OrderBy(m => m, StringComparer.Ordinal));
    }

    private static string NormalizeStrings(IEnumerable<string> values)
    {
        if (values == null)
        {
            return string.Empty;
        }

        return string.Join("|", values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .OrderBy(v => v, StringComparer.Ordinal));
    }

    private static string NormalizeEnums<TEnum>(IEnumerable<TEnum> values) where TEnum : struct, Enum
    {
        if (values == null)
        {
            return string.Empty;
        }

        return string.Join("|", values
            .Select(v => v.ToString())
            .OrderBy(v => v, StringComparer.Ordinal));
    }

    private static string CanonicalizeValue(object value)
    {
        if (value == null)
        {
            return "<null>";
        }

        Type type = value.GetType();

        if (value is string stringValue)
        {
            return stringValue;
        }

        if (type.IsEnum)
        {
            return value.ToString();
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        }

        if (value is IEnumerable enumerable)
        {
            List<string> items = [];
            foreach (object item in enumerable)
            {
                items.Add(CanonicalizeValue(item));
            }

            items.Sort(StringComparer.Ordinal);
            return string.Join("|", items);
        }

        PropertyInfo nameProperty = type.GetProperty("Name", BindingFlags.Instance | BindingFlags.Public);
        if (nameProperty?.CanRead == true && nameProperty.GetIndexParameters().Length == 0)
        {
            return $"{type.FullName}:{CanonicalizeValue(nameProperty.GetValue(value))}";
        }

        return value.ToString() ?? type.FullName ?? type.Name;
    }
}

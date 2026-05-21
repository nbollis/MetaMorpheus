#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace TaskLayer.ParallelSearch.Statistics.Calibration;

public static class CalibrationReportWriter
{
    public static void WriteReport(CalibrationResult result, string outputPath)
    {
        using var writer = new StreamWriter(outputPath);
        WriteReport(result, writer);
    }

    public static string FormatReport(CalibrationResult result)
    {
        var sb = new StringBuilder();
        using var writer = new StringWriter(sb);
        WriteReport(result, writer);
        return sb.ToString();
    }

    private static void WriteReport(CalibrationResult result, TextWriter writer)
    {
        writer.WriteLine("=" .PadRight(70, '='));
        writer.WriteLine("  CALIBRATION REPORT");
        writer.WriteLine("=" .PadRight(70, '='));
        writer.WriteLine();

        writer.WriteLine($"  Alpha:                          {result.Alpha}");
        writer.WriteLine($"  Total databases:                {result.TotalDatabases}");
        writer.WriteLine($"  Null bulk databases:            {result.NullBulkDatabaseCount}");
        writer.WriteLine($"  Removed as outliers:            {result.DatabasesRemovedAsOutliers}");
        writer.WriteLine($"  Iterations for bulk isolation:  {result.IterationsUsed}");
        writer.WriteLine();

        if (result.NullBulkDatabaseCount == 0)
        {
            writer.WriteLine("  WARNING: No databases remained in the null bulk.");
            writer.WriteLine("  Calibration cannot proceed — all databases appear to carry signal.");
            writer.WriteLine();
            return;
        }

        WriteSectionHeader(writer, "OVERALL NULL DISTRIBUTIONS");

        WriteProfile(writer, "  Tests passed per database", result.OverallTestPassCountProfile);
        WriteProfile(writer, "  Families passed per database", result.OverallFamilyPassCountProfile);
        WriteProfile(writer, "  Combined p-value (All)", result.CombinedPValueProfile);
        WriteProfile(writer, "  Combined q-value (All)", result.CombinedQValueProfile);

        writer.WriteLine();

        if (result.PerFamilyTestPassCountProfiles.Count > 0)
        {
            WriteSectionHeader(writer, "PER-FAMILY TEST PASS COUNTS (null bulk)");
            writer.WriteLine();
            writer.WriteLine($"  {"Family",-30} {"Mean",8} {"P50",8} {"P90",8} {"P95",8} {"P99",8}");
            writer.WriteLine("  " + new string('-', 70));

            foreach (var kvp in result.PerFamilyTestPassCountProfiles.OrderBy(p => p.Key.ToString()))
            {
                var profile = kvp.Value;
                writer.WriteLine($"  {kvp.Key,-30} {profile.Mean,8:F2} {profile.Percentile50,8:F0} {profile.Percentile90,8:F0} {profile.Percentile95,8:F0} {profile.Percentile99,8:F0}");
            }

            writer.WriteLine();
        }

        if (result.PerTestPValueProfiles.Count > 0)
        {
            WriteSectionHeader(writer, "PER-TEST P-VALUE DISTRIBUTIONS (null bulk)");
            writer.WriteLine();
            writer.WriteLine($"  {"Test (key)",-40} {"Count",6} {"Mean",8} {"P50",8} {"P90",8} {"P95",8} {"P99",8}");
            writer.WriteLine("  " + new string('-', 86));

            foreach (var kvp in result.PerTestPValueProfiles.OrderBy(p => p.Key))
            {
                var p = kvp.Value;
                writer.WriteLine($"  {kvp.Key,-40} {p.Count,6} {p.Mean,8:F3} {p.Percentile50,8:F3} {p.Percentile90,8:F3} {p.Percentile95,8:F3} {p.Percentile99,8:F3}");
            }

            writer.WriteLine();
        }

        if (result.PerTestEffectSizeProfiles.Count > 0)
        {
            WriteSectionHeader(writer, "PER-TEST EFFECT SIZE DISTRIBUTIONS (null bulk)");
            writer.WriteLine();
            writer.WriteLine($"  {"Test (key)",-40} {"Count",6} {"Mean",8} {"P50",8} {"P90",8} {"P95",8} {"P99",8}");
            writer.WriteLine("  " + new string('-', 86));

            foreach (var kvp in result.PerTestEffectSizeProfiles.OrderBy(p => p.Key))
            {
                var p = kvp.Value;
                writer.WriteLine($"  {kvp.Key,-40} {p.Count,6} {p.Mean,8:F3} {p.Percentile50,8:F3} {p.Percentile90,8:F3} {p.Percentile95,8:F3} {p.Percentile99,8:F3}");
            }

            writer.WriteLine();
        }

        WriteSectionHeader(writer, "RECOMMENDED THRESHOLDS");
        writer.WriteLine();
        writer.WriteLine("  Based on null bulk 95th and 99th percentiles:");
        writer.WriteLine();

        if (result.OverallTestPassCountProfile != null)
        {
            writer.WriteLine($"    A database passing >= {result.OverallTestPassCountProfile.Percentile95:F0} tests is beyond the 95th percentile of null.");
            writer.WriteLine($"    A database passing >= {result.OverallTestPassCountProfile.Percentile99:F0} tests is beyond the 99th percentile of null.");
        }

        if (result.OverallFamilyPassCountProfile != null)
        {
            writer.WriteLine($"    A database with >= {result.OverallFamilyPassCountProfile.Percentile95:F0} evidence families is beyond the 95th percentile of null.");
            writer.WriteLine($"    A database with >= {result.OverallFamilyPassCountProfile.Percentile99:F0} evidence families is beyond the 99th percentile of null.");
        }

        if (result.CombinedPValueProfile != null)
        {
            writer.WriteLine($"    A combined p-value <= {result.CombinedPValueProfile.Percentile95:E3} is beyond the 95th percentile of null.");
            writer.WriteLine($"    A combined p-value <= {result.CombinedPValueProfile.Percentile99:E3} is beyond the 99th percentile of null.");
        }

        writer.WriteLine();
        writer.WriteLine("=" .PadRight(70, '='));
        writer.WriteLine("  END CALIBRATION REPORT");
        writer.WriteLine("=" .PadRight(70, '='));
        writer.WriteLine();
    }

    private static void WriteSectionHeader(TextWriter writer, string title)
    {
        writer.WriteLine("  " + title);
        writer.WriteLine("  " + new string('-', title.Length));
    }

    private static void WriteProfile(TextWriter writer, string label, NullDistributionProfile? profile)
    {
        if (profile == null)
        {
            writer.WriteLine($"  {label,-35} (no null data available)");
            return;
        }

        writer.WriteLine($"  {label,-35} n={profile.Count,5}  mean={profile.Mean,8:F4}  sd={profile.StdDev,8:F4}  " +
                        $"P50={profile.Percentile50,8:F4}  P90={profile.Percentile90,8:F4}  " +
                        $"P95={profile.Percentile95,8:F4}  P99={profile.Percentile99,8:F4}");
    }
}

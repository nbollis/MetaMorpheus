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
        writer.WriteLine();

        if (result.TotalDatabases == 0)
        {
            writer.WriteLine("  WARNING: No databases to calibrate.");
            writer.WriteLine();
            return;
        }

        WriteSectionHeader(writer, "OVERALL DISTRIBUTIONS (all databases)");

        WriteProfile(writer, "  Tests passed per database", result.OverallTestPassCountProfile);
        WriteProfile(writer, "  Families passed per database", result.OverallFamilyPassCountProfile);
        WriteProfile(writer, "  Combined p-value (All)", result.CombinedPValueProfile);
        WriteProfile(writer, "  Combined q-value (All)", result.CombinedQValueProfile);

        writer.WriteLine();

        if (result.PerFamilyTestPassCountProfiles.Count > 0)
        {
            WriteSectionHeader(writer, "PER-FAMILY TEST PASS COUNTS (all databases)");
            writer.WriteLine();
            writer.WriteLine($"  {"Family",-30} {"Mean",8} {"P50",8} {"P90",8} {"P95",8} {"P99",8}");
            writer.WriteLine("  " + new string('-', 77));

            foreach (var kvp in result.PerFamilyTestPassCountProfiles.OrderBy(p => p.Key.ToString()))
            {
                var profile = kvp.Value;
                writer.WriteLine($"  {kvp.Key,-30} {profile.Mean,8:F2} {profile.Percentile50,8:F0} {profile.Percentile90,8:F0} {profile.Percentile95,8:F0} {profile.Percentile99,8:F0}");
            }

            writer.WriteLine();
        }

        if (result.PerTestPValueProfiles.Count > 0)
        {
            WriteSectionHeader(writer, "PER-TEST P-VALUE DISTRIBUTIONS (all databases)");
            writer.WriteLine();
            writer.WriteLine($"  {"Test (key)",-50} {"Count",6} {"Mean",8} {"P50",8} {"P90",8} {"P95",8} {"P99",8}");
            writer.WriteLine("  " + new string('-', 103));

            foreach (var kvp in result.PerTestPValueProfiles.OrderBy(p => p.Key))
            {
                var p = kvp.Value;
                writer.WriteLine($"  {kvp.Key,-50} {p.Count,6} {p.Mean,8:F3} {p.Percentile50,8:F3} {p.Percentile90,8:F3} {p.Percentile95,8:F3} {p.Percentile99,8:F3}");
            }

            writer.WriteLine();
        }

        if (result.PerTestEffectSizeProfiles.Count > 0)
        {
            WriteSectionHeader(writer, "PER-TEST EFFECT SIZE DISTRIBUTIONS (all databases)");
            writer.WriteLine();
            writer.WriteLine($"  {"Test (key)",-50} {"Count",6} {"Mean",8} {"P50",8} {"P90",8} {"P95",8} {"P99",8}");
            writer.WriteLine("  " + new string('-', 103));

            foreach (var kvp in result.PerTestEffectSizeProfiles.OrderBy(p => p.Key))
            {
                var p = kvp.Value;
                writer.WriteLine($"  {kvp.Key,-50} {p.Count,6} {p.Mean,8:F3} {p.Percentile50,8:F3} {p.Percentile90,8:F3} {p.Percentile95,8:F3} {p.Percentile99,8:F3}");
            }

            writer.WriteLine();
        }

        WriteSectionHeader(writer, "RECOMMENDED THRESHOLDS");
        writer.WriteLine();
        writer.WriteLine("  Based on 95th and 99th percentiles across all databases:");
        writer.WriteLine();

        if (result.OverallTestPassCountProfile != null)
        {
            writer.WriteLine($"    A database passing >= {result.OverallTestPassCountProfile.Percentile95:F0} tests is beyond the 95th percentile.");
            writer.WriteLine($"    A database passing >= {result.OverallTestPassCountProfile.Percentile99:F0} tests is beyond the 99th percentile.");
        }

        if (result.OverallFamilyPassCountProfile != null)
        {
            writer.WriteLine($"    A database with >= {result.OverallFamilyPassCountProfile.Percentile95:F0} evidence families is beyond the 95th percentile.");
            writer.WriteLine($"    A database with >= {result.OverallFamilyPassCountProfile.Percentile99:F0} evidence families is beyond the 99th percentile.");
        }

        if (result.CombinedPValueProfile != null)
        {
            writer.WriteLine($"    A combined p-value <= {result.CombinedPValueProfile.Percentile95:E3} is beyond the 95th percentile.");
            writer.WriteLine($"    A combined p-value <= {result.CombinedPValueProfile.Percentile99:E3} is beyond the 99th percentile.");
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
            writer.WriteLine($"  {label,-35} (no data available)");
            return;
        }

        writer.WriteLine($"  {label,-35} n={profile.Count,5}  mean={profile.Mean,8:F4}  sd={profile.StdDev,8:F4}  " +
                        $"P50={profile.Percentile50,8:F4}  P90={profile.Percentile90,8:F4}  " +
                        $"P95={profile.Percentile95,8:F4}  P99={profile.Percentile99,8:F4}");
    }
}

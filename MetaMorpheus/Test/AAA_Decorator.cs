using Easy.Common.Extensions;
using NUnit.Framework;
using Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskLayer;

namespace Test;

[TestFixture]
public class AAA_Decorator
{
    static string ProteomeDir = @"B:\Users\Nic\BacterialProteomics\Uniprot\Bacteria_Reviewed";
    private record RunInfo(string SearchDir, string proteomeDir, string TOMLPath);

    static RunInfo BigRun_EV = new RunInfo(
        @"D:\Projects\BacterialProteomics\BigRun_EV\Task1-ManySearchTask",
        ProteomeDir,
        @"D:\Projects\BacterialProteomics\BigRun_EV\Task Settings\Task1-ManySearchTaskconfig.toml"
    );

    static RunInfo BigRun_NotEV = new RunInfo(
        @"D:\Projects\BacterialProteomics\BigRun_notEV\Task1-ManySearchTask",
        ProteomeDir,
        @"D:\Projects\BacterialProteomics\BigRun_notEV\Task Settings\Task1-ManySearchTaskconfig.toml"
    );

    static RunInfo VaginalSpike_Control = new RunInfo(
        @"D:\Projects\BacterialProteomics\VaginalSpike_BacterialControls\Task1-ManySearchTask",
        ProteomeDir,
        @"D:\Projects\BacterialProteomics\VaginalSpike_BacterialControls\Task Settings\Task1-ManySearchTaskconfig.toml");


    static RunInfo VaginalSpike_Ascites = new RunInfo(
        @"D:\Projects\BacterialProteomics\VaginalSpike_AllAscitesAllProteomes\Task1-ManySearchTask",
        ProteomeDir,
        @"D:\Projects\BacterialProteomics\VaginalSpike_AllAscitesAllProteomes\Task Settings\Task1-ManySearchTaskconfig.toml");

    [Test]
    public void TestMethod()
    {
        RunInfo info = VaginalSpike_Control;

        //var decorator = new PostHocDecorator(info.SearchDir, info.proteomeDir, info.TOMLPath);

        //decorator.DecorateAndWrite();
    }

    [Test]
    public void FixPsmOutput()
    {
        bool makeBackup = false;
        string rootDir = @"D:\Projects\BacterialProteomics\CovidSpikedIn_Bulk_FixPepWriterAndFragmentLengthNormalization\Task1-ParallelSearchTask";

        var fixedFiles = PsmFixer.TraverseAndFix(rootDir, makeBackup);


        rootDir = @"D:\Projects\BacterialProteomics\CovidSpikedIn_Bulk\Task1-ParallelSearchTask";
        fixedFiles = PsmFixer.TraverseAndFix(rootDir, makeBackup);
    }
}



public static class PsmFixer
{
    public static List<string> TraverseAndFix(string rootDir, bool makeBackup)
    {
        var fixedFiles = new List<string>();

        foreach (var file in Directory.EnumerateFiles(
                     rootDir,
                     "*.psmtsv",
                     SearchOption.AllDirectories))
        {
            if (file.Contains("- Copy"))
                continue;

            try
            {
                if (DetectAndFixPsmTsv(file, makeBackup))
                    fixedFiles.Add(file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {file}: {ex.Message}");
            }
        }

        return fixedFiles;
    }

    public static bool DetectAndFixPsmTsv(string path, bool makeBackup)
    {
        using var reader = new StreamReader(path);

        string? headerLine = reader.ReadLine();
        if (headerLine == null)
            return false;

        var headerCols = headerLine.Split('\t');
        int expectedCols = headerCols.Length;
        int organismColIndex = Array.IndexOf(headerCols, SpectrumMatchFromTsvHeader.OrganismName);

        var outputLines = new List<string> { headerLine };
        bool modified = false;

        string? line;
        int lineNumber = 1;

        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            var cols = line.Split('\t');

            if (cols.Length != expectedCols)
            {
                modified = true;

                if (cols.Length > expectedCols)
                {
                    if (organismColIndex >= 0 && cols[organismColIndex+1].IsNullOrEmpty() && cols[organismColIndex+2].IsNullOrEmpty())
                    {
                        // Shift left to remove two empty columns after OrganismName
                        for (int i = organismColIndex + 1; i < cols.Length - 2; i++)
                        {
                            cols[i] = cols[i + 2];
                        }
                        // Resize to expected columns
                        Array.Resize(ref cols, expectedCols);
                    }
                    else
                        cols = cols.Take(expectedCols).ToArray();
                }
                else
                {
                    Array.Resize(ref cols, expectedCols);
                }
            }

            outputLines.Add(string.Join('\t', cols));
        }

        if (!modified)
            return false;

        reader.Dispose();

        if (makeBackup)
        {
            string backupPath = path + ".bak";
            if (!File.Exists(backupPath))
                File.Move(path, backupPath);
        }

        File.WriteAllLines(path, outputLines);
        return true;
    }
}

public abstract class Decorator(string directoryPath)
{
    public void Decorate()
    {
        //var resultManager = TaskLayer.ParallelSearchTask.CreateResultsManager(directoryPath, true);
    }
}

public class RetentionTimeDecorator(string directoryPath) : Decorator(directoryPath)
{

}
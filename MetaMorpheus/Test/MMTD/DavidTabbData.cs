using System;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Test;

public static class DavidTabbData
{
    public static string DavidTabbDirectory => @"D:\Projects\Top Down MetaMorpheus\DavidTabbResults";
    public static string DataFileDirectory => @"D:\DataFiles\TopDown";

    public static string AveragingTomlPath =>
        @"D:\Projects\Top Down MetaMorpheus\DavidTabbResults\AveragingTask.toml";

    public static IEnumerable<DavidTabbDataSet> GetTabbDataSets()
    {
        //string name = "hela1";
        //string dataDirectory = @"D:\DataFiles\Hela_1";
        //string databasepath = @"D:\Databases\ArchaeonHR02_UP000236403_2023_10_06.fasta";
        //List<string> dataFiles = new()
        //    { Path.Combine(TestContext.CurrentContext.TestDirectory, "sliced-raw.mzML") };
        //string calibTomlPath = @"D:\DataFiles\Hela_1\2023-11-21-13-57-21\Task Settings\Task1-CalibrateTaskconfig.toml";
        //string[] searchTomlPaths = Directory.GetFiles(dataDirectory, "*.toml")
        //    .Where(p => p.Contains("Search", StringComparison.InvariantCultureIgnoreCase))
        //    .ToArray();

        //string gptmdTomlpath = Path.Combine(dataDirectory, "GPTMD.toml");
        //yield return new DavidTabbDataSet(name, dataDirectory, dataFiles, databasepath, calibTomlPath, searchTomlPaths, gptmdTomlpath);



        string name = "PXD003074-SULIS-HCD";
        string dataDirectory = Path.Combine(DavidTabbDirectory, "PXD003074-SULIS");
        string databasepath = Path.Combine(DavidTabbDirectory, "PXD003074-SULIS",
            "20210301-UP000013006-UP000000625.fasta");
        List<string> dataFiles =
            Directory.GetFiles(@"D:\DataFiles\TopDown\PXD003074-SULIS", "*HCD20_01.raw").ToList();
        string calibTomlPath = Path.Combine(dataDirectory, "HCDCalibrateTaskconfig.toml");
        string[] searchTomlPaths = Directory.GetFiles(dataDirectory, "*.toml")
            .Where(p => p.Contains("Search", StringComparison.InvariantCultureIgnoreCase) && p.Contains("HCD"))
            .ToArray();
        string gptmdTomlpath = Path.Combine(dataDirectory, "HCDGPTMD.toml");
        yield return new DavidTabbDataSet(name, dataDirectory, dataFiles, databasepath, calibTomlPath, searchTomlPaths, gptmdTomlpath);


        name = "PXD003074-SULIS-ETD";
        dataDirectory = Path.Combine(DavidTabbDirectory, "PXD003074-SULIS");
        databasepath = Path.Combine(DavidTabbDirectory, "PXD003074-SULIS",
            "20210301-UP000013006-UP000000625.fasta");
        dataFiles = Directory.GetFiles(@"D:\DataFiles\TopDown\PXD003074-SULIS", "*ETD6.raw").ToList();
        calibTomlPath = Path.Combine(dataDirectory, "ETDCalibrateTaskconfig.toml");
        searchTomlPaths = Directory.GetFiles(dataDirectory, "*.toml")
            .Where(p => p.Contains("Search", StringComparison.InvariantCultureIgnoreCase) && p.Contains("ETD"))
            .ToArray();
        gptmdTomlpath = Path.Combine(dataDirectory, "ETDGPTMD.toml");
        yield return new DavidTabbDataSet(name, dataDirectory, dataFiles, databasepath, calibTomlPath, searchTomlPaths, gptmdTomlpath);


        // TODO below, get calib toml's in directories
        name = @"PXD005420-HUMAN";
        dataDirectory = Path.Combine(DavidTabbDirectory, "PXD005420-HUMAN");
        databasepath = Path.Combine(DavidTabbDirectory, "PXD005420-HUMAN", "20220130_up000005640.fasta");
        dataFiles = Directory.GetFiles(@"D:\DataFiles\TopDown\PXD005420-HUMAN", "*.raw").ToList();
        calibTomlPath = Path.Combine(dataDirectory, "CalibrateTask.toml");
        searchTomlPaths = Directory.GetFiles(dataDirectory, "*.toml")
            .Where(p => p.Contains("Search", StringComparison.InvariantCultureIgnoreCase))
            .ToArray();
        gptmdTomlpath = Path.Combine(dataDirectory, "GPTMD.toml");
        yield return new DavidTabbDataSet(name, dataDirectory, dataFiles, databasepath, calibTomlPath, searchTomlPaths, gptmdTomlpath);


        //name = @"PXD010825-PIG";
        //dataDirectory = Path.Combine(DavidTabbDirectory, "PXD010825-PIG");
        //databasepath = Path.Combine(DavidTabbDirectory, "PXD010825-PIG", "20210706-MSV000080621-Subset.fasta");
        //dataFiles = Directory.GetFiles(@"D:\DataFiles\TopDown\PXD010825-PIG", "*.baf").ToList();
        //calibTomlPath = Path.Combine(dataDirectory, "CalibrateTask.toml");
        //searchTomlPaths = Directory.GetFiles(dataDirectory, "*.toml")
        //    .Where(p => p.Contains("Search", StringComparison.InvariantCultureIgnoreCase))
        //    .ToArray();
        //yield return new DavidTabbDataSet(name, dataDirectory, dataFiles, databasepath, calibTomlPath, searchTomlPaths);


        name = @"PXD019247-ECOLI";
        dataDirectory = Path.Combine(DavidTabbDirectory, "PXD019247-ECOLI");
        databasepath = Path.Combine(DavidTabbDirectory, "PXD003074-SULIS",
            "20210301-UP000013006-UP000000625.fasta");
        dataFiles = Directory.GetFiles(@"D:\DataFiles\TopDown\PXD019247-ECOLI", "*.raw").ToList();
        calibTomlPath = Path.Combine(dataDirectory, "CalibrateTask.toml");
        searchTomlPaths = Directory.GetFiles(dataDirectory, "*.toml")
            .Where(p => p.Contains("Search", StringComparison.InvariantCultureIgnoreCase))
            .ToArray();
        gptmdTomlpath = Path.Combine(dataDirectory, "GPTMD.toml");
        yield return new DavidTabbDataSet(name, dataDirectory, dataFiles, databasepath, calibTomlPath, searchTomlPaths, gptmdTomlpath);


        name = @"PXD019368-HUMAN";
        dataDirectory = Path.Combine(DavidTabbDirectory, "PXD019368-HUMAN");
        databasepath = Path.Combine(DavidTabbDirectory, "PXD019368-HUMAN", "PXD017858-HEK293T.fasta");
        dataFiles = Directory.GetFiles(@"D:\DataFiles\TopDown\PXD019368-HUMAN", "*(2).mzML", SearchOption.AllDirectories).ToList();
        calibTomlPath = Path.Combine(dataDirectory, "CalibrateTask.toml");
        searchTomlPaths = Directory.GetFiles(dataDirectory, "*.toml")
            .Where(p => p.Contains("Search", StringComparison.InvariantCultureIgnoreCase))
            .ToArray();
        gptmdTomlpath = Path.Combine(dataDirectory, "GPTMD.toml");
        yield return new DavidTabbDataSet(name, dataDirectory, dataFiles, databasepath, calibTomlPath, searchTomlPaths, gptmdTomlpath);


        name = @"PXD020342-DANRE";
        dataDirectory = Path.Combine(DavidTabbDirectory, "PXD020342-DANRE");
        databasepath = Path.Combine(DavidTabbDirectory, "PXD020342-DANRE",
            "Complete-uniprot-proteome_UP000000437-Cntms.fasta");
        dataFiles = Directory.GetFiles(@"D:\DataFiles\TopDown\PXD020342-DANRE", "*.raw").ToList();
        calibTomlPath = Path.Combine(dataDirectory, "CalibrateTask.toml");
        searchTomlPaths = Directory.GetFiles(dataDirectory, "*.toml")
            .Where(p => p.Contains("Search", StringComparison.InvariantCultureIgnoreCase))
            .ToArray();
        gptmdTomlpath = Path.Combine(dataDirectory, "GPTMD.toml");
        yield return new DavidTabbDataSet(name, dataDirectory, dataFiles, databasepath, calibTomlPath, searchTomlPaths, gptmdTomlpath);


        name = @"PXD031744-BOVIN";
        dataDirectory = Path.Combine(DavidTabbDirectory, "PXD031744-BOVIN");
        databasepath = Path.Combine(DavidTabbDirectory, "PXD031744-BOVIN", "20220125-Mowei-Milk-Small.fasta");
        dataFiles = Directory.GetFiles(@"D:\DataFiles\TopDown\PXD031744-BOVIN", "*.raw").ToList();
        calibTomlPath = Path.Combine(dataDirectory, "CalibrateTask.toml");
        searchTomlPaths = Directory.GetFiles(dataDirectory, "*.toml")
            .Where(p => p.Contains("Search", StringComparison.InvariantCultureIgnoreCase))
            .ToArray();
        gptmdTomlpath = Path.Combine(dataDirectory, "GPTMD.toml");
        yield return new DavidTabbDataSet(name, dataDirectory, dataFiles, databasepath, calibTomlPath, searchTomlPaths, gptmdTomlpath);
    }
}
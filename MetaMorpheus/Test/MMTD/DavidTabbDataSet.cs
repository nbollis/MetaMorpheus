using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaskLayer;

namespace Test;

public class DavidTabbDataSet
{
    public string DataSetName { get; }
    public string DataSetDirectoryPath { get; }
    public List<string> DataFilePaths { get; set; }
    public List<DbForTask> DbForTask { get; }
    public string CalibrationTomlPath { get; }
    public string GptmdTomlPath { get; }

    public string[] SearchTomlPaths { get; }


    public Dictionary<string, string> SearchTomlAndOutputDirectories { get; }

    #region Calculated Values
    public string ProcessingOutputDirectory { get; }

    public List<string> CalibDataFileNames =>
        Directory.GetFiles(Path.Combine(ProcessingOutputDirectory, "Calibration"), "*.mzML").ToList();

    public List<string> AveragedDataFileNames
    {
        get
        {
            try
            {
                return Directory.GetFiles(Path.Combine(ProcessingOutputDirectory, "SpectralAveraging"), "*.mzML").ToList();
            }
            catch (Exception e)
            {
                return new List<string>();
            }
        }
    }
        

    #endregion

    public DavidTabbDataSet(string dataSetName, string dataSetDirectoryPath, List<string> dataSetFileNames,
        string databasePath, string calibTomlPath, string[] searchTomlPaths, string gptmdTomlPath)
    {
        DataSetName = dataSetName;
        DataSetDirectoryPath = dataSetDirectoryPath;
        DataFilePaths = dataSetFileNames;
        DbForTask = new List<DbForTask>() { new (databasePath, false) };
        CalibrationTomlPath = calibTomlPath;
        SearchTomlPaths = searchTomlPaths;
        GptmdTomlPath = gptmdTomlPath;

        if (!Directory.Exists(Path.Combine(DataSetDirectoryPath, "Search Outputs")))
            Directory.CreateDirectory(Path.Combine(DataSetDirectoryPath, "Search Outputs"));

        var path = Path.Combine(DataSetDirectoryPath, "Search Outputs", "MM");
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        if (dataSetName.Contains("PXD003074-SULIS"))
        {
            if (dataSetName.Contains("HCD"))
                ProcessingOutputDirectory = Path.Combine(path, "HCDProcessedSpectra");
            else if (dataSetName.Contains("ETD"))
                ProcessingOutputDirectory = Path.Combine(path, "ETDProcessedSpectra");
        }
        else
            ProcessingOutputDirectory = Path.Combine(path, "Processing");

        SearchTomlAndOutputDirectories = new();
        foreach (var tomlPath in searchTomlPaths)
        {
            string searchName = Path.GetFileNameWithoutExtension(tomlPath)
                .Replace("Search", "");
            string searchOutputPath = Path.Combine(path, searchName);
            SearchTomlAndOutputDirectories.Add(searchOutputPath, tomlPath);
        }
    }

    public override string ToString()
    {
        return DataSetName;
    }
}
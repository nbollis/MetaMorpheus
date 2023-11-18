using YeastyBois.Models;

namespace YeastyBois.Database;

/// <summary>
/// Calls the data from the database
/// </summary>
public class YeastyBoiDataAccess
{
    private YeastyBoiDbContext context;

    public YeastyBoiDataAccess()
    {
        context = new();
    }

    public List<DataSet> GetAllDataSets()
    {
        try
        {
            List<DataSet> dataSets = context.DataSets.ToList();
            return dataSets;
        }
        catch (Exception e)
        {
            return null;
        }
    }
    public List<DataFile> GetAllDataFiles()
    {
        try
        {
            List<DataFile> dataFiles = context.DataFiles.ToList();
            return dataFiles;
        }
        catch (Exception e)
        {
            return null;
        }
    }

    public List<Results> GetAllResults()
    {
        try
        {
            List<Results> results = context.Results.ToList();
            return results;
        }
        catch (Exception e)
        {
            return null;
        }
    }
}
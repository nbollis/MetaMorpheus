using YeastyBois.Models;

namespace YeastyBois.Database;

public class YeastyBoiDataDirectClient : IYeastyBoiData
{
    private YeastyBoiDataAccess _dbAccess;
    private YeastyBoiData _data;

    public YeastyBoiDataDirectClient(bool getAllData)
    {
        _dbAccess = new();
        if (getAllData)
            Data = GetYeastyBoiData();
    }

    public YeastyBoiData Data
    {
        get => _data = GetYeastyBoiData();
        set => _data = value;
    }

    private YeastyBoiData GetYeastyBoiData()
    {
        try
        {
            YeastyBoiData data = new()
            {
                AllDataSets = new Lazy<List<DataSet>>(_dbAccess.GetAllDataSets),
                AllDataFiles = new Lazy<List<DataFile>>(_dbAccess.GetAllDataFiles),
                AllResults = new Lazy<List<Results>>(_dbAccess.GetAllResults),
            };

            return data;
        }
        catch (Exception e)
        {
            return null;
        }
    }
}
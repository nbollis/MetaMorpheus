#nullable enable
namespace TaskLayer.ParallelSearch.Util;

public enum DatabaseToProduce
{
    AllSignificantOrganisms,
    AllDetectedProteinsFromSignificantOrganisms,
    AllDetectedPeptidesFromSignificantOrganisms
}

public static class DatabaseToProduceExtension
{
    public static string GetFileName(this DatabaseToProduce mode)
    {
        return mode switch
        {
            DatabaseToProduce.AllSignificantOrganisms => "AllSignificantOrganisms.fasta",
            DatabaseToProduce.AllDetectedProteinsFromSignificantOrganisms => "AllDetectedProteinsFromSignificantOrganisms.fasta",
            DatabaseToProduce.AllDetectedPeptidesFromSignificantOrganisms => "AllProteinsFromDetectedPeptidesFromSignificantOrganisms.fasta",
            _ => "UnknownDatabase.fasta"
        };
    }

    public static string GetTaskIdText(this DatabaseToProduce mode)
    {
        return mode switch
        {
            DatabaseToProduce.AllSignificantOrganisms => "AllSignificantOrganisms",
            DatabaseToProduce.AllDetectedProteinsFromSignificantOrganisms => "AllDetectedProteinsFromSignificantOrganisms",
            DatabaseToProduce.AllDetectedPeptidesFromSignificantOrganisms => "AllProteinsFromDetectedPeptidesFromSignificantOrganisms",
            _ => "Unknown Database",
        };
    }
}

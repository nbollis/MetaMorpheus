namespace EngineLayer.FragmentTypeDetection;

/// <summary>
/// Class to hold comparison results between searches
/// </summary>
public class SearchComparisonResult
{
    public int FirstSearchPsmsAt1PercentFdr { get; set; }
    public double FirstSearchAverageScore { get; set; }
    public int SecondSearchPsmsAt1PercentFdr { get; set; }
    public double SecondSearchAverageScore { get; set; }
    public int ImprovementInPsmCount { get; set; }
    public double PercentImprovement { get; set; }
}

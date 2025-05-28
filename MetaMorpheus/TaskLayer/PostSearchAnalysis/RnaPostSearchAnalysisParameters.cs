using System.Collections.Generic;
using EngineLayer;
using Transcriptomics;

namespace TaskLayer;

public class RnaPostSearchAnalysisParameters : PostSearchAnalysisParametersParent
{

    public new RnaSearchParameters SearchParameters
    {
        get => (RnaSearchParameters)base.SearchParameters;
        set => base.SearchParameters = value;
    }
    public new List<RNA> BioPolymerList { get; set; }
}
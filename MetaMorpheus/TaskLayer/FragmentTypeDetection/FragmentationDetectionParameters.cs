using System.Collections.Generic;
using Omics.Fragmentation;

namespace TaskLayer.FragmentTypeDetection;

public class FragmentationDetectionParameters : SearchParameters
{
    public List<ProductType> IonsToSearchFor { get; set; }

    public FragmentationDetectionParameters() : base()
    {
        // Disable features not relevant for fragmentation detection
        MinAllowedInternalFragmentLength = 0;
        SearchType = SearchType.Classic;
        DoLabelFreeQuantification = false;
        DoParsimony = false;
        DoLocalizationAnalysis = false;

        // Initialize any specific parameters for fragmentation detection here
        IonsToSearchFor = new();
    }

    public FragmentationDetectionParameters(SearchParameters searchParams) : this()
    {
        CopySearchParameters(searchParams);
    }
}
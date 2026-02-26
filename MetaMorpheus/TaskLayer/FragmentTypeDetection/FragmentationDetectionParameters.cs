using System.Collections.Generic;
using EngineLayer.FragmentTypeDetection;
using Omics.Fragmentation;

namespace TaskLayer.FragmentTypeDetection;

public class FragmentationDetectionParameters : SearchParameters
{
    public List<ProductType> IonsToSearchFor { get; set; }
    public IFragmentDetectionStrategy FragmentDetectionStrategy { get; set; }

    public FragmentationDetectionParameters() : base()
    {
        IonsToSearchFor = new List<ProductType>();

        // Disable features not relevant for fragmentation detection
        MinAllowedInternalFragmentLength = 0;
        SearchType = SearchType.Classic;
        DoLabelFreeQuantification = false;
        DoParsimony = false;
        DoLocalizationAnalysis = false;
    }

    public FragmentationDetectionParameters(SearchParameters searchParams) : this()
    {
        CopySearchParameters(searchParams);
    }
}
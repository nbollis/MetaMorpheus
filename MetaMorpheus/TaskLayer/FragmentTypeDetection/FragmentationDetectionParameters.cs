using System.Collections.Generic;
using Omics.Fragmentation;

namespace TaskLayer.FragmentTypeDetection;

public class FragmentationDetectionParameters : SearchParameters
{
    List<ProductType> IonsToSearchFor { get; set; }

    public FragmentationDetectionParameters() : base()
    {
        // Initialize any specific parameters for fragmentation detection here
        IonsToSearchFor = new();
    }

    public FragmentationDetectionParameters(SearchParameters searchParams) : this()
    {
        CopySearchParameters(searchParams);
    }
}
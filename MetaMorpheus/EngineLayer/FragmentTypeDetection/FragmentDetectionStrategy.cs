using EngineLayer.FdrAnalysis;
using Omics.Fragmentation;
using System.Collections.Generic;

namespace EngineLayer.FragmentTypeDetection;
public interface IFragmentDetectionStrategy
{
    string Name { get; }
    public List<ProductType> DetermineOptimalFragmentTypes(List<SpectralMatch> allMatches, CommonParameters common);
}

public abstract class FragmentDetectionStrategy() : IFragmentDetectionStrategy
{
    public abstract string Name { get; }
    public abstract List<ProductType> DetermineOptimalFragmentTypes(List<SpectralMatch> allMatches, CommonParameters common);




}
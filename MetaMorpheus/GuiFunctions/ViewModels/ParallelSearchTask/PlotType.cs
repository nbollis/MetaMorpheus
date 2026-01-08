namespace GuiFunctions.ViewModels.ParallelSearchTask;

/// <summary>
/// Enum representing available plot types for parallel search visualization
/// </summary>
public enum PlotType
{
    /// <summary>
    /// Manhattan plot showing -log10(p-value) across databases
    /// </summary>
    ManhattanPlot,

    /// <summary>
    /// Phylogenetic tree showing taxonomic relationships with sized nodes
    /// </summary>
    PhylogeneticTree
}

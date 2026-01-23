namespace GuiFunctions.ViewModels.ParallelSearchTask.Plots;

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
    PhylogeneticTree,

    /// <summary>
    /// Detailed statistical test view showing distributions of raw values, p-values, and q-values
    /// </summary>
    StatisticalTestDetail
}

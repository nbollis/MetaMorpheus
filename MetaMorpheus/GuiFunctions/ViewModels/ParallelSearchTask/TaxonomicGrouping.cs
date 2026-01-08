namespace GuiFunctions.ViewModels.ParallelSearchTask;

/// <summary>
/// Taxonomic grouping levels for statistical visualization
/// </summary>
public enum TaxonomicGrouping
{
    /// <summary>
    /// Group by individual organism (default)
    /// </summary>
    Organism,
    
    /// <summary>
    /// Group by kingdom (e.g., Bacteria, Eukaryota)
    /// </summary>
    Kingdom,
    
    /// <summary>
    /// Group by phylum
    /// </summary>
    Phylum,
    
    /// <summary>
    /// Group by class
    /// </summary>
    Class,
    
    /// <summary>
    /// Group by order
    /// </summary>
    Order,
    
    /// <summary>
    /// Group by family
    /// </summary>
    Family,
    
    /// <summary>
    /// Group by genus
    /// </summary>
    Genus,
    
    /// <summary>
    /// Group by species
    /// </summary>
    Species,
    
    /// <summary>
    /// No grouping - all databases same color
    /// </summary>
    None
}

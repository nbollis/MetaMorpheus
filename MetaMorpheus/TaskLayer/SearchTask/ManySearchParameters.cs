#nullable enable
using System.Collections.Generic;
using EngineLayer.DatabaseLoading;

namespace TaskLayer;

public class ManySearchParameters : SearchParameters
{
    // Transient databases - one search per database in this list
    public List<DbForTask> TransientDatabases { get; set; } = new();
    public bool OverwriteTransientSearchOutputs { get; set; } = true;
    public int MaxSearchesInParallel { get; set; } = 4;
}
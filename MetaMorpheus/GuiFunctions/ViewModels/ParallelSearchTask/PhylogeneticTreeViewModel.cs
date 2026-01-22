using Easy.Common.Extensions;
using GuiFunctions.Util;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace GuiFunctions.ViewModels.ParallelSearchTask;

/// <summary>
/// ViewModel for Phylogenetic Tree visualization
/// Shows taxonomic relationships with node sizes based on statistical metrics
/// </summary>
public class PhylogeneticTreeViewModel : StatisticalPlotViewModelBase
{
    private NodeSizeMetric _nodeSizeMetric = NodeSizeMetric.SignificantHitCount;
    private TreeLayout _treeLayout = TreeLayout.Radial;
    private TaxonomicGrouping _startLevel = TaxonomicGrouping.Kingdom;
    private TaxonomicGrouping _endLevel = TaxonomicGrouping.Genus;

    public override PlotType PlotType => PlotType.PhylogeneticTree;

    public PhylogeneticTreeViewModel()
    {
        PlotTitle = "Phylogenetic Tree - Taxonomic Relationships";
    }

    #region Properties

    /// <summary>
    /// Metric used to size tree nodes
    /// </summary>
    public NodeSizeMetric NodeSizeMetric
    {
        get => _nodeSizeMetric;
        set
        {
            if (_nodeSizeMetric == value) return;
            _nodeSizeMetric = value;
            MarkDirty();
            OnPropertyChanged(nameof(NodeSizeMetric));
        }
    }

    /// <summary>
    /// Layout style for the tree
    /// </summary>
    public TreeLayout TreeLayout
    {
        get => _treeLayout;
        set
        {
            if (_treeLayout == value) return;
            _treeLayout = value;
            MarkDirty();
            OnPropertyChanged(nameof(TreeLayout));
        }
    }

    /// <summary>
    /// Starting taxonomic level for tree structure
    /// </summary>
    public TaxonomicGrouping StartLevel
    {
        get => _startLevel;
        set
        {
            if (_startLevel == value) return;
            _startLevel = value;
            MarkDirty();
            OnPropertyChanged(nameof(StartLevel));
        }
    }

    /// <summary>
    /// Ending taxonomic level for tree structure
    /// </summary>
    public TaxonomicGrouping EndLevel
    {
        get => _endLevel;
        set
        {
            if (_endLevel == value) return;
            _endLevel = value;
            MarkDirty();
            OnPropertyChanged(nameof(EndLevel));
        }
    }

    #endregion

    #region Plot Generation

    protected override PlotModel GeneratePlotModel()
    {
        var model = new PlotModel
        {
            Title = PlotTitle,
            DefaultFontSize = 12,
            IsLegendVisible = ShowLegend,
        };

        if (Results.Count == 0)
        {
            model.Subtitle = "No data available";
            return model;
        }

        // Filter results if MaxPointsToPlot is set
        var filteredResults = Results;
        if (MaxPointsToPlot > 0)
        {
            filteredResults = FilterToTopNDatabases(Results, MaxPointsToPlot);
        }

        // Build taxonomic tree structure from filtered results
        var tree = BuildTaxonomicTree(filteredResults);
        
        if (tree == null || tree.Children.Count == 0)
        {
            model.Subtitle = "No valid taxonomic data";
            return model;
        }

        // Create axes
        CreateAxes(model);

        // Calculate node positions based on layout
        CalculateNodePositions(tree);

        // Add tree visualization
        AddTreeToModel(model, tree);

        // Configure legend
        ConfigureLegend(model);

        return model;
    }

    protected override IEnumerable<string> GetExportData()
    {
        yield return "Database,Organism,Kingdom,Phylum,Class,Order,Family,Genus,Species," +
                    "SignificantHitCount,TotalHits,SignificanceRatio,MeanPValue,MeanQValue,TaxonomicLevel";

        // Filter results if MaxPointsToPlot is set (same as displayed in plot)
        var exportResults = Results;
        if (MaxPointsToPlot > 0)
        {
            exportResults = FilterToTopNDatabases(Results, MaxPointsToPlot);
        }

        for (int dbIndex = 0; dbIndex < exportResults.Count; dbIndex++)
        {
            var database = exportResults[dbIndex];
            var taxonomy = database.Taxonomy;

            if (taxonomy == null) continue;

            // Calculate metrics
            var (significantCount, totalCount) = CalculateHitCounts(database);
            double significanceRatio = totalCount > 0 ? significantCount / (double)totalCount : 0.0;
            double meanPValue = CalculateMeanPValue(database);
            double meanQValue = CalculateMeanQValue(database);

            yield return $"{database.DatabaseName},{taxonomy.Organism}," +
                        $"{taxonomy.Kingdom},{taxonomy.Phylum},{taxonomy.Class}," +
                        $"{taxonomy.Order},{taxonomy.Family},{taxonomy.Genus},{taxonomy.Species}," +
                        $"{significantCount},{totalCount},{significanceRatio:F4}," +
                        $"{meanPValue:E4},{meanQValue:E4},{GetTaxonomicGroupForResult(database)}";
        }
    }

    #endregion

    #region Tree Structure

    /// <summary>
    /// Filter databases to show only the top N most significant data points
    /// Ranks databases by their best (most significant) statistical result for the selected test
    /// </summary>
    private List<DatabaseResultViewModel> FilterToTopNDatabases(
        List<DatabaseResultViewModel> databases, 
        int maxPoints)
    {
        // Create a list with database and its best (lowest) p/q-value for the selected test
        var rankedDatabases = new List<(DatabaseResultViewModel Database, double BestValue)>();

        foreach (var database in databases)
        {
            double bestValue = double.MaxValue;
            
            var relevantResults = database.StatisticalResults
                .Where(r => string.IsNullOrEmpty(SelectedTest) || r.TestName == SelectedTest);

            foreach (var result in relevantResults)
            {
                double value = UseQValue ? result.QValue : result.PValue;
                if (!double.IsNaN(value) && value < bestValue)
                {
                    bestValue = value;
                }
            }

            // Only include if we found a valid value
            if (bestValue < double.MaxValue)
            {
                rankedDatabases.Add((database, bestValue));
            }
        }

        // Sort by best value (most significant first) and take top N
        rankedDatabases.Sort((a, b) => a.BestValue.CompareTo(b.BestValue));
        
        return rankedDatabases
            .Take(maxPoints)
            .Select(x => x.Database)
            .ToList();
    }

    /// <summary>
    /// Represents a node in the taxonomic tree
    /// </summary>
    private class TreeNode
    {
        public string Name { get; set; } = string.Empty;
        public TaxonomicGrouping Level { get; set; }
        public List<TreeNode> Children { get; set; } = new();
        public List<DatabaseResultViewModel> Databases { get; set; } = new();
        public double X { get; set; }
        public double Y { get; set; }
        public double NodeSize { get; set; }
        public int SignificantCount { get; set; }
        public int TotalCount { get; set; }
        public double MeanPValue { get; set; }

        private string? _toolTip = null;
        public string ToolTip
        {
            get => _toolTip ??= GetTooltip();
            set => _toolTip = value;
        }

        /// <summary>
        /// Generate tooltip text for this node
        /// </summary>
        private string GetTooltip()
        {
            var ratio = TotalCount > 0 ? (SignificantCount / (double)TotalCount * 100) : 0;
            var pValueStr = double.IsNaN(MeanPValue) ? "N/A" : $"{MeanPValue:E2}";

            var tooltip = $"{Name}\n" +
                         $"Level: {Level}\n" +
                         $"Significant: {SignificantCount}\n" +
                         $"Total Tests: {TotalCount}\n" +
                         $"Ratio: {ratio:F1}%\n" +
                         $"Mean P-value: {pValueStr}";

            if (Databases.Count > 0)
            {
                tooltip += $"\nDatabases: {Databases.Count}";
                if (Databases.Count <= 5)
                {
                    tooltip += "\n" + string.Join("\n", Databases.Select(d => $"  • {d.Taxonomy?.Organism ?? d.DatabaseName}"));
                }
            }

            return tooltip;
        }
    }

    /// <summary>
    /// Build hierarchical tree structure from database results
    /// </summary>
    private TreeNode? BuildTaxonomicTree()
    {
        var root = new TreeNode
        {
            Name = "Root",
            Level = TaxonomicGrouping.None
        };

        // Group databases by taxonomy levels (from StartLevel to EndLevel)
        foreach (var database in Results)
        {
            if (database.Taxonomy == null) continue;

            AddToTree(root, database);
        }

        return root;
    }

    /// <summary>
    /// Build hierarchical tree structure from filtered database results
    /// </summary>
    private TreeNode? BuildTaxonomicTree(List<DatabaseResultViewModel> databases)
    {
        var root = new TreeNode
        {
            Name = "Root",
            Level = TaxonomicGrouping.None
        };

        // Group databases by taxonomy levels (from StartLevel to EndLevel)
        foreach (var database in databases)
        {
            if (database.Taxonomy == null) continue;

            AddToTree(root, database);
        }

        return root;
    }

    /// <summary>
    /// Recursively add database to appropriate position in tree
    /// </summary>
    private void AddToTree(TreeNode parent, DatabaseResultViewModel database)
    {
        var levels = GetTaxonomicLevelsInRange();
        
        TreeNode current = parent;
        foreach (var level in levels)
        {
            string groupName = database.GetTaxonomicValueForLevel(level);
            
            if (string.IsNullOrWhiteSpace(groupName))
                groupName = "Unclassified";

            // Find or create child node
            var child = current.Children.FirstOrDefault(c => c.Name == groupName && c.Level == level);
            if (child == null)
            {
                child = new TreeNode
                {
                    Name = groupName,
                    Level = level
                };
                current.Children.Add(child);
            }

            current = child;
        }

        // Add database to leaf node
        current.Databases.Add(database);
    }

    /// <summary>
    /// Get taxonomic levels between StartLevel and EndLevel
    /// </summary>
    private List<TaxonomicGrouping> GetTaxonomicLevelsInRange()
    {
        var allLevels = new List<TaxonomicGrouping>
        {
            TaxonomicGrouping.Kingdom,
            TaxonomicGrouping.Phylum,
            TaxonomicGrouping.Class,
            TaxonomicGrouping.Order,
            TaxonomicGrouping.Family,
            TaxonomicGrouping.Genus,
            TaxonomicGrouping.Species
        };

        // Handle special cases
        if (StartLevel == TaxonomicGrouping.None || StartLevel == TaxonomicGrouping.Organism)
        {
            // Use full hierarchy if None or Organism selected
            if (EndLevel == TaxonomicGrouping.None || EndLevel == TaxonomicGrouping.Organism)
                return allLevels;
            
            int endIndex = allLevels.IndexOf(EndLevel);
            return endIndex >= 0 ? allLevels.Take(endIndex + 1).ToList() : allLevels;
        }

        if (EndLevel == TaxonomicGrouping.None || EndLevel == TaxonomicGrouping.Organism)
        {
            // Use from start to end of hierarchy
            int startIndex = allLevels.IndexOf(StartLevel);
            return startIndex >= 0 ? allLevels.Skip(startIndex).ToList() : allLevels;
        }

        // Normal case: both start and end are valid levels
        int start = allLevels.IndexOf(StartLevel);
        int end = allLevels.IndexOf(EndLevel);

        if (start < 0 || end < 0)
            return allLevels;

        // Ensure start comes before end
        if (start > end)
            (start, end) = (end, start);

        return allLevels.Skip(start).Take(end - start + 1).ToList();
    }

    /// <summary>
    /// Calculate metrics for node sizing
    /// </summary>
    private void CalculateNodeMetrics(TreeNode node)
    {
        // Aggregate metrics from databases and children
        int significantCount = 0;
        int totalCount = 0;
        double sumPValues = 0;
        int pValueCount = 0;

        // From direct databases
        foreach (var db in node.Databases)
        {
            var (sig, total) = CalculateHitCounts(db);
            significantCount += sig;
            totalCount += total;

            var meanP = CalculateMeanPValue(db);
            if (!double.IsNaN(meanP))
            {
                sumPValues += meanP;
                pValueCount++;
            }
        }

        // From children (recursive)
        foreach (var child in node.Children)
        {
            CalculateNodeMetrics(child);
            significantCount += child.SignificantCount;
            totalCount += child.TotalCount;
            sumPValues += child.MeanPValue * child.Databases.Count;
            pValueCount += child.Databases.Count;
        }

        node.SignificantCount = significantCount;
        node.TotalCount = totalCount;
        node.MeanPValue = pValueCount > 0 ? sumPValues / pValueCount : double.NaN;

        // Calculate node size based on selected metric
        node.NodeSize = CalculateNodeSizeValue(node);
    }

    /// <summary>
    /// Calculate node size based on selected metric
    /// </summary>
    private double CalculateNodeSizeValue(TreeNode node)
    {
        return NodeSizeMetric switch
        {
            NodeSizeMetric.SignificantHitCount => node.SignificantCount,
            NodeSizeMetric.TotalHitCount => node.TotalCount,
            NodeSizeMetric.SignificanceRatio => node.TotalCount > 0 ? node.SignificantCount / (double)node.TotalCount : 0,
            NodeSizeMetric.MeanSignificance => double.IsNaN(node.MeanPValue) ? 0 : -Math.Log10(node.MeanPValue),
            _ => node.SignificantCount
        };
    }

    #endregion

    #region Layout Calculation

    /// <summary>
    /// Calculate positions for all nodes based on selected layout
    /// </summary>
    private void CalculateNodePositions(TreeNode root)
    {
        // Calculate metrics first
        CalculateNodeMetrics(root);

        if (TreeLayout == TreeLayout.Radial)
        {
            CalculateRadialLayout(root);
        }
        else
        {
            CalculateHierarchicalLayout(root);
        }
    }

    /// <summary>
    /// Calculate radial (circular) layout positions
    /// </summary>
    private void CalculateRadialLayout(TreeNode root)
    {
        root.X = 0;
        root.Y = 0;

        CalculateRadialPositions(root, 0, 2 * Math.PI, 1);
    }

    private void CalculateRadialPositions(TreeNode node, double startAngle, double endAngle, int depth)
    {
        if (node.Children.Count == 0) return;

        double angleRange = endAngle - startAngle;
        double angleStep = angleRange / node.Children.Count;
        double currentAngle = startAngle;

        double radius = depth * 10.0; // Distance from center increases with depth

        foreach (var child in node.Children)
        {
            double midAngle = currentAngle + angleStep / 2;
            
            child.X = node.X + radius * Math.Cos(midAngle);
            child.Y = node.Y + radius * Math.Sin(midAngle);

            // Recurse with this child's angle range
            CalculateRadialPositions(child, currentAngle, currentAngle + angleStep, depth + 1);
            
            currentAngle += angleStep;
        }
    }

    /// <summary>
    /// Calculate hierarchical (left-to-right) layout positions
    /// </summary>
    private void CalculateHierarchicalLayout(TreeNode root)
    {
        root.X = 0;
        root.Y = 0;

        CalculateHierarchicalPositions(root, 0, 0);
    }

    private int CalculateHierarchicalPositions(TreeNode node, int depth, int verticalPosition)
    {
        node.X = depth * 20.0; // Horizontal spacing
        node.Y = verticalPosition * 5.0; // Vertical spacing

        int currentY = verticalPosition;
        foreach (var child in node.Children)
        {
            currentY = CalculateHierarchicalPositions(child, depth + 1, currentY);
            currentY++;
        }

        // Center parent node among children
        if (node.Children.Count > 0)
        {
            double minChildY = node.Children.Min(c => c.Y);
            double maxChildY = node.Children.Max(c => c.Y);
            node.Y = (minChildY + maxChildY) / 2.0;
        }

        return currentY;
    }

    #endregion

    #region Visualization

    /// <summary>
    /// Add tree visualization to plot model
    /// </summary>
    private void AddTreeToModel(PlotModel model, TreeNode root)
    {
        // Add edges (lines connecting nodes)
        var edgeSeries = new LineSeries
        {
            LineStyle = LineStyle.Solid,
            Color = OxyColors.LightGray,
            StrokeThickness = 1,
            CanTrackerInterpolatePoints = false
        };

        AddTreeEdges(root, edgeSeries);
        model.Series.Add(edgeSeries);

        // Add nodes (scatter points)
        AddTreeNodes(model, root);
    }

    /// <summary>
    /// Recursively add edges to line series
    /// </summary>
    private void AddTreeEdges(TreeNode node, LineSeries edgeSeries)
    {
        foreach (var child in node.Children)
        {
            edgeSeries.Points.Add(new DataPoint(node.X, node.Y));
            edgeSeries.Points.Add(new DataPoint(child.X, child.Y));
            edgeSeries.Points.Add(DataPoint.Undefined); // Break in line

            AddTreeEdges(child, edgeSeries);
        }
    }

    /// <summary>
    /// Add nodes as scatter points with varying sizes
    /// </summary>
    private void AddTreeNodes(PlotModel model, TreeNode root)
    {
        // Collect all nodes
        var allNodes = new List<TreeNode>();
        CollectNodes(root, allNodes);

        // Find min/max sizes for scaling
        double minSize = allNodes.Min(n => n.NodeSize);
        double maxSize = allNodes.Max(n => n.NodeSize);
        double sizeRange = maxSize - minSize;
        if (sizeRange < 0.001) sizeRange = 1.0;

        // Create series for each taxonomic level
        var seriesByLevel = new Dictionary<TaxonomicGrouping, ScatterSeries>();

        foreach (var node in allNodes)
        {
            // Scale node size for visibility (3-15 range)
            double normalizedSize = (node.NodeSize - minSize) / sizeRange;
            double markerSize = 3 + normalizedSize * 12;

            // Create scatter point with tooltip
            var point = new ScatterPoint(node.X, node.Y, markerSize, node.NodeSize, node.ToolTip);

            if (!seriesByLevel.ContainsKey(node.Level))
            {
                seriesByLevel[node.Level] = new ScatterSeries
                {
                    Title = node.Level.ToString(),
                    MarkerType = MarkerType.Circle,
                };
            }

            var series = seriesByLevel[node.Level];
            series.Points.Add(point);
        }

        foreach (var series in seriesByLevel.Values)
        {
            series.TrackerFormatString = "{Tag}";
            model.Series.Add(series);
        }
    }

    /// <summary>
    /// Collect all nodes from tree
    /// </summary>
    private void CollectNodes(TreeNode node, List<TreeNode> collection)
    {
        collection.Add(node);
        foreach (var child in node.Children)
        {
            CollectNodes(child, collection);
        }
    }

    private class CustomTrackerHitResult : TrackerHitResult
    {
        public CustomTrackerHitResult(ScatterPoint point)
        {
            Text = point.Tag?.ToString() ?? $"X: {point.X:F2}\nY: {point.Y:F2}";
            Position = new ScreenPoint(point.X, point.Y);
        }
    }

    #endregion

    #region Helper Methods

    private void CreateAxes(PlotModel model)
    {
        var xAxis = new LinearAxis
        {
            Position = AxisPosition.Bottom,
            IsAxisVisible = false
        };
        model.Axes.Add(xAxis);

        var yAxis = new LinearAxis
        {
            Position = AxisPosition.Left,
            IsAxisVisible = false
        };
        model.Axes.Add(yAxis);

        model.Padding = new OxyThickness(20);
    }

    private (int Significant, int Total) CalculateHitCounts(DatabaseResultViewModel database)
    {
        int significant = 0;
        int total = 0;

        var statisticalResults = database.StatisticalResults;
        for (int i = 0; i < statisticalResults.Count; i++)
        {
            var result = statisticalResults[i];
            
            if (!string.IsNullOrEmpty(SelectedTest) && result.TestName != SelectedTest)
                continue;

            total++;
            if (result.IsSignificant(Alpha, UseQValue))
                significant++;
        }

        return (significant, total);
    }

    private double CalculateMeanPValue(DatabaseResultViewModel database)
    {
        double sum = 0;
        int count = 0;

        var statisticalResults = database.StatisticalResults;
        for (int i = 0; i < statisticalResults.Count; i++)
        {
            var result = statisticalResults[i];
            
            if (!string.IsNullOrEmpty(SelectedTest) && result.TestName != SelectedTest)
                continue;

            double value = UseQValue ? result.QValue : result.PValue;
            if (!double.IsNaN(value))
            {
                sum += value;
                count++;
            }
        }

        return count > 0 ? sum / count : double.NaN;
    }

    private double CalculateMeanQValue(DatabaseResultViewModel database)
    {
        double sum = 0;
        int count = 0;

        var statisticalResults = database.StatisticalResults;
        for (int i = 0; i < statisticalResults.Count; i++)
        {
            var result = statisticalResults[i];
            
            if (!string.IsNullOrEmpty(SelectedTest) && result.TestName != SelectedTest)
                continue;

            if (!double.IsNaN(result.QValue))
            {
                sum += result.QValue;
                count++;
            }
        }

        return count > 0 ? sum / count : double.NaN;
    }

    private void ConfigureLegend(PlotModel model)
    {
        if (ShowLegend)
        {
            model.IsLegendVisible = true;
            model.LegendPosition = LegendPosition.RightTop;
            model.LegendPlacement = LegendPlacement.Inside;
            model.LegendOrientation = LegendOrientation.Vertical;
        }
    }

    #endregion
}

/// <summary>
/// Metric used to determine node size in phylogenetic tree
/// </summary>
public enum NodeSizeMetric
{
    SignificantHitCount,
    TotalHitCount,
    SignificanceRatio,
    MeanSignificance
}

/// <summary>
/// Layout style for phylogenetic tree
/// </summary>
public enum TreeLayout
{
    Radial,
    Hierarchical
}

using System;
using System.Collections.Generic;
using System.Linq;
using GuiFunctions.Util;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace GuiFunctions.ViewModels.ParallelSearchTask;

/// <summary>
/// ViewModel for Manhattan plot visualization
/// Shows -log10(p-value) or -log10(q-value) across databases
/// Colored by significance threshold
/// </summary>
public class ManhattanPlotViewModel : StatisticalPlotViewModelBase
{

    // Color queue for taxonomic grouping - similar to DeconvolutionPlot
    private static readonly CyclicalQueue<OxyColor> ColorQueue = new CyclicalQueue<OxyColor>(new[]
    {
        OxyColors.Red, OxyColors.Blue, OxyColors.Green, OxyColors.Orange, OxyColors.Purple,
        OxyColors.Teal, OxyColors.Brown, OxyColors.Pink, OxyColors.Yellow, OxyColors.Gray,
        OxyColors.Cyan, OxyColors.Magenta, OxyColors.LimeGreen, OxyColors.DarkBlue, OxyColors.DarkRed,
        OxyColors.DarkGreen, OxyColors.Gold, OxyColors.Indigo, OxyColors.Olive, OxyColors.Maroon,
        OxyColors.Navy, OxyColors.Turquoise, OxyColors.Violet, OxyColors.Sienna, OxyColors.Salmon,
        OxyColors.Coral, OxyColors.Khaki, OxyColors.Plum, OxyColors.Peru, OxyColors.SteelBlue,
        OxyColors.MediumPurple, OxyColors.MediumSeaGreen, OxyColors.MediumSlateBlue, OxyColors.MediumVioletRed,
        OxyColors.MediumOrchid, OxyColors.MediumTurquoise, OxyColors.MediumSpringGreen, OxyColors.MediumAquamarine
    });

    public ManhattanPlotViewModel()
    {
        PlotTitle = "Manhattan Plot - Statistical Significance Across Databases";
    }

    #region Plot Generation

    protected override PlotModel GeneratePlotModel()
    {
        var model = new PlotModel
        {
            Title = PlotTitle,
            DefaultFontSize = 12,
            IsLegendVisible = false,
        };

        if (Results.Count == 0)
        {
            model.Subtitle = "No data available";
            return model;
        }

        // Reset color queue for consistent coloring
        ColorQueue.Reset();

        // Prepare data structures
        var (seriesByGroup, groupColors, significantCountsByGroup, groupPositions, sortedDatabases, totalPoints) = 
            PrepareDataStructures();

        if (totalPoints == 0)
        {
            model.Subtitle = "No valid data points";
            return model;
        }

        // Determine which groups to show
        var groupsToShowInLegend = DetermineTopNGroups(significantCountsByGroup);

        // Create and configure axes
        CreateAxes(model, groupPositions, groupsToShowInLegend, totalPoints);

        // Create and add series
        AddSeriesToModel(model, seriesByGroup, groupsToShowInLegend, sortedDatabases);

        // Add significance threshold annotation
        AddSignificanceThreshold(model);

        // Configure legend
        ConfigureLegend(model);

        return model;
    }

    protected override IEnumerable<string> GetExportData()
    {
        yield return "Database,TestName,MetricName,PValue,QValue,NegLog10PValue,NegLog10QValue,IsSignificant,TaxonomicGroup";

        // Cache taxonomy lookups to avoid repeated calls
        var taxonomyCache = new Dictionary<DatabaseResultViewModel, string>(Results.Count);

        for (int dbIndex = 0; dbIndex < Results.Count; dbIndex++)
        {
            var database = Results[dbIndex];

            // Get or cache taxonomy group
            if (!taxonomyCache.TryGetValue(database, out var taxonomicGroup))
            {
                taxonomicGroup = GetTaxonomicGroupForResult(database);
                taxonomyCache[database] = taxonomicGroup;
            }

            var statisticalResults = database.StatisticalResults;
            for (int resIndex = 0; resIndex < statisticalResults.Count; resIndex++)
            {
                var result = statisticalResults[resIndex];
                var negLogP = CalculateNegativeLog10(result.PValue);
                var negLogQ = CalculateNegativeLog10(result.QValue);
                var isSignificant = result.IsSignificant(Alpha, UseQValue);

                yield return $"{database.DatabaseName},{result.TestName},{result.MetricName}," +
                            $"{result.PValue},{result.QValue},{negLogP},{negLogQ},{isSignificant},{taxonomicGroup}";
            }
        }
    }

    #endregion

    #region Data Preparation Methods

    /// <summary>
    /// Prepare all data structures needed for plotting
    /// </summary>
    private (Dictionary<string, ScatterSeries> SeriesByGroup,
             Dictionary<string, OxyColor> GroupColors,
             Dictionary<string, int> SignificantCountsByGroup,
             Dictionary<string, (int Start, int End)> GroupPositions,
             List<DatabaseResultViewModel> SortedDatabases,
             int TotalPoints) PrepareDataStructures()
    {
        var seriesByGroup = new Dictionary<string, ScatterSeries>();
        var groupColors = new Dictionary<string, OxyColor>();
        var significantCountsByGroup = new Dictionary<string, int>();
        var groupPositions = new Dictionary<string, (int Start, int End)>();

        // Sort databases by taxonomic grouping
        var sortedDatabases = SortDatabasesByTaxonomy();

        // Build series and track group positions
        int totalPoints = BuildSeriesAndTrackPositions(
            sortedDatabases,
            seriesByGroup,
            groupColors,
            significantCountsByGroup,
            groupPositions);

        return (seriesByGroup, groupColors, significantCountsByGroup, groupPositions, sortedDatabases, totalPoints);
    }

    /// <summary>
    /// Sort databases by taxonomic grouping for ordered display
    /// </summary>
    private List<DatabaseResultViewModel> SortDatabasesByTaxonomy()
    {
        if (GroupBy == TaxonomicGrouping.None)
        {
            return Results;
        }

        var sortedDatabases = new List<DatabaseResultViewModel>(Results.Count);
        sortedDatabases.AddRange(Results);
        sortedDatabases.Sort((a, b) =>
        {
            var groupA = GetTaxonomicGroupForResult(a);
            var groupB = GetTaxonomicGroupForResult(b);

            // First sort by taxonomic group
            int groupCompare = string.Compare(groupA, groupB, StringComparison.Ordinal);
            if (groupCompare != 0)
                return groupCompare;

            // Then by database name within group
            return string.Compare(a.DatabaseName, b.DatabaseName, StringComparison.Ordinal);
        });

        return sortedDatabases;
    }

    /// <summary>
    /// Build scatter series for each taxonomic group and track their positions
    /// </summary>
    private int BuildSeriesAndTrackPositions(
        List<DatabaseResultViewModel> sortedDatabases,
        Dictionary<string, ScatterSeries> seriesByGroup,
        Dictionary<string, OxyColor> groupColors,
        Dictionary<string, int> significantCountsByGroup,
        Dictionary<string, (int Start, int End)> groupPositions)
    {
        int pointIndex = 0;
        int currentGroupStart = 0;
        string? lastGroup = null;

        for (int dbIndex = 0; dbIndex < sortedDatabases.Count; dbIndex++)
        {
            var database = sortedDatabases[dbIndex];
            var taxonomicGroup = GetTaxonomicGroupForResult(database);

            // Track group boundaries for X-axis labels
            if (GroupBy != TaxonomicGrouping.None && lastGroup != taxonomicGroup)
            {
                if (lastGroup != null && currentGroupStart < pointIndex)
                {
                    groupPositions[lastGroup] = (currentGroupStart, pointIndex - 1);
                }
                currentGroupStart = pointIndex;
                lastGroup = taxonomicGroup;
            }

            // Process statistical results for this database
            pointIndex = ProcessDatabaseResults(
                database,
                taxonomicGroup,
                pointIndex,
                seriesByGroup,
                groupColors,
                significantCountsByGroup);
        }

        // Record final group position
        if (GroupBy != TaxonomicGrouping.None && lastGroup != null && currentGroupStart < pointIndex)
        {
            groupPositions[lastGroup] = (currentGroupStart, pointIndex - 1);
        }

        return pointIndex;
    }

    /// <summary>
    /// Process statistical results for a single database
    /// </summary>
    private int ProcessDatabaseResults(
        DatabaseResultViewModel database,
        string taxonomicGroup,
        int startPointIndex,
        Dictionary<string, ScatterSeries> seriesByGroup,
        Dictionary<string, OxyColor> groupColors,
        Dictionary<string, int> significantCountsByGroup)
    {
        int pointIndex = startPointIndex;
        var statisticalResults = database.StatisticalResults;

        for (int resIndex = 0; resIndex < statisticalResults.Count; resIndex++)
        {
            var result = statisticalResults[resIndex];

            // Filter by selected test
            if (!string.IsNullOrEmpty(SelectedTest) && result.TestName != SelectedTest)
                continue;

            var negLog = UseQValue ? result.NegLog10QValue : result.NegLog10PValue;
            if (double.IsNaN(negLog))
                continue;

            // Get or create series for this group
            var series = GetOrCreateSeriesForGroup(taxonomicGroup, seriesByGroup, groupColors, significantCountsByGroup);

            string toolTip = $"{database.OrganismName}\n{(UseQValue ? "-log10(Q-value)" : " - log10(P - value)")}: {(UseQValue ? result.NegLog10QValue : result.NegLog10PValue):F3}";

            // Add point to series
            series.Points.Add(new ScatterPoint(pointIndex, negLog, tag: toolTip));

            // Track significant counts
            if (result.IsSignificant(Alpha, UseQValue))
            {
                significantCountsByGroup[taxonomicGroup]++;
            }

            pointIndex++;
        }

        return pointIndex;
    }

    /// <summary>
    /// Get existing series for a group or create a new one
    /// </summary>
    private ScatterSeries GetOrCreateSeriesForGroup(
        string taxonomicGroup,
        Dictionary<string, ScatterSeries> seriesByGroup,
        Dictionary<string, OxyColor> groupColors,
        Dictionary<string, int> significantCountsByGroup)
    {
        if (seriesByGroup.TryGetValue(taxonomicGroup, out var series))
        {
            return series;
        }

        // Create new series
        series = new ScatterSeries
        {
            MarkerType = MarkerType.Circle,
            MarkerSize = 5,
            TrackerFormatString = "{Tag}"
        };
        seriesByGroup[taxonomicGroup] = series;
        significantCountsByGroup[taxonomicGroup] = 0;

        // Assign color
        var color = AssignColorForGroup(taxonomicGroup);
        groupColors[taxonomicGroup] = color;
        series.MarkerFill = color;
        series.MarkerStroke = color;

        return series;
    }

    /// <summary>
    /// Assign appropriate color for a taxonomic group
    /// </summary>
    private OxyColor AssignColorForGroup(string taxonomicGroup)
    {
        if (string.IsNullOrEmpty(taxonomicGroup) || 
            taxonomicGroup == "Unclassified" || 
            taxonomicGroup == "Unknown")
        {
            return OxyColors.LightGray;
        }

        return ColorQueue.Dequeue();
    }

    /// <summary>
    /// Determine which groups should appear in legend based on Top N filtering
    /// </summary>
    private HashSet<string>? DetermineTopNGroups(Dictionary<string, int> significantCountsByGroup)
    {
        if (GroupBy == TaxonomicGrouping.None || TopNGroups <= 0)
        {
            return null; // Show all groups
        }

        // Sort groups by significant count and take top N
        var sortedGroups = new List<(string Group, int Count)>(significantCountsByGroup.Count);
        foreach (var kvp in significantCountsByGroup)
        {
            if (kvp.Value > 0)
                sortedGroups.Add((kvp.Key, kvp.Value));
        }

        sortedGroups.Sort((a, b) => b.Count.CompareTo(a.Count));

        var groupsToShow = new HashSet<string>(TopNGroups);
        int countToTake = Math.Min(TopNGroups, sortedGroups.Count);
        for (int i = 0; i < countToTake; i++)
        {
            groupsToShow.Add(sortedGroups[i].Group);
        }

        return groupsToShow;
    }

    #endregion

    #region Axis Creation Methods

    /// <summary>
    /// Create and add axes to the plot model
    /// </summary>
    private void CreateAxes(
        PlotModel model,
        Dictionary<string, (int Start, int End)> groupPositions,
        HashSet<string>? groupsToShowInLegend,
        int totalPoints)
    {
        // Create X-axis based on grouping mode
        if (GroupBy == TaxonomicGrouping.None)
        {
            CreateNumericXAxis(model);
        }
        else
        {
            CreateTaxonomicXAxis(model, groupPositions, groupsToShowInLegend, totalPoints);
        }

        // Create Y-axis
        CreateYAxis(model);

        // Set model padding
        model.Padding = new OxyThickness(20, 10, 10, 50);
    }

    /// <summary>
    /// Create a simple numeric X-axis for non-taxonomic grouping
    /// </summary>
    private void CreateNumericXAxis(PlotModel model)
    {
        var xAxisTitle = GetXAxisTitle();
        var linearXAxis = new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = xAxisTitle,
            MinimumPadding = 0.02,
            MaximumPadding = 0.02
        };
        model.Axes.Add(linearXAxis);
    }

    /// <summary>
    /// Create X-axis with taxonomic group labels
    /// </summary>
    private void CreateTaxonomicXAxis(
        PlotModel model,
        Dictionary<string, (int Start, int End)> groupPositions,
        HashSet<string>? groupsToShowInLegend,
        int totalPoints)
    {
        var xAxisTitle = GetXAxisTitle();
        var linearXAxis = new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = xAxisTitle,
            MinimumPadding = 0.02,
            MaximumPadding = 0.02
        };

        // Create tick positions and labels for groups in legend
        var (tickPositions, tickLookup) = CreateTickPositionsAndLabels(groupPositions, groupsToShowInLegend);

        // Configure axis appearance
        ConfigureAxisAppearance(linearXAxis, tickPositions, totalPoints);

        // Generate the actual ticks that OxyPlot will display based on MajorStep
        List<double> actualTicks = new();
        for (double tick = 0; tick < totalPoints; tick += linearXAxis.MajorStep)
        {
            actualTicks.Add(tick);
        }

        // For each group midpoint, find the closest actual tick position
        // This creates a mapping: actualTick -> groupLabel
        var tickToLabelMap = new Dictionary<double, string>();

        foreach (var midpoint in tickPositions)
        {
            // Find the closest actual tick to this group midpoint
            double closestTick = actualTicks[0];
            double minDistance = Math.Abs(actualTicks[0] - midpoint);

            for (int i = 1; i < actualTicks.Count; i++)
            {
                double distance = Math.Abs(actualTicks[i] - midpoint);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestTick = actualTicks[i];
                }
            }

            // Map this tick to the group label (only if not already mapped to prevent duplicates)
            if (!tickToLabelMap.ContainsKey(closestTick) && tickLookup.TryGetValue(midpoint, out var label))
            {
                tickToLabelMap[closestTick] = label;
            }
        }

        // Configure label formatter to use the pre-computed mapping
        const double epsilon = 0.01;
        linearXAxis.LabelFormatter = value =>
        {
            if (value < 0)
                return "";

            // Check if this value matches one of our labeled ticks
            foreach (var kvp in tickToLabelMap)
            {
                if (Math.Abs(value - kvp.Key) < epsilon)
                {
                    return kvp.Value;
                }
            }

            return "";
        };

        model.Axes.Add(linearXAxis);
    }

    /// <summary>
    /// Create tick positions and labels for taxonomic groups
    /// </summary>
    private (List<double> TickPositions, Dictionary<double, string> TickLookup) CreateTickPositionsAndLabels(
        Dictionary<string, (int Start, int End)> groupPositions,
        HashSet<string>? groupsToShowInLegend)
    {
        var tickPositions = new List<double>();
        var tickLookup = new Dictionary<double, string>();

        // Sort groups by their X-axis position
        var sortedGroupPositions = groupPositions.OrderBy(kvp => kvp.Value.Start).ToList();

        foreach (var kvp in sortedGroupPositions)
        {
            var groupName = kvp.Key;

            // Only add label if this group is in the legend (or showing all)
            bool shouldLabel = groupsToShowInLegend == null || groupsToShowInLegend.Contains(groupName);

            if (shouldLabel)
            {
                var (start, end) = kvp.Value;
                var midpoint = (start + end) / 2.0;

                tickPositions.Add(midpoint);
                tickLookup[midpoint] = string.IsNullOrEmpty(groupName) ? "Unclassified" : groupName;
            }
        }

        return (tickPositions, tickLookup);
    }

    /// <summary>
    /// Configure axis appearance with explicit tick positions
    /// </summary>
    private void ConfigureAxisAppearance(LinearAxis linearXAxis, List<double> tickPositions, int totalPoints)
    {
        linearXAxis.TickStyle = OxyPlot.Axes.TickStyle.Outside;
        linearXAxis.MajorTickSize = 7;
        linearXAxis.MinorTickSize = 0;
        linearXAxis.AxislineStyle = LineStyle.Solid;
        linearXAxis.AxislineThickness = 1;

        // Rotate labels for readability
        linearXAxis.Angle = 30;
        linearXAxis.FontSize = 12;

        // Use explicit tick positions by manually setting them
        if (tickPositions.Count > 0)
        {
            // Create a custom tick generator that uses our exact positions
            // Set the interval to ensure OxyPlot evaluates at our positions
            linearXAxis.IntervalLength = 0; // Disable automatic interval calculation
            
            // Calculate the smallest gap between our tick positions
            double minGap = double.MaxValue;
            for (int i = 1; i < tickPositions.Count; i++)
            {
                double gap = tickPositions[i] - tickPositions[i - 1];
                if (gap < minGap)
                    minGap = gap;
            }
            
            // Set MajorStep to a fraction of the minimum gap to ensure all our positions are hit
            linearXAxis.MajorStep = minGap / 2.0;
            
            // Ensure at least step of 1
            if (linearXAxis.MajorStep < 10)
                linearXAxis.MajorStep = 10;
            if (linearXAxis.MajorStep > 100)
                linearXAxis.MajorStep = 100;
        }
    }

    /// <summary>
    /// Create Y-axis for -log10(p-value) or -log10(q-value)
    /// </summary>
    private void CreateYAxis(PlotModel model)
    {
        var yAxisTitle = UseQValue ? "-log10(Q-value)" : "-log10(P-value)";
        var valueAxis = CreateLinearAxis(yAxisTitle, AxisPosition.Left, minimum: 0);
        model.Axes.Add(valueAxis);
    }

    /// <summary>
    /// Get X-axis title based on grouping level
    /// </summary>
    private string GetXAxisTitle()
    {
        return GroupBy switch
        {
            TaxonomicGrouping.None => "Database",
            TaxonomicGrouping.Organism => "Organism",
            TaxonomicGrouping.Kingdom => "Kingdom",
            TaxonomicGrouping.Phylum => "Phylum",
            TaxonomicGrouping.Class => "Class",
            TaxonomicGrouping.Order => "Order",
            TaxonomicGrouping.Family => "Family",
            TaxonomicGrouping.Genus => "Genus",
            TaxonomicGrouping.Species => "Species",
            _ => "Database"
        };
    }

    #endregion

    #region Series Creation Methods

    /// <summary>
    /// Add series to the plot model based on grouping mode
    /// </summary>
    private void AddSeriesToModel(
        PlotModel model,
        Dictionary<string, ScatterSeries> seriesByGroup,
        HashSet<string>? groupsToShowInLegend,
        List<DatabaseResultViewModel> sortedDatabases)
    {
        if (GroupBy == TaxonomicGrouping.None)
        {
            var significantSeries = new ScatterSeries
            {
                Title = "Significant",
                MarkerType = MarkerType.Circle,
                MarkerSize = 6,
                MarkerFill = OxyColors.Red
            };

            var nonSignificantSeries = new ScatterSeries
            {
                Title = "Non-significant",
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerFill = OxyColors.Gray
            };

            // Classify points by significance
            int pointIndex = 0;
            for (int dbIndex = 0; dbIndex < sortedDatabases.Count; dbIndex++)
            {
                var database = sortedDatabases[dbIndex];
                var statisticalResults = database.StatisticalResults;

                for (int resIndex = 0; resIndex < statisticalResults.Count; resIndex++)
                {
                    var result = statisticalResults[resIndex];

                    if (!string.IsNullOrEmpty(SelectedTest) && result.TestName != SelectedTest)
                        continue;

                    var negLog = UseQValue ? result.NegLog10QValue : result.NegLog10PValue;
                    if (double.IsNaN(negLog))
                        continue;

                    var scatterPoint = new ScatterPoint(pointIndex, negLog);

                    if (result.IsSignificant(Alpha, UseQValue))
                        significantSeries.Points.Add(scatterPoint);
                    else
                        nonSignificantSeries.Points.Add(scatterPoint);

                    pointIndex++;
                }
            }

            model.Series.Add(significantSeries);
            model.Series.Add(nonSignificantSeries);
        }
        else
        {
            // Sort series by group name for consistent legend ordering
            var sortedSeries = seriesByGroup.OrderBy(kvp => kvp.Key).ToList();

            foreach (var kvp in sortedSeries)
            {
                var groupKey = kvp.Key;
                var series = kvp.Value;

                bool showInLegend = groupsToShowInLegend == null || groupsToShowInLegend.Contains(groupKey);

                if (showInLegend)
                {
                    series.Title = string.IsNullOrEmpty(groupKey) ? "Unclassified" : groupKey;
                }
                // else Title stays null, hiding it from legend

                model.Series.Add(series);
            }
        }
    }

    #endregion

    #region Annotation and Legend Methods

    /// <summary>
    /// Add significance threshold line annotation
    /// </summary>
    private void AddSignificanceThreshold(PlotModel model)
    {
        double thresholdLine = CalculateNegativeLog10(Alpha);
        if (!double.IsNaN(thresholdLine))
        {
            model.Annotations.Add(new LineAnnotation
            {
                Type = LineAnnotationType.Horizontal,
                Y = thresholdLine,
                Color = OxyColors.Red,
                LineStyle = LineStyle.Dash,
                Text = $"α = {Alpha}",
                TextColor = OxyColors.Red
            });
        }
    }

    /// <summary>
    /// Configure legend visibility and placement
    /// </summary>
    private void ConfigureLegend(PlotModel model)
    {
        if (ShowLegend)
        {
            model.IsLegendVisible = true;
            model.LegendPosition = LegendPosition.TopCenter;
            model.LegendPlacement = LegendPlacement.Outside;
            model.LegendOrientation = LegendOrientation.Horizontal;
            model.LegendItemAlignment = HorizontalAlignment.Left;
        }
    }

    #endregion
}

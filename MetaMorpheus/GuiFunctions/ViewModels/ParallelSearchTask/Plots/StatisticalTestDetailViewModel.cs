using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using TaskLayer.ParallelSearch.Statistics;
using TaskLayer.ParallelSearch.Util;

namespace GuiFunctions.ViewModels.ParallelSearchTask.Plots;

/// <summary>
/// ViewModel for detailed statistical test visualization
/// Shows distributions of raw values, p-values, and q-values for a specific test
/// </summary>
public class StatisticalTestDetailViewModel : StatisticalPlotViewModelBase
{
    private static int MinBinCount = 5;
    private static int MaxBinCount = 200;
    private const int MaxRenderedQqPoints = 4000;
    private const int MaxRenderedVolcanoPointsPerSeries = 4000;

    private List<StatisticalTestResult> _allStatisticalResults = new();
    private Dictionary<string, List<StatisticalTestResult>> _resultsBySelectionKey = new(StringComparer.Ordinal);
    private TestSummary _testSummary;
    private int _binCount = 20;
    private bool _useLogScale = false;
    private List<StatisticalTestResult> _selectedTestResults = new();
    private List<SelectedTestDisplayPoint> _selectedDisplayPoints = new();
    private List<double> _selectedRawValues = new();
    private List<double> _selectedPValues = new();
    private List<double> _selectedQValues = new();
    private PlotModel? _cachedRawPlotModel;
    private PlotModel? _cachedPValuePlotModel;
    private PlotModel? _cachedQValuePlotModel;
    private PlotModel? _cachedQQPlotModel;
    private PlotModel? _cachedVolcanoPlotModel;
    private string _cachedRawPlotKey = string.Empty;
    private string _cachedPValuePlotKey = string.Empty;
    private string _cachedQValuePlotKey = string.Empty;
    private string _cachedQQPlotKey = string.Empty;
    private string _cachedVolcanoPlotKey = string.Empty;

    public override PlotType PlotType => PlotType.StatisticalTestDetail;

    public PlotModel QQPlotModel => BuildQQPlot();

    public PlotModel VolcanoPlotModel => BuildVolcanoPlot();

    public StatisticalTestDetailViewModel()
    {
        PlotTitle = "Statistical Test Detail";
    }

    #region Properties

    /// <summary>
    /// Number of bins for histogram display
    /// </summary>
    public int BinCount
    {
        get => _binCount;
        set
        {
            if (_binCount == value) return;
            _binCount = value < MinBinCount ? MinBinCount : value > MaxBinCount ? MaxBinCount : value; // Clamp between MinBinCount and MaxBinCount
            InvalidateHistogramPlotCaches();
            MarkDirty();
            OnPropertyChanged(nameof(BinCount));
            NotifyHistogramPlotModelPropertiesChanged();
        }
    }

    /// <summary>
    /// Whether to use logarithmic scale for Y-axis
    /// </summary>
    public bool UseLogScale
    {
        get => _useLogScale;
        set
        {
            if (_useLogScale == value) return;
            _useLogScale = value;
            InvalidateHistogramPlotCaches();
            MarkDirty();
            OnPropertyChanged(nameof(UseLogScale));
            NotifyHistogramPlotModelPropertiesChanged();
        }
    }

    public new double Alpha
    {
        get => base.Alpha;
        set
        {
            if (Math.Abs(base.Alpha - value) < 0.00001)
            {
                return;
            }

            base.Alpha = value;
            InvalidateAlphaDependentPlotCaches();
            UpdateTestSummary();
            NotifyAlphaDependentPlotModelPropertiesChanged();
        }
    }

    /// <summary>
    /// Currently selected test name for detailed view
    /// </summary>
    public override string SelectedTest
    {
        get => _selectedTest;
        set
        {
            if (_selectedTest == value) return;
            _selectedTest = value;

            UpdateSelectedTestCaches();
            UpdateTestSummary();
            UpdateSelectedTestForResults();
            UpdateTopNResults();
            MarkDirty();
            OnPropertyChanged(nameof(SelectedTest));
            OnPropertyChanged(nameof(TestSummary));
            OnPropertyChanged(nameof(PlotModel));
            NotifyDetailPlotModelPropertiesChanged();
        }
    }

    /// <summary>
    /// All statistical results from all databases
    /// </summary>
    public List<StatisticalTestResult> AllStatisticalResults
    {
        get => _allStatisticalResults;
        set
        {
            var newResults = value ?? [];
            if (ReferenceEquals(_allStatisticalResults, newResults))
            {
                return;
            }

            _allStatisticalResults = newResults;
            _resultsBySelectionKey = BuildSelectionIndex(newResults);
            UpdateSelectedTestCaches();
            UpdateTestSummary();
            MarkDirty();
            OnPropertyChanged(nameof(AllStatisticalResults));
            OnPropertyChanged(nameof(TestSummary));
            OnPropertyChanged(nameof(PlotModel));
            NotifyDetailPlotModelPropertiesChanged();
        }
    }

    /// <summary>
    /// Summary statistics for the selected test
    /// </summary>
    public TestSummary TestSummary
    {
        get => _testSummary;
        private set
        {
            _testSummary = value;
            OnPropertyChanged(nameof(TestSummary));
        }
    }

    #endregion

    #region Plot Generation

    protected override PlotModel GeneratePlotModel()
    {
        // The detail control binds directly to the individual plot-model properties below.
        // Returning one of them keeps export behavior working without triggering a re-entrant
        // property-change cascade while PlotModel itself is being generated.
        return RawValuePlotModel;
    }

    protected override IEnumerable<string> GetExportData()
    {
        yield return "DatabaseName,TestName,MetricName,RawValue,PValue,QValue,IsSignificantByP,IsSignificantByQ";

        foreach (var result in _selectedTestResults)
        {
            var rawValue = ExtractRawValue(result);
            var isSignificantByP = result.PValue <= Alpha;
            var isSignificantByQ = result.QValue <= Alpha;

            yield return $"{result.DatabaseName},{result.TestName},{result.MetricName}," +
                        $"{rawValue},{result.PValue:E4},{result.QValue:E4}," +
                        $"{isSignificantByP},{isSignificantByQ}";
        }
    }

    #endregion

    #region Three Individual Plot Models for UI

    /// <summary>
    /// Plot model for raw value distribution
    /// </summary>
    public PlotModel RawValuePlotModel
    {
        get
        {
            string cacheKey = $"raw|{SelectedTest}|{BinCount}|{UseLogScale}|{_selectedTestResults.Count}|{_selectedRawValues.Count}";
            if (_cachedRawPlotModel != null && _cachedRawPlotKey == cacheKey)
                return _cachedRawPlotModel;

            var model = new PlotModel
            {
                Title = "Raw Value Distribution",
                DefaultFontSize = 11,
                IsLegendVisible = false,
                Padding = new OxyThickness(10)
            };

            if (!_selectedTestResults.Any())
                return model;

            if (!_selectedRawValues.Any())
            {
                model.Subtitle = "No raw values available";
                return model;
            }

            var histogram = CreateHistogram(_selectedRawValues, "Raw Values", uiBinCount: BinCount);

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Raw Value",
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray
            });

            model.Axes.Add(CreateYAxis("Count"));

            AddHistogramSeries(model, histogram, OxyColors.Green);

            _cachedRawPlotKey = cacheKey;
            _cachedRawPlotModel = model;
            return model;
        }
    }

    /// <summary>
    /// Plot model for p-value distribution
    /// </summary>
    public PlotModel PValuePlotModel
    {
        get
        {
            string cacheKey = $"p|{SelectedTest}|{BinCount}|{UseLogScale}|{Alpha}|{_selectedTestResults.Count}|{_selectedPValues.Count}";
            if (_cachedPValuePlotModel != null && _cachedPValuePlotKey == cacheKey)
                return _cachedPValuePlotModel;

            var model = new PlotModel
            {
                Title = "P-Value Distribution",
                DefaultFontSize = 11,
                IsLegendVisible = false,
                Padding = new OxyThickness(10)
            };

            if (!_selectedTestResults.Any())
                return model;

            if (!_selectedPValues.Any())
            {
                model.Subtitle = "No valid p-values";
                return model;
            }

            var histogram = CreateHistogram(_selectedPValues, "P-Values", 0, 1.0, BinCount);

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "P-Value",
                Minimum = 0,
                Maximum = 1.0,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray
            });

            model.Axes.Add(CreateYAxis("Count"));

            AddHistogramSeries(model, histogram, OxyColors.SteelBlue);
            AddSignificanceThreshold(model);

            _cachedPValuePlotKey = cacheKey;
            _cachedPValuePlotModel = model;
            return model;
        }
    }

    /// <summary>
    /// Plot model for q-value distribution
    /// </summary>
    public PlotModel QValuePlotModel
    {
        get
        {
            string cacheKey = $"q|{SelectedTest}|{BinCount}|{UseLogScale}|{Alpha}|{_selectedTestResults.Count}|{_selectedQValues.Count}";
            if (_cachedQValuePlotModel != null && _cachedQValuePlotKey == cacheKey)
                return _cachedQValuePlotModel;

            var model = new PlotModel
            {
                Title = "Q-Value Distribution",
                DefaultFontSize = 11,
                IsLegendVisible = false,
                Padding = new OxyThickness(10)
            };

            if (!_selectedTestResults.Any())
                return model;

            if (!_selectedQValues.Any())
            {
                model.Subtitle = "No valid q-values";
                return model;
            }

            var histogram = CreateHistogram(_selectedQValues, "Q-Values", 0, 1.0, BinCount);

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Q-Value",
                Minimum = 0,
                Maximum = 1.0,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray
            });

            model.Axes.Add(CreateYAxis("Count"));

            AddHistogramSeries(model, histogram, OxyColors.Orange);
            AddSignificanceThreshold(model);

            _cachedQValuePlotKey = cacheKey;
            _cachedQValuePlotModel = model;
            return model;
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Build Q-Q plot for calibration assessment
    /// Plots observed -log10(p-values) against expected uniform quantiles
    /// A well-calibrated test follows the diagonal red line
    /// </summary>
    private PlotModel BuildQQPlot()
    {
        string cacheKey = $"qq|{SelectedTest}|{_selectedTestResults.Count}|{_selectedPValues.Count}";
        if (_cachedQQPlotModel != null && _cachedQQPlotKey == cacheKey)
            return _cachedQQPlotModel;

        var model = new PlotModel
        {
            Title = "Q-Q Plot (Calibration)",
            DefaultFontSize = 11,
            IsLegendVisible = false,
            Padding = new OxyThickness(10)
        };

        if (!_selectedTestResults.Any())
            return model;

        var validResults = _selectedDisplayPoints
            .Where(p => p.Result.IsDefined && !double.IsNaN(p.Result.PValue) && p.Result.PValue > 0 && p.Result.PValue <= 1.0)
            .OrderBy(p => p.Result.PValue)
            .ToList();

        if (validResults.Count < 2)
        {
            model.Subtitle = "Insufficient data for Q-Q plot";
            return model;
        }

        int n = validResults.Count;
        var sampledIndices = GetEvenlySampledIndices(n, MaxRenderedQqPoints);
        double maxExpected = -Math.Log10(1.0 / (n + 1));
        double maxObserved = validResults.Select(p => -Math.Log10(p.Result.PValue)).Max();
        double maxVal = Math.Max(maxExpected, maxObserved) * 1.05;

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = "Expected -log10(p) (uniform quantile)",
            Minimum = 0,
            Maximum = maxVal,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColors.LightGray
        });

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "Observed -log10(p) (empirical quantile)",
            Minimum = 0,
            Maximum = maxVal,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColors.LightGray
        });

        var diagonal = new LineSeries
        {
            Color = OxyColors.Red,
            StrokeThickness = 1,
            LineStyle = LineStyle.Dash
        };
        diagonal.Points.Add(new DataPoint(0, 0));
        diagonal.Points.Add(new DataPoint(maxVal, maxVal));
        model.Series.Add(diagonal);

        var scatter = new ScatterSeries
        {
            MarkerType = MarkerType.Circle,
            MarkerSize = 3,
            MarkerFill = OxyColors.SteelBlue,
            MarkerStroke = OxyColors.DarkBlue,
            MarkerStrokeThickness = 0.5,
            TrackerFormatString = "{Tag}"
        };

        foreach (int sampledIndex in sampledIndices)
        {
            var resultInfo = validResults[sampledIndex];
            double expected = -Math.Log10((double)(sampledIndex + 1) / (n + 1));
            double observed = -Math.Log10(resultInfo.Result.PValue);
            string toolTip = $"{resultInfo.OrganismName}\np-value: {resultInfo.Result.PValue:F4}\nraw value: {resultInfo.RawValueText}";
            scatter.Points.Add(new ScatterPoint(expected, observed, tag: toolTip));
        }

        model.Series.Add(scatter);

        if (sampledIndices.Count < n)
        {
            model.Subtitle = $"Rendering {sampledIndices.Count:N0} of {n:N0} points for performance";
        }

        _cachedQQPlotKey = cacheKey;
        _cachedQQPlotModel = model;
        return model;
    }

    /// <summary>
    /// Build volcano plot: effect size vs -log10(p-value)
    /// Each point is a database. Red = significant, gray = not.
    /// </summary>
    private PlotModel BuildVolcanoPlot()
    {
        string cacheKey = $"volcano|{SelectedTest}|{Alpha}|{_selectedTestResults.Count}";
        if (_cachedVolcanoPlotModel != null && _cachedVolcanoPlotKey == cacheKey)
            return _cachedVolcanoPlotModel;

        var model = new PlotModel
        {
            Title = "Volcano Plot (Effect vs Significance)",
            DefaultFontSize = 11,
            IsLegendVisible = true,
            Padding = new OxyThickness(10)
        };

        if (!_selectedTestResults.Any())
            return model;

        var nonSignificant = new ScatterSeries
        {
            Title = "Not significant",
            MarkerType = MarkerType.Circle,
            MarkerSize = 4,
            MarkerFill = OxyColors.Gray,
            MarkerStroke = OxyColors.DarkGray,
            MarkerStrokeThickness = 0.5,
            TrackerFormatString = "{Tag}"
        };

        var significant = new ScatterSeries
        {
            Title = $"Significant (p ≤ {Alpha})",
            MarkerType = MarkerType.Circle,
            MarkerSize = 5,
            MarkerFill = OxyColors.Red,
            MarkerStroke = OxyColors.DarkRed,
            MarkerStrokeThickness = 0.5,
            TrackerFormatString = "{Tag}"
        };

        var nonSignificantCandidates = new List<VolcanoRenderPoint>(_selectedDisplayPoints.Count);
        var significantCandidates = new List<VolcanoRenderPoint>();

        foreach (var resultInfo in _selectedDisplayPoints)
        {
            var result = resultInfo.Result;
            if (!result.IsDefined)
                continue;

            double? effectSize = result.EffectSize;
            if (!effectSize.HasValue || double.IsNaN(effectSize.Value) || double.IsInfinity(effectSize.Value))
                continue;

            double logP = result.PValue > 0 ? -Math.Log10(result.PValue) : 0;
            bool isSig = result.PValue <= Alpha;

            if (isSig)
                significantCandidates.Add(new VolcanoRenderPoint(resultInfo, effectSize.Value, logP));
            else
                nonSignificantCandidates.Add(new VolcanoRenderPoint(resultInfo, effectSize.Value, logP));
        }

        foreach (int index in GetEvenlySampledIndices(nonSignificantCandidates.Count, MaxRenderedVolcanoPointsPerSeries))
        {
            nonSignificant.Points.Add(nonSignificantCandidates[index].ToScatterPoint());
        }

        foreach (int index in GetEvenlySampledIndices(significantCandidates.Count, MaxRenderedVolcanoPointsPerSeries))
        {
            significant.Points.Add(significantCandidates[index].ToScatterPoint());
        }

        model.Series.Add(nonSignificant);
        model.Series.Add(significant);

        if (nonSignificant.Points.Count < nonSignificantCandidates.Count || significant.Points.Count < significantCandidates.Count)
        {
            model.Subtitle = $"Rendering {(nonSignificant.Points.Count + significant.Points.Count):N0} of {(nonSignificantCandidates.Count + significantCandidates.Count):N0} points for performance";
        }

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = "Effect Size (observed / null-mean)",
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColors.LightGray
        });

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "-log10(p-value)",
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColors.LightGray
        });

        if (_selectedTestResults.Any(r => r.IsDefined && r.PValue > 0))
        {
            double sigThreshold = -Math.Log10(Alpha);
            model.Annotations.Add(new LineAnnotation
            {
                Type = LineAnnotationType.Horizontal,
                Y = sigThreshold,
                Color = OxyColors.Red,
                LineStyle = LineStyle.Dash,
                StrokeThickness = 1,
                Text = $"p = {Alpha}",
                TextColor = OxyColors.Red,
                TextHorizontalAlignment = HorizontalAlignment.Right
            });
        }

        _cachedVolcanoPlotKey = cacheKey;
        _cachedVolcanoPlotModel = model;
        return model;
    }

    /// <summary>
    /// Update test summary when selected test or data changes
    /// </summary>
    private void UpdateTestSummary()
    {
        if (string.IsNullOrEmpty(SelectedTest) || !_selectedTestResults.Any())
        {
            TestSummary = null;
            return;
        }

        var metricName = _selectedTestResults.First().MetricName;
        var validDatabases = _selectedTestResults.Count(r => r.IsDefined);
        var significantByP = _selectedTestResults.Count(r => r.IsDefined && r.PValue <= Alpha);
        var significantByQ = _selectedTestResults.Count(r => r.IsDefined && r.QValue <= Alpha);

        TestSummary = new TestSummary
        {
            TestName = SelectedTest,
            MetricName = metricName,
            EvidenceFamily = _selectedTestResults.Select(r => r.EvidenceFamily).Distinct().Count() == 1 ? _selectedTestResults.First().EvidenceFamily : null,
            ValidDatabases = validDatabases,
            UndefinedDatabases = _selectedTestResults.Count(r => !r.IsDefined),
            SignificantByP = significantByP,
            SignificantByQ = significantByQ
        };
    }

    private void UpdateSelectedTestCaches()
    {
        InvalidateAllDetailPlotCaches();
        if (string.IsNullOrEmpty(SelectedTest) || !_allStatisticalResults.Any())
        {
            _selectedTestResults = [];
            _selectedDisplayPoints = [];
            _selectedRawValues = [];
            _selectedPValues = [];
            _selectedQValues = [];
            return;
        }

        _selectedTestResults = _resultsBySelectionKey.TryGetValue(SelectedTest, out var selectedResults)
            ? selectedResults
            : [];

        var displayPoints = new List<SelectedTestDisplayPoint>(_selectedTestResults.Count);
        var rawValues = new List<double>(_selectedTestResults.Count);
        var pValues = new List<double>(_selectedTestResults.Count);
        var qValues = new List<double>(_selectedTestResults.Count);

        foreach (var result in _selectedTestResults)
        {
            double rawValue = ExtractRawValue(result);
            string organismName = TaxonomyMapping.GetTaxonomyInfo(result.DatabaseName)?.Organism ?? result.DatabaseName;
            string rawValueText = double.IsNaN(rawValue) ? "N/A" : rawValue.ToString("F4");

            displayPoints.Add(new SelectedTestDisplayPoint(result, organismName, rawValue, rawValueText));

            if (result.IsDefined && !double.IsNaN(rawValue))
            {
                rawValues.Add(rawValue);
            }

            if (!double.IsNaN(result.PValue) && result.PValue > 0 && result.PValue <= 1.0)
            {
                pValues.Add(result.PValue);
            }

            if (!double.IsNaN(result.QValue) && result.QValue > 0 && result.QValue <= 1.0)
            {
                qValues.Add(result.QValue);
            }
        }

        _selectedDisplayPoints = displayPoints;
        _selectedRawValues = rawValues;
        _selectedPValues = pValues;
        _selectedQValues = qValues;
    }

    private static Dictionary<string, List<StatisticalTestResult>> BuildSelectionIndex(List<StatisticalTestResult> allResults)
    {
        var index = new Dictionary<string, List<StatisticalTestResult>>(StringComparer.Ordinal);

        foreach (var result in allResults)
        {
            if (!index.TryGetValue(result.SelectionKey, out var bucket))
            {
                bucket = new List<StatisticalTestResult>();
                index[result.SelectionKey] = bucket;
            }

            bucket.Add(result);
        }

        return index;
    }

    private static List<int> GetEvenlySampledIndices(int sourceCount, int maxCount)
    {
        if (sourceCount <= 0)
        {
            return [];
        }

        if (sourceCount <= maxCount)
        {
            return Enumerable.Range(0, sourceCount).ToList();
        }

        var indices = new List<int>(maxCount);
        double stride = (double)(sourceCount - 1) / (maxCount - 1);

        for (int i = 0; i < maxCount; i++)
        {
            indices.Add((int)Math.Round(i * stride));
        }

        return indices.Distinct().ToList();
    }

    private void NotifyDetailPlotModelPropertiesChanged()
    {
        OnPropertyChanged(nameof(RawValuePlotModel));
        OnPropertyChanged(nameof(PValuePlotModel));
        OnPropertyChanged(nameof(QValuePlotModel));
        OnPropertyChanged(nameof(QQPlotModel));
        OnPropertyChanged(nameof(VolcanoPlotModel));
    }

    private void NotifyHistogramPlotModelPropertiesChanged()
    {
        OnPropertyChanged(nameof(RawValuePlotModel));
        OnPropertyChanged(nameof(PValuePlotModel));
        OnPropertyChanged(nameof(QValuePlotModel));
    }

    private void NotifyAlphaDependentPlotModelPropertiesChanged()
    {
        OnPropertyChanged(nameof(PValuePlotModel));
        OnPropertyChanged(nameof(QValuePlotModel));
        OnPropertyChanged(nameof(VolcanoPlotModel));
    }

    private void InvalidateAllDetailPlotCaches()
    {
        InvalidateHistogramPlotCaches();
        InvalidateScatterPlotCaches();
    }

    private void InvalidateHistogramPlotCaches()
    {
        InvalidatePlotCache(ref _cachedRawPlotModel, ref _cachedRawPlotKey);
        InvalidatePlotCache(ref _cachedPValuePlotModel, ref _cachedPValuePlotKey);
        InvalidatePlotCache(ref _cachedQValuePlotModel, ref _cachedQValuePlotKey);
    }

    private void InvalidateAlphaDependentPlotCaches()
    {
        InvalidatePlotCache(ref _cachedPValuePlotModel, ref _cachedPValuePlotKey);
        InvalidatePlotCache(ref _cachedQValuePlotModel, ref _cachedQValuePlotKey);
        InvalidatePlotCache(ref _cachedVolcanoPlotModel, ref _cachedVolcanoPlotKey);
    }

    private void InvalidateScatterPlotCaches()
    {
        InvalidatePlotCache(ref _cachedQQPlotModel, ref _cachedQQPlotKey);
        InvalidatePlotCache(ref _cachedVolcanoPlotModel, ref _cachedVolcanoPlotKey);
    }

    private static void InvalidatePlotCache(ref PlotModel? cachedModel, ref string cacheKey)
    {
        cachedModel = null;
        cacheKey = string.Empty;
    }

    /// <summary>
    /// Extract raw value from a single result
    /// </summary>
    private double ExtractRawValue(StatisticalTestResult result)
    {
        // Try to get raw value from AdditionalMetrics
        return result.TestStatistic ?? double.NaN;
    }

    /// <summary>
    /// Create Y-axis with support for linear or logarithmic scale
    /// </summary>
    private Axis CreateYAxis(string title)
    {
        if (UseLogScale)
        {
            return new LogarithmicAxis
            {
                Position = AxisPosition.Left,
                Title = $"{title} (log scale)",
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray,
                MinorGridlineStyle = LineStyle.Dot,
                MinorGridlineColor = OxyColors.LightGray,
                Minimum = 0.1 // Avoid log(0)
            };
        }
        else
        {
            return new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = title,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray,
                Minimum = 0
            };
        }
    }

    /// <summary>
    /// Create histogram bins from data with custom bin count
    /// </summary>
    private List<HistogramBin> CreateHistogram(List<double> values, string label, double? minValue = null, double? maxValue = null, int? uiBinCount = null)
    {
        if (!values.Any())
            return new List<HistogramBin>();

        // Calculate using Sturges' rule if it is less than what is in the ui. 
        int sturgesBinCount = (int)Math.Ceiling(Math.Log2(values.Count) + 1);

        int numBins = uiBinCount.HasValue
            ? /*Math.Min(sturgesBinCount, */uiBinCount.Value
            : Math.Clamp(sturgesBinCount, MinBinCount, MaxBinCount);

        int distinctValues = values.Distinct().Count();

        double min = minValue ?? values.Min();
        double max = maxValue ?? values.Max();
        double range = max - min;

        if (range < 1e-10)
            return new List<HistogramBin>();

        double binWidth = range / numBins;

        var bins = new List<HistogramBin>();
        for (int i = 0; i < numBins; i++)
        {
            double binStart = min + i * binWidth;
            double binEnd = binStart + binWidth;
            bins.Add(new HistogramBin
            {
                Start = binStart,
                End = binEnd,
                Count = 0
            });
        }

        // Populate bins
        foreach (var value in values)
        {
            int binIndex = (int)((value - min) / binWidth);
            if (binIndex >= numBins)
                binIndex = numBins - 1;
            if (binIndex < 0)
                binIndex = 0;
            bins[binIndex].Count++;
        }

        return bins;
    }

    /// <summary>
    /// Add histogram series to plot model
    /// </summary>
    private void AddHistogramSeries(PlotModel model, List<HistogramBin> histogram, OxyColor color)
    {
        var series = new RectangleBarSeries
        {
            FillColor = color,
            StrokeThickness = 1,
            StrokeColor = OxyColors.Black,
        };

        foreach (var bin in histogram)
        {
            series.Items.Add(new RectangleBarItem
            {
                X0 = bin.Start,
                X1 = bin.End,
                Y0 = 0,
                Y1 = bin.Count
            });
        }

        model.Series.Add(series);
    }

    /// <summary>
    /// Add significance threshold line annotation
    /// </summary>
    private void AddSignificanceThreshold(PlotModel model)
    {
        model.Annotations.Add(new LineAnnotation
        {
            Type = LineAnnotationType.Vertical,
            X = Alpha,
            Color = OxyColors.Red,
            LineStyle = LineStyle.Dash,
            StrokeThickness = 2,
            Text = $"α = {Alpha}",
            TextColor = OxyColors.Red,
            TextHorizontalAlignment = HorizontalAlignment.Right
        });
    }

    #endregion
}

internal sealed class SelectedTestDisplayPoint(
    StatisticalTestResult result,
    string organismName,
    double rawValue,
    string rawValueText)
{
    public StatisticalTestResult Result { get; } = result;
    public string OrganismName { get; } = organismName;
    public double RawValue { get; } = rawValue;
    public string RawValueText { get; } = rawValueText;
}

internal sealed class VolcanoRenderPoint(SelectedTestDisplayPoint displayPoint, double effectSize, double logP)
{
    public SelectedTestDisplayPoint DisplayPoint { get; } = displayPoint;
    public double EffectSize { get; } = effectSize;
    public double LogP { get; } = logP;

    public ScatterPoint ToScatterPoint()
    {
        string toolTip = $"{DisplayPoint.OrganismName}\np-value: {DisplayPoint.Result.PValue:F4}\neffect size: {EffectSize:F4}\nraw value: {DisplayPoint.RawValueText}";
        return new ScatterPoint(EffectSize, LogP, tag: toolTip);
    }
}

/// <summary>
/// Histogram bin for distribution visualization
/// </summary>
public class HistogramBin
{
    public double Start { get; set; }
    public double End { get; set; }
    public int Count { get; set; }
}

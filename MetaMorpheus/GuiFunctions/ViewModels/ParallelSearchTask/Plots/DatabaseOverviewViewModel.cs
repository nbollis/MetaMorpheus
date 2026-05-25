using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TaskLayer.ParallelSearch.Statistics;

namespace GuiFunctions.ViewModels.ParallelSearchTask.Plots;

public sealed class DatabaseOverviewViewModel : BaseViewModel
{
    private DatabaseResultViewModel? _selectedDatabase;
    private PlotModel? _cachedFamilyBarPlotModel;
    private string _cachedFamilyBarPlotKey = string.Empty;

    public DatabaseResultViewModel? SelectedDatabase
    {
        get => _selectedDatabase;
        set
        {
            if (ReferenceEquals(_selectedDatabase, value)) return;
            _selectedDatabase = value;
            OnPropertyChanged(nameof(SelectedDatabase));
            Refresh();
        }
    }

    public PlotModel FamilyBarPlotModel { get; private set; } = new();
    public ObservableCollection<DatabaseTestResultRow> TestResults { get; } = new();

    public bool HasData => SelectedDatabase != null;
    public string DisplayName => SelectedDatabase?.DatabaseName ?? "(none)";
    public string Organism => SelectedDatabase?.OrganismName ?? "";
    public string CombinedP => SelectedDatabase != null ? FormatP(SelectedDatabase.CombinedPValue) : "";
    public string CombinedQ => SelectedDatabase != null ? FormatP(SelectedDatabase.CombinedQValue) : "";
    public string AnomalyScore
    {
        get
        {
            if (SelectedDatabase == null) return "";
            var r = SelectedDatabase.StatisticalResults.FirstOrDefault();
            if (r == null) return "";
            string rank = r.AnomalyRank < 0 ? "N/A" : r.AnomalyRank.ToString();
            return $"{r.SummaryAnomalyScore:F3} (rank {rank})";
        }
    }
    public string SummaryMetrics => SelectedDatabase != null
        ? $"{SelectedDatabase.AnalysisResult.TargetPsmsAtQValueThreshold} PSMs | {SelectedDatabase.AnalysisResult.TargetPeptidesAtQValueThreshold} Peptides | {SelectedDatabase.AnalysisResult.TransientProteinCount} Proteins"
        : "";

    public void Refresh()
    {
        var db = SelectedDatabase;
        FamilyBarPlotModel = db != null ? GetCachedFamilyBarChart(db) : new PlotModel();
        TestResults.Clear();
        if (db != null)
        {
            foreach (var r in db.StatisticalResults.OrderBy(r => r.EvidenceFamily).ThenBy(r => r.TestName))
            {
                TestResults.Add(new DatabaseTestResultRow
                {
                    Family = r.EvidenceFamily?.ToString() ?? "",
                    TestName = r.TestName,
                    MetricName = r.MetricName,
                    PValue = r.PValue,
                    QValue = r.QValue,
                    EffectSize = r.EffectSize ?? double.NaN,
                    IsSignificant = r.IsSignificant(),
                    IsDefined = r.IsDefined
                });
            }
        }
        NotifyAll();
    }

    private PlotModel GetCachedFamilyBarChart(DatabaseResultViewModel db)
    {
        string key = $"{db.DatabaseName}|{db.StatisticalResults.Count}|{db.CombinedPValue}|{db.CombinedQValue}";
        if (_cachedFamilyBarPlotModel != null && _cachedFamilyBarPlotKey == key)
            return _cachedFamilyBarPlotModel;

        _cachedFamilyBarPlotKey = key;
        _cachedFamilyBarPlotModel = BuildFamilyBarChart(db);
        return _cachedFamilyBarPlotModel;
    }

    private PlotModel BuildFamilyBarChart(DatabaseResultViewModel db)
    {
        var combinedByFamily = db.StatisticalResults
            .Where(r => r.IsCombinedResult && r.EvidenceFamily.HasValue)
            .GroupBy(r => r.EvidenceFamily!.Value)
            .ToDictionary(g => g.Key, g => g.First().PValue);

        var labels = new List<string>();
        var strengths = new List<double>();
        double threshold = -Math.Log10(0.01);

        AddBarIfValid(labels, strengths, "CountEnrichment",   GetFamilyP(combinedByFamily, StatisticalEvidenceFamily.CountEnrichment));
        AddBarIfValid(labels, strengths, "AmbiguityDecoy",    GetFamilyP(combinedByFamily, StatisticalEvidenceFamily.AmbiguityOrTargetDecoy));
        AddBarIfValid(labels, strengths, "ScoreDistribution", GetFamilyP(combinedByFamily, StatisticalEvidenceFamily.ScoreDistribution));
        AddBarIfValid(labels, strengths, "Fragmentation",     GetFamilyP(combinedByFamily, StatisticalEvidenceFamily.Fragmentation));
        AddBarIfValid(labels, strengths, "RetentionTime",     GetFamilyP(combinedByFamily, StatisticalEvidenceFamily.RetentionTime));
        AddBarIfValid(labels, strengths, "ProteinGroup",      GetFamilyP(combinedByFamily, StatisticalEvidenceFamily.ProteinGroup));
        AddBarIfValid(labels, strengths, "DeNovo",            GetFamilyP(combinedByFamily, StatisticalEvidenceFamily.DeNovo));
        AddBarIfValid(labels, strengths, "PrecursorDecon",    GetFamilyP(combinedByFamily, StatisticalEvidenceFamily.PrecursorDeconvolution));

        var model = new PlotModel
        {
            Title = "Family Evidence",
            DefaultFontSize = 11,
            IsLegendVisible = false,
            Padding = new OxyThickness(8)
        };

        if (labels.Count == 0)
        {
            model.Subtitle = "No family evidence data available for this database";
            return model;
        }

        model.Axes.Add(new CategoryAxis
        {
            Position = AxisPosition.Left,
            ItemsSource = labels,
            GapWidth = 0.3
        });

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = "-log10(Combined P)",
            Minimum = 0,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColors.LightGray
        });

        var sigSeries = new BarSeries { FillColor = OxyColors.SteelBlue, StrokeThickness = 0.5 };
        var nonSigSeries = new BarSeries { FillColor = OxyColors.LightGray, StrokeThickness = 0.5 };

        for (int i = 0; i < strengths.Count; i++)
        {
            (strengths[i] >= threshold ? sigSeries : nonSigSeries).Items.Add(new BarItem { Value = strengths[i] });
        }

        if (sigSeries.Items.Count > 0) model.Series.Add(sigSeries);
        if (nonSigSeries.Items.Count > 0) model.Series.Add(nonSigSeries);

        return model;
    }

    private static void AddBarIfValid(List<string> labels, List<double> strengths, string label, double pValue)
    {
        if (!double.IsNaN(pValue) && pValue > 0 && pValue < 1.0)
        {
            labels.Add(label);
            strengths.Add(-Math.Log10(pValue));
        }
    }

    private static double GetFamilyP(Dictionary<StatisticalEvidenceFamily, double> dict, StatisticalEvidenceFamily family) =>
        dict.TryGetValue(family, out var p) ? p : double.NaN;

    private static string FormatP(double p) =>
        double.IsNaN(p) ? "NaN" : p < 0.0001 ? p.ToString("e2") : p.ToString("F4");

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(FamilyBarPlotModel));
        OnPropertyChanged(nameof(TestResults));
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Organism));
        OnPropertyChanged(nameof(CombinedP));
        OnPropertyChanged(nameof(CombinedQ));
        OnPropertyChanged(nameof(AnomalyScore));
        OnPropertyChanged(nameof(SummaryMetrics));
    }
}

public sealed class DatabaseTestResultRow
{
    public string Family { get; set; } = "";
    public string TestName { get; set; } = "";
    public string MetricName { get; set; } = "";
    public double PValue { get; set; }
    public double QValue { get; set; }
    public double EffectSize { get; set; }
    public bool IsSignificant { get; set; }
    public bool IsDefined { get; set; }
}

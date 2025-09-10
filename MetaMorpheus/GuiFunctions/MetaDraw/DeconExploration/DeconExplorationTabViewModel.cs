#nullable enable
using EngineLayer.Util;
using MassSpectrometry;
using MathNet.Numerics;
using MzLibUtil;
using OxyPlot.Wpf;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ThermoFisher.CommonCore.Data.Business;
using Precursor = EngineLayer.Util.Precursor;

namespace GuiFunctions;

public enum DeconvolutionMode
{
    FullSpectrum,
    IsolationRegion
}

public class DeconExplorationTabViewModel : BaseViewModel
{
    public ObservableCollection<MsDataFile> MsDataFiles { get; set; } = new();
    public ObservableCollection<MsDataScan> Scans { get; set; } = new();
    public ObservableCollection<DeconvolutedSpeciesViewModel> DeconvolutedSpecies { get; set; } = new();
    public DeconvolutionPlot? Plot { get; set; }
    public List<DeconvolutionMode> DeconvolutionModes { get; } = System.Enum.GetValues<DeconvolutionMode>().ToList();
    public DeconHostViewModel DeconHostViewModel { get; set; } = new();

    private MsDataFile? _selectedMsDataFile;
    public MsDataFile? SelectedMsDataFile
    {
        get => _selectedMsDataFile;
        set
        {
            if (_selectedMsDataFile == value) return;
            _selectedMsDataFile = value;

            PopulateScansCollection();
            OnPropertyChanged(nameof(SelectedMsDataFile));
        }
    }

    private MsDataScan? _selectedMsDataScan;
    public MsDataScan? SelectedMsDataScan
    {
        get => _selectedMsDataScan;
        set
        {
            if (_selectedMsDataScan == value) return;
            _selectedMsDataScan = value;
            OnPropertyChanged(nameof(SelectedMsDataScan));
        }
    }

    private DeconvolutionMode _mode;
    public DeconvolutionMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;

            PopulateScansCollection();
            OnPropertyChanged(nameof(Mode));
        }
    }

    private bool _applyPrecursorFiltering;
    public bool ApplyPrecursorFiltering
    {
        get => _applyPrecursorFiltering;
        set
        {
            if (_applyPrecursorFiltering == value) return;
            _applyPrecursorFiltering = value;
            OnPropertyChanged(nameof(ApplyPrecursorFiltering));
        }
    }

    public ICommand RunDeconvolutionCommand { get; }

    public DeconExplorationTabViewModel()
    {
        Mode = DeconvolutionMode.IsolationRegion;
        RunDeconvolutionCommand = new DelegateCommand(pv => RunDeconvolution((pv as PlotView)!));
    }

    private void RunDeconvolution(PlotView plotView)
    {
        DeconvolutedSpecies.Clear();
        if (SelectedMsDataScan == null) return;

        IEnumerable<IsotopicEnvelope> results;
        MzRange? isolationRange;
        MsDataScan scanToPlot;
        Tolerance? deconPpmTolerance = null;

        if (Mode == DeconvolutionMode.FullSpectrum)
        {
            isolationRange = null;
            scanToPlot = SelectedMsDataScan;
            var deconParamsVm = SelectedMsDataScan.MsnOrder == 1
                ? DeconHostViewModel.PrecursorDeconvolutionParameters
                : DeconHostViewModel.ProductDeconvolutionParameters;
            deconPpmTolerance = deconParamsVm.DeconvolutionTolerance;
            results = Deconvoluter.Deconvolute(scanToPlot, deconParamsVm.Parameters);
        }
        else
        {
            isolationRange = SelectedMsDataScan.IsolationRange;
            scanToPlot = SelectedMsDataFile!.GetOneBasedScan(SelectedMsDataScan.OneBasedPrecursorScanNumber!.Value);
            var deconParams = DeconHostViewModel.PrecursorDeconvolutionParameters;
            deconPpmTolerance = deconParams.DeconvolutionTolerance;
            results = SelectedMsDataScan.GetIsolatedMassesAndCharges(scanToPlot, deconParams.Parameters);
        }

        if (ApplyPrecursorFiltering && deconPpmTolerance != null)
        {
            var set = new PrecursorSet(deconPpmTolerance);
            foreach (var envelope in results)
            {
                if (envelope != null)
                    set.Add(new Precursor(envelope));
            }
            set.Sanitize();
            results = set.Select(p => p.Envelope!).Where(p => p != null);
        }

        // Project to view models and sort
        var sortedSpecies = results
            .Where(p => p != null)
            .Select(p => new DeconvolutedSpeciesViewModel(p))
            .OrderByDescending(p => p.MonoisotopicMass.Round(2))
            .ThenByDescending(p => p.Charge)
            .ToList();

        foreach (var deconSpecies in sortedSpecies)
            DeconvolutedSpecies.Add(deconSpecies);

        Plot = new DeconvolutionPlot(plotView, scanToPlot, sortedSpecies, isolationRange);
    }

    private void PopulateScansCollection()
    {
        Scans.Clear();

        if (SelectedMsDataFile == null)
            return;

        switch (Mode)
        {
            // Display only MS2
            case DeconvolutionMode.IsolationRegion:
            {
                foreach (var scan in SelectedMsDataFile.GetMsDataScans())
                    if (scan.MsnOrder == 2)
                        Scans.Add(scan);
                break;
            }
            case DeconvolutionMode.FullSpectrum:
            {
                foreach (var scan in SelectedMsDataFile.GetMsDataScans())
                    Scans.Add(scan);
                break;
            }
        }
    }
}
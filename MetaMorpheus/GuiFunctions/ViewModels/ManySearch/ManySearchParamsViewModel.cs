using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using EngineLayer.DatabaseLoading;
using TaskLayer;

namespace GuiFunctions;

/// <summary>
/// View model for managing ManySearch parameters including transient databases
/// </summary>
public sealed class ManySearchParamsViewModel : BaseViewModel
{
    private ManySearchParameters _parameters;

    public ManySearchParamsViewModel(ManySearchParameters? parameters = null)
    {
        _parameters = parameters ?? new ManySearchParameters();
        
        // Initialize commands
        AddDatabaseCommand = new DelegateCommand(AddDatabase);
        RemoveSelectedDatabasesCommand = new RelayCommand(RemoveSelectedDatabases);
        ClearAllDatabasesCommand = new RelayCommand(ClearAllDatabases);
        
        // Initialize observable collection from parameters
        TransientDatabases = new ObservableCollection<ProteinDbForDataGrid>(
            _parameters.TransientDatabases.Select(db => new ProteinDbForDataGrid(db)));
        
        // Subscribe to collection changes to keep parameters in sync
        TransientDatabases.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(TransientDatabaseCount));
            OnPropertyChanged(nameof(HasTransientDatabases));
            SyncDatabasesBackToParameters();
        };
    }

    public ManySearchParameters Parameters
    {
        get => _parameters;
        set
        {
            _parameters = value;
            OnPropertyChanged(nameof(Parameters));
            OnPropertyChanged(nameof(TransientDatabaseCount));
            OnPropertyChanged(nameof(HasTransientDatabases));

            // Reload the observable collection
            var tempList = _parameters.TransientDatabases.ToList();
            TransientDatabases.Clear();
            foreach (var db in tempList)
            {
                TransientDatabases.Add(new ProteinDbForDataGrid(db));
            }
        }
    }

    // Observable collection for UI binding
    private ObservableCollection<ProteinDbForDataGrid> _transientDatabases = new();
    public ObservableCollection<ProteinDbForDataGrid> TransientDatabases
    {
        get => _transientDatabases;
        set
        {
            _transientDatabases = value;
            OnPropertyChanged(nameof(TransientDatabases));
        }
    }

    public int TransientDatabaseCount => TransientDatabases?.Count ?? 0;
    public bool HasTransientDatabases => TransientDatabaseCount > 0;

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            OnPropertyChanged(nameof(IsEnabled));
        }
    }

    public int MaxSearchesInParallel
    {
        get => _parameters.MaxSearchesInParallel;
        set
        {
            if (value > 0)
            {
                _parameters.MaxSearchesInParallel = value;
                OnPropertyChanged(nameof(MaxSearchesInParallel));
            }
        }
    }

    public bool OverwriteTransientResults
    {
        get => _parameters.OverwriteTransientSearchOutputs;
        set
        {
            _parameters.OverwriteTransientSearchOutputs = value;
            OnPropertyChanged(nameof(OverwriteTransientResults));
        }
    }

    public bool CompressTransientResults
    {
        get => _parameters.CompressTransientSearchOutputs;
        set
        {
            _parameters.CompressTransientSearchOutputs = value;
            OnPropertyChanged(nameof(CompressTransientResults));
        }
    }

    public bool WriteTransientResultsOnly
    {
        get => _parameters.WriteTransientResultsOnly;
        set
        {
            _parameters.WriteTransientResultsOnly = value;
            OnPropertyChanged(nameof(WriteTransientResultsOnly));
        }
    }

    public bool WriteTransientSpectralLibrary
    {
        get => _parameters.WriteTransientSpectralLibrary;
        set
        {
            _parameters.WriteTransientSpectralLibrary = value;
            OnPropertyChanged(nameof(WriteTransientSpectralLibrary));
        }
    }

    // Commands
    public ICommand AddDatabaseCommand { get; }
    public ICommand RemoveSelectedDatabasesCommand { get; }
    public ICommand ClearAllDatabasesCommand { get; }

    /// <summary>
    /// Adds a transient database from file path
    /// </summary>
    private void AddDatabase(object parameter)
    {
        if (parameter is string filePath)
        {
            AddTransientDatabase(filePath);
        }
        else if (parameter is IEnumerable<string> filePaths)
        {
            foreach (var path in filePaths)
            {
                AddTransientDatabase(path);
            }
        }
    }

    /// <summary>
    /// Adds a transient database, ensuring no duplicates based on file path
    /// </summary>
    public void AddTransientDatabase(string filePath, bool isContaminant = false, string? decoyIdentifier = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        // Sanitize: Check for duplicates
        if (TransientDatabases.Any(d => d.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
            return;

        var db = new ProteinDbForDataGrid(filePath)
        {
            Contaminant = isContaminant
        };
        if (decoyIdentifier != null)
        {
            db.DecoyIdentifier = decoyIdentifier;
        }
        
        TransientDatabases.Add(db);
    }

    /// <summary>
    /// Removes selected databases
    /// </summary>
    private void RemoveSelectedDatabases()
    {
        // Get selected items (will be set from the control)
        var toRemove = TransientDatabases.Where(d => d.IsSelected).ToList();
        foreach (var db in toRemove)
        {
            TransientDatabases.Remove(db);
        }
    }

    /// <summary>
    /// Clears all transient databases
    /// </summary>
    private void ClearAllDatabases()
    {
        if (!HasTransientDatabases)
            return;

        var result = MessageBox.Show(
            "Are you sure you want to clear all transient databases?",
            "Confirm Clear",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            TransientDatabases.Clear();
        }
    }

    /// <summary>
    /// Syncs the observable collection back to the parameters object
    /// </summary>
    private void SyncDatabasesBackToParameters()
    {
        _parameters.TransientDatabases = TransientDatabases
            .Where(d => d.Use)
            .Select(d => new DbForTask(d.FilePath, d.Contaminant, d.DecoyIdentifier))
            .ToList();
    }

    public override string ToString() => $"ManySearch ({TransientDatabaseCount} databases)";
}

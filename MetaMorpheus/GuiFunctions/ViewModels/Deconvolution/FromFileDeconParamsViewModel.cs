using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using MassSpectrometry;
using TaskLayer.Deconvolution;
using TaskLayer.Deconvolution.FeatureFileMapping;

namespace GuiFunctions
{
    /// <summary>
    /// View-model for precursor deconvolution parameters whose feature file mapping
    /// is driven from a <see cref="SearchFeatureFileMap"/>.
    ///
    /// Per the user clarification, the condition is GLOBAL to the entire feature set:
    /// there is exactly ONE selected condition that applies to every selected spectra
    /// file. Mixed per-row condition selection is not supported.
    ///
    /// Reopen/restore source of truth: <see cref="SearchFeatureFileMap.SelectedConditionKey"/>.
    /// The global <see cref="FeatureFileMapStore"/> is used for condition discovery
    /// and to derive each row's resolved feature file path for the shared condition.
    /// </summary>
    public sealed class FromFileDeconParamsViewModel : DeconParamsViewModel
    {
        private FeatureMappedFromFileDeconvolutionParameters _parameters;
        private string _selectedCondition = string.Empty;

        /// <summary>
        /// One row per selected spectra file. Rows no longer hold per-row condition
        /// state — they only display the resolved feature file path under the VM's
        /// currently selected shared condition.
        /// </summary>
        public ObservableCollection<FromFileFeatureMappingRowModel> Rows { get; } = new ObservableCollection<FromFileFeatureMappingRowModel>();

        /// <summary>
        /// All condition keys the user can choose from for this feature set.
        /// Populated from the global store and the embedded map (so a previously-saved
        /// condition remains selectable even if the store no longer mentions it).
        /// </summary>
        public ObservableCollection<string> AvailableConditions { get; } = new ObservableCollection<string>();

        /// <summary>
        /// The single shared condition for the entire feature set. Setting this
        /// recomputes every row's resolved feature file path.
        /// </summary>
        public string SelectedCondition
        {
            get => _selectedCondition;
            set
            {
                var newValue = value ?? string.Empty;
                if (_selectedCondition == newValue)
                    return;
                _selectedCondition = newValue;
                OnPropertyChanged(nameof(SelectedCondition));
                RefreshAllResolvedPaths();
                OnPropertyChanged(nameof(IsValid));
            }
        }

        public bool IsValid =>
            // Vacuously valid when no rows have been initialized: the user hasn't yet
            // been given a chance to map anything, so this is the pre-mapping state, not
            // an invalid mapping. Once rows are present, every row must resolve to a
            // feature file path under the shared condition for the mapping to be valid.
            Rows.Count == 0
            || (Rows.All(r => !string.IsNullOrWhiteSpace(r.ResolvedFeatureFilePath))
                && !string.IsNullOrWhiteSpace(_selectedCondition));

        public override int MaxAssumedChargeState
        {
            get
            {
                if (!IsValid)
                {
                    MessageBoxHelper.ShowError("Invalid feature mapping. Please specify a mapping for all selected files.");
                    return 0; // Trigger explicit task validation failure
                }
                return _parameters.MaxAssumedChargeState;
            }
            set
            {
                _parameters.MaxAssumedChargeState = value;
                OnPropertyChanged(nameof(MaxAssumedChargeState));
            }
        }

        public override DeconvolutionParameters Parameters
        {
            get
            {
                // Empty Rows is the pre-mapping state (no spectra files yet, or no rows
                // initialised). Returning the underlying parameters as-is here keeps the
                // save flow from throwing for a fresh, never-configured search. The host's
                // task-validation surface still calls IsValid / MaxAssumedChargeState, which
                // gate save/run; once the user has actually populated rows, an incomplete
                // mapping is invalid and will throw here as the loud failure boundary.
                if (Rows.Count > 0 && !IsValid)
                {
                    throw new InvalidOperationException("Invalid feature mapping. Please specify a mapping for all selected files.");
                }

                var newMap = new SearchFeatureFileMap();
                newMap.SourceMapPath = _parameters.FeatureFileMap?.SourceMapPath ?? string.Empty;
                newMap.SelectedConditionKey = _selectedCondition;

                // Use the global store's display name if we can find it, otherwise fall
                // back to the condition key. This keeps reopen restoration matching the
                // key back to a display name when the store is still around.
                if (TryLoadStoreForRead(out var storeForDisplay)
                    && storeForDisplay.TryGetCondition(_selectedCondition, out var condMeta))
                {
                    newMap.SelectedConditionDisplayName = condMeta.DisplayName;
                }
                else
                {
                    newMap.SelectedConditionDisplayName = _selectedCondition;
                }

                foreach (var row in Rows)
                {
                    newMap.Entries.Add(new SearchFeatureFileMapEntry
                    {
                        MassSpecFilePath = row.MassSpecFilePath,
                        MassSpecFileName = row.MassSpecFileName,
                        FeatureFilePath = row.ResolvedFeatureFilePath,
                        FeatureFileName = Path.GetFileName(row.ResolvedFeatureFilePath)
                    });
                }

                _parameters.FeatureFileMap = newMap;
                return _parameters;
            }
            protected set
            {
                _parameters = (FeatureMappedFromFileDeconvolutionParameters)value;
                OnPropertyChanged(nameof(Parameters));
            }
        }

        public FromFileDeconParamsViewModel(FeatureMappedFromFileDeconvolutionParameters parameters, IEnumerable<string> currentRawFiles)
        {
            Parameters = parameters;
            InitializeRows(currentRawFiles);
        }

        /// <summary>
        /// Creates a view model without initializing rows. Rows can be initialized later via <see cref="InitializeRows"/>.
        /// Used by <see cref="MzLibExtensions.ToViewModel"/> when raw file list is not available at construction time
        /// (e.g., when reopening a saved task with <see cref="FeatureMappedFromFileDeconvolutionParameters"/>).
        /// </summary>
        public FromFileDeconParamsViewModel(FeatureMappedFromFileDeconvolutionParameters parameters)
        {
            Parameters = parameters;
        }

        /// <summary>
        /// Override the base to return a constant — accessing <see cref="DeconvolutionType"/> must
        /// never eagerly evaluate <see cref="Parameters"/>, because for an unconfigured FromFile VM
        /// (no rows mapped yet) the parameters getter throws <see cref="InvalidOperationException"/>
        /// to enforce the save/run validation contract. Type-based selection in the host and in
        /// the GUI combobox converter need to read this property on a brand-new VM before the
        /// user has finished configuring mappings.
        /// </summary>
        public override DeconvolutionType DeconvolutionType => DeconvolutionType.FromFile;

        /// <summary>
        /// Rebuilds rows from <paramref name="rawFiles"/> and the global feature-map store.
        /// Discovers the set of available conditions and, when the embedded map already
        /// has a selected condition, restores it. After this call the VM is in a stable
        /// state — selecting/switching to this VM must NOT throw.
        /// </summary>
        public void InitializeRows(IEnumerable<string> rawFiles, string storePath = null)
        {
            Rows.Clear();
            _parameters.FeatureFileMap ??= new SearchFeatureFileMap();

            // Try to read the global store. Treat any failure (missing/corrupt file) as
            // "no store available" rather than throwing — switching to FromFile on a fresh
            // search must not crash the host.
            FeatureFileMap globalStore = null;
            try
            {
                globalStore = FeatureFileMapStore.Load(storePath);
                _parameters.FeatureFileMap.SourceMapPath = globalStore.FilePath;
            }
            catch
            {
                globalStore = new FeatureFileMap();
            }

            // Discover all condition keys present in the global store.
            var discovered = new HashSet<string>(StringComparer.Ordinal);
            foreach (var cond in globalStore.Conditions)
            {
                if (!string.IsNullOrEmpty(cond.ConditionKey))
                    discovered.Add(cond.ConditionKey);
            }
            foreach (var spectraEntry in globalStore.SpectraFiles)
            {
                foreach (var cf in spectraEntry.FeatureFiles)
                {
                    if (!string.IsNullOrEmpty(cf.ConditionKey))
                        discovered.Add(cf.ConditionKey);
                }
            }

            // Ensure the previously-saved selected condition is in the dropdown so the
            // restore step below can match even if the global store no longer contains it.
            var embeddedSelected = _parameters.FeatureFileMap.SelectedConditionKey;
            if (!string.IsNullOrEmpty(embeddedSelected))
                discovered.Add(embeddedSelected);

            AvailableConditions.Clear();
            foreach (var c in discovered.OrderBy(c => c, StringComparer.Ordinal))
                AvailableConditions.Add(c);

            // Build rows: one per raw file. Resolved paths are computed in
            // RefreshAllResolvedPaths once the shared condition is set.
            foreach (var rawFile in rawFiles ?? Enumerable.Empty<string>())
            {
                Rows.Add(new FromFileFeatureMappingRowModel(rawFile));
            }

            // Restore the shared condition from the embedded map (reopen truth source).
            // Fall back to the single available condition (when only one exists, auto-pick).
            // Otherwise leave it empty so the user must choose.
            if (!string.IsNullOrEmpty(embeddedSelected) && AvailableConditions.Contains(embeddedSelected))
            {
                _selectedCondition = embeddedSelected;
            }
            else if (AvailableConditions.Count == 1)
            {
                _selectedCondition = AvailableConditions[0];
            }
            else
            {
                _selectedCondition = string.Empty;
            }

            OnPropertyChanged(nameof(SelectedCondition));
            OnPropertyChanged(nameof(AvailableConditions));
            RefreshAllResolvedPaths();
            OnPropertyChanged(nameof(IsValid));
        }

        /// <summary>
        /// Recomputes every row's resolved feature file path under the current shared
        /// condition. Rows whose spectra file has no entry for the shared condition
        /// (per the global store, or the embedded map as fallback) end up with an
        /// empty path; this is the intended invalid-mapping signal.
        /// </summary>
        private void RefreshAllResolvedPaths()
        {
            FeatureFileMap store = null;
            try { store = TryLoadStoreForRead(out var s) ? s : null; } catch { store = null; }

            foreach (var row in Rows)
            {
                if (string.IsNullOrEmpty(_selectedCondition))
                {
                    row.ResolvedFeatureFilePath = string.Empty;
                    continue;
                }

                string resolved = null;
                if (store != null && store.TryGetFeatureFile(row.MassSpecFilePath, _selectedCondition, out var p))
                {
                    resolved = p;
                }
                else if (_parameters.FeatureFileMap != null
                    && _parameters.FeatureFileMap.TryGetFeaturePathForMassSpecFile(row.MassSpecFilePath, out var embedded))
                {
                    // Fall back to whatever the embedded map recorded for this spectra
                    // file (e.g. on reopen when the global store is empty for this file).
                    resolved = embedded;
                }

                row.ResolvedFeatureFilePath = resolved ?? string.Empty;
            }
        }

        private bool TryLoadStoreForRead(out FeatureFileMap map)
        {
            try
            {
                map = FeatureFileMapStore.Load(_parameters.FeatureFileMap?.SourceMapPath);
                return true;
            }
            catch
            {
                map = null;
                return false;
            }
        }

        public override string ToString() => "From Feature File";
    }
}

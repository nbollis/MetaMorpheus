using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MassSpectrometry;
using TaskLayer.Deconvolution;
using TaskLayer.Deconvolution.FeatureFileMapping;

namespace GuiFunctions
{
    public sealed class FromFileDeconParamsViewModel : DeconParamsViewModel
    {
        private FeatureMappedFromFileDeconvolutionParameters _parameters;

        public bool IsValid => !Rows.Any(r => string.IsNullOrWhiteSpace(r.SelectedCondition) || string.IsNullOrWhiteSpace(r.ResolvedFeatureFilePath));

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
                if (!IsValid)
                {
                    throw new InvalidOperationException("Invalid feature mapping. Please specify a mapping for all selected files.");
                }

                var newMap = new SearchFeatureFileMap();
                newMap.SourceMapPath = _parameters.FeatureFileMap?.SourceMapPath ?? string.Empty;
                foreach (var row in Rows)
                {
                    newMap.Entries.Add(new SearchFeatureFileMapEntry 
                    {
                        MassSpecFilePath = row.MassSpecFilePath,
                        MassSpecFileName = row.MassSpecFileName,
                        FeatureFilePath = row.ResolvedFeatureFilePath,
                        FeatureFileName = System.IO.Path.GetFileName(row.ResolvedFeatureFilePath)
                    });
                }

                // Preserve condition selection state for reopen restoration
                var firstSelectedRow = Rows.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.SelectedCondition));
                if (firstSelectedRow != null)
                {
                    newMap.SelectedConditionKey = firstSelectedRow.SelectedCondition;
                    // Note: SelectedConditionDisplayName equals the condition key because the data model
                    // (SearchFeatureFileMapEntry / FeatureFileConditionEntry) only has ConditionKey — there
                    // is no separate display name field stored per entry. The global store's
                    // FeatureFileMapCondition.DisplayName is not available at this point; we only have the
                    // condition key string from the UI dropdown. Using the key as the display name ensures
                    // reopen restoration can match it back to a condition.
                    newMap.SelectedConditionDisplayName = firstSelectedRow.SelectedCondition;
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

        public ObservableCollection<FromFileFeatureMappingRowModel> Rows { get; } = new ObservableCollection<FromFileFeatureMappingRowModel>();

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

        public void InitializeRows(IEnumerable<string> rawFiles, string storePath = null)
        {
            Rows.Clear();
            var globalStore = FeatureFileMapStore.Load(storePath); // Read from disk — provides condition discovery
            _parameters.FeatureFileMap ??= new SearchFeatureFileMap();
            _parameters.FeatureFileMap.SourceMapPath = globalStore.FilePath;

            foreach (var rawFile in rawFiles ?? Enumerable.Empty<string>())
            {
                // --- Condition discovery from the global store ---
                var conditions = new List<string>();
                if (globalStore.TryGetSpectraEntry(rawFile, out var entry))
                {
                    foreach (var cf in entry.FeatureFiles)
                    {
                        conditions.Add(cf.ConditionKey);
                    }
                }

                // --- Embedded map entries are the primary source for re-opening ---
                string embeddedFeaturePath = null;
                if (_parameters.FeatureFileMap.TryGetFeaturePathForMassSpecFile(rawFile, out var embPath))
                {
                    embeddedFeaturePath = embPath;
                    // Ensure the embedded map's selected condition is in the dropdown
                    // so the pre-selection below can match even if the global store
                    // does not contain this condition anymore.
                    if (!string.IsNullOrEmpty(_parameters.FeatureFileMap.SelectedConditionKey) &&
                        !conditions.Contains(_parameters.FeatureFileMap.SelectedConditionKey))
                    {
                        conditions.Add(_parameters.FeatureFileMap.SelectedConditionKey);
                    }
                }

                var row = new FromFileFeatureMappingRowModel(
                    rawFile,
                    conditions,
                    (msFile, condKey) =>
                    {
                        // First try to resolve the feature path from the global store
                        if (globalStore.TryGetFeatureFile(msFile, condKey, out var featurePath))
                            return featurePath;
                        // Fall back to the feature path captured in the embedded map
                        return embeddedFeaturePath;
                    }
                );

                // If the incoming parameters already have a selected condition, and this row supports it, pre-select it
                if (!string.IsNullOrEmpty(_parameters.FeatureFileMap?.SelectedConditionKey) &&
                    conditions.Contains(_parameters.FeatureFileMap.SelectedConditionKey))
                {
                    row.SelectedCondition = _parameters.FeatureFileMap.SelectedConditionKey;
                }
                // Or if there is exactly one available condition, default to it
                else if (conditions.Count == 1)
                {
                    row.SelectedCondition = conditions[0];
                }

                Rows.Add(row);
            }
        }

        public override string ToString() => "From Feature File";
    }
}

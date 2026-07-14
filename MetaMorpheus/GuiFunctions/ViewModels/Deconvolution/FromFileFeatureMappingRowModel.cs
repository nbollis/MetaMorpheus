using System.Collections.ObjectModel;
using System.IO;

namespace GuiFunctions
{
    /// <summary>
    /// One row in the FromFileDeconParamsViewModel mapping grid. Represents a single
    /// mass-spectrometry file's resolved feature-file path under the VM's currently
    /// selected (shared) condition.
    ///
    /// Per the user clarification, the condition is GLOBAL to the feature set: there
    /// is no per-row condition choice. The row simply displays what the shared
    /// condition resolves to for this spectra file. The owning
    /// <see cref="FromFileDeconParamsViewModel"/> drives the resolver and pushes
    /// the resolved path here whenever the shared condition or the spectra file set
    /// changes.
    /// </summary>
    public class FromFileFeatureMappingRowModel : BaseViewModel
    {
        private string _resolvedFeatureFilePath = string.Empty;

        public string MassSpecFilePath { get; }
        public string MassSpecFileName { get; }

        public FromFileFeatureMappingRowModel(string massSpecFilePath)
        {
            MassSpecFilePath = massSpecFilePath;
            MassSpecFileName = Path.GetFileName(massSpecFilePath);
        }

        public string ResolvedFeatureFilePath
        {
            get => _resolvedFeatureFilePath;
            set
            {
                if (_resolvedFeatureFilePath != value)
                {
                    _resolvedFeatureFilePath = value ?? string.Empty;
                    OnPropertyChanged(nameof(ResolvedFeatureFilePath));
                }
            }
        }
    }
}

using System.Collections.ObjectModel;
using System.IO;

namespace GuiFunctions
{
    public class FromFileFeatureMappingRowModel : BaseViewModel
    {
        private string _selectedCondition = string.Empty;
        private string _resolvedFeatureFilePath = string.Empty;

        public string MassSpecFilePath { get; }
        public string MassSpecFileName { get; }

        public ObservableCollection<string> AvailableConditions { get; }

        public string SelectedCondition
        {
            get => _selectedCondition;
            set
            {
                if (_selectedCondition != value)
                {
                    _selectedCondition = value;
                    OnPropertyChanged(nameof(SelectedCondition));
                    UpdateResolvedPath();
                }
            }
        }

        public string ResolvedFeatureFilePath
        {
            get => _resolvedFeatureFilePath;
            private set
            {
                if (_resolvedFeatureFilePath != value)
                {
                    _resolvedFeatureFilePath = value;
                    OnPropertyChanged(nameof(ResolvedFeatureFilePath));
                }
            }
        }

        private readonly System.Func<string, string, string> _featurePathResolver;

        public FromFileFeatureMappingRowModel(
            string massSpecFilePath, 
            System.Collections.Generic.IEnumerable<string> availableConditions, 
            System.Func<string, string, string> featurePathResolver)
        {
            MassSpecFilePath = massSpecFilePath;
            MassSpecFileName = Path.GetFileName(massSpecFilePath);
            AvailableConditions = new ObservableCollection<string>(availableConditions);
            _featurePathResolver = featurePathResolver;
        }

        private void UpdateResolvedPath()
        {
            if (string.IsNullOrEmpty(SelectedCondition))
            {
                ResolvedFeatureFilePath = string.Empty;
                return;
            }

            var path = _featurePathResolver?.Invoke(MassSpecFilePath, SelectedCondition);
            ResolvedFeatureFilePath = path ?? string.Empty;
        }
    }
}

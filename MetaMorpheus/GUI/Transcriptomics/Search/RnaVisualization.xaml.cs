using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MetaMorpheusGUI
{
    /// <summary>
    /// Interaction logic for RnaVisualization.xaml
    /// </summary>
    public partial class RnaVisualization : UserControl
    {
        public RnaVisualization()
        {
            InitializeComponent();
        }

        private void RnaVisualization_OnDrop(object sender, DragEventArgs e)
        {
            string[] files = ((string[])e.Data.GetData(DataFormats.FileDrop)).OrderBy(p => p).ToArray();
            (DataContext as RnaVisualizationVm).ParseDroppedFile(files);
        }

        private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = DataContext as RnaVisualizationVm;
            if (vm.SearchPersists)
                vm.TargetedSearch(PlotView, DrawnSequenceCanvas);
            else
                vm.DisplaySelected(PlotView, DrawnSequenceCanvas);
        }

        private void Settings_OnClick(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as RnaVisualizationVm;

            var selectedItem = vm.SelectedMatch;
            var settingsWindow = new MetaDrawSettingsWindow();
            var result = settingsWindow.ShowDialog();
            if (result == true)
            {
                vm.DisplaySelected(PlotView, DrawnSequenceCanvas);
            }
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            (DataContext as RnaVisualizationVm).TargetedSearch(PlotView, DrawnSequenceCanvas);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
            (DataContext as RnaVisualizationVm).DisplaySelected(PlotView, DrawnSequenceCanvas);
        }

        private void Settings_OnClick(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as RnaVisualizationVm;

            var selectedItem = vm.SelectedMatch;
            var settingsWindow = new MetaDrawSettingsWindow(vm.SettingsView);
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

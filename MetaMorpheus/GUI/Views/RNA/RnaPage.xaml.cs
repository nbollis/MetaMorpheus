using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MetaMorpheusGUI
{
    /// <summary>
    /// Interaction logic for RnaPage.xaml
    /// </summary>
    public partial class RnaPage : UserControl
    {
        public RnaPage()
        {
            InitializeComponent();
        }

        private void RnaPage_OnDrop(object sender, DragEventArgs e)
        {
            string[] files = ((string[])e.Data.GetData(DataFormats.FileDrop)).OrderBy(p => p).ToArray();
            (DataContext as RnaTargetedSearchVm).ParseDroppedFile(files);
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            var control = (((this.Parent as TabItem).Parent as TabControl));

            (control.DataContext as RnaBigVm).VisualizationVm =
                new RnaVisualizationVm((DataContext as RnaTargetedSearchVm).DataFile,
                    (DataContext as RnaTargetedSearchVm).SpectralMatches);
            control.SelectedIndex = 1;

        }
    }
}

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

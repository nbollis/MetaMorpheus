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
using GuiFunctions;

namespace MetaMorpheusGUI.ClaireUwU
{
    /// <summary>
    /// Interaction logic for uwuControl.xaml
    /// </summary>
    public partial class uwuControl : UserControl
    {
        public uwuControl()
        {
            InitializeComponent();
            DataContext = new UwuVM();
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            (DataContext as UwuVM).CalculateProteinInfo();
        }

        private void AnalysisButton_OnClick(object sender, RoutedEventArgs e)
        {
            (DataContext as UwuVM).RunAnalysis();
        }
    }
}

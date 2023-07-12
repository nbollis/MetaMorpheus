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
        UwuVM ViewModel { get; set; }
        public uwuControl()
        {
            InitializeComponent();
            ViewModel = new();
            DataContext = ViewModel;
        }

        private void AnalysisButton_OnClick(object sender, RoutedEventArgs e)
        {
            ViewModel.RunAnalysis();
        }

        private void ProteinSequenceTextBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ViewModel.ProteinSequence = ((TextBox)sender).Text;
            }
        }

        private void ExportButton_OnClick(object sender, RoutedEventArgs e)
        {
            ViewModel.ExportResults();
        }

        private void TargetMassTextBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ViewModel.TargetMass = double.Parse(((TextBox)sender).Text);
            }
        }

        private void MassDifTextBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ViewModel.MassDifference = double.Parse(((TextBox)sender).Text);
            }
        }
    }
}

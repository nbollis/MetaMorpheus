using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MetaMorpheusGUI
{
    /// <summary>
    /// Interaction logic for uwuControl.xaml
    /// </summary>
    public partial class ScramblerControl : UserControl
    {
        public ScramblerControl()
        {
            InitializeComponent();
        }

        private void AnalysisButton_OnClick(object sender, RoutedEventArgs e)
        {
            (DataContext as ScramblerVM)!.RunAnalysis();
        }

        private void ProteinSequenceTextBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                (DataContext as ScramblerVM)!.ProteinSequence = ((TextBox)sender).Text;
            }
        }

        private void ExportButton_OnClick(object sender, RoutedEventArgs e)
        {
            (DataContext as ScramblerVM)!.ExportResults();
        }

        private void TargetMassTextBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                (DataContext as ScramblerVM)!.TargetMass = double.Parse(((TextBox)sender).Text);
            }
        }

        private void MassDifTextBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                (DataContext as ScramblerVM)!.MassDifference = double.Parse(((TextBox)sender).Text);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Windows.Shapes;
using EngineLayer;
using GuiFunctions;
using GuiFunctions.Transcriptomics;
using MassSpectrometry;
using MzLibUtil;
using TaskLayer;
using Transcriptomics.Digestion;

namespace MetaMorpheusGUI
{
    /// <summary>
    /// Interaction logic for RnaSearchTaskWindow.xaml
    /// </summary>
    public partial class RnaSearchTaskWindow : Window
    {
        internal RnaSearchParametersViewModel TheTaskViewModel { get; private set; }
        internal RnaSearchTask TheTask { get; private set; }
        private readonly ObservableCollection<ModTypeForTreeViewModel> FixedModTypeForTreeViewObservableCollection = new ObservableCollection<ModTypeForTreeViewModel>();
        private readonly ObservableCollection<ModTypeForTreeViewModel> VariableModTypeForTreeViewObservableCollection = new ObservableCollection<ModTypeForTreeViewModel>();
        public RnaSearchTaskWindow(RnaSearchTask task = null)
        {
            InitializeComponent();
            TheTask = task ?? new RnaSearchTask();
            PopulateChoices();
            UpdateFieldsFromTask(TheTask);
            DataContext = TheTaskViewModel;
        }

        private void UpdateFieldsFromTask(RnaSearchTask task)
        {
            TheTaskViewModel = new RnaSearchParametersViewModel()
            {
                digestionParams = task.CommonParameters.DigestionParams as RnaDigestionParams,
                searchParams = task.SearchParameters,
            };
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void PopulateChoices()
        {
            foreach (var hm in GlobalVariables.AllModsKnown.Where(b => b.ValidModification == true).GroupBy(b => b.ModificationType))
            {
                var theModType = new ModTypeForTreeViewModel(hm.Key, false);
                FixedModTypeForTreeViewObservableCollection.Add(theModType);
                foreach (var uah in hm)
                {
                    theModType.Children.Add(new ModForTreeViewModel(uah.ToString(), false, uah.IdWithMotif, false, theModType));
                }
            }
            FixedModsTreeView.DataContext = FixedModTypeForTreeViewObservableCollection;

            foreach (var hm in GlobalVariables.AllModsKnown.Where(b => b.ValidModification == true).GroupBy(b => b.ModificationType))
            {
                var theModType = new ModTypeForTreeViewModel(hm.Key, false);
                VariableModTypeForTreeViewObservableCollection.Add(theModType);
                foreach (var uah in hm)
                {
                    theModType.Children.Add(new ModForTreeViewModel(uah.ToString(), false, uah.IdWithMotif, false, theModType));
                }
            }
            VariableModsTreeView.DataContext = VariableModTypeForTreeViewObservableCollection;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            RnaSearchParameters searchParams = TheTaskViewModel.searchParams;
            RnaDigestionParams digestionParams = TheTaskViewModel.digestionParams;
            try
            {
                Rnase rnase = RnaseDictionary.Dictionary[TheTaskViewModel.SelectedRnase];
                digestionParams.GetType().GetProperty("Rnase")?.SetValue(digestionParams, rnase);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Rnase loading error with message: {ex.Message}");
            }

            var listOfModsVariable = new List<(string, string)>();
            foreach (var heh in VariableModTypeForTreeViewObservableCollection)
            {
                listOfModsVariable.AddRange(heh.Children.Where(b => b.Use).Select(b => (b.Parent.DisplayName, b.ModName)));
            }

            var listOfModsFixed = new List<(string, string)>();
            foreach (var heh in FixedModTypeForTreeViewObservableCollection)
            {
                listOfModsFixed.AddRange(heh.Children.Where(b => b.Use).Select(b => (b.Parent.DisplayName, b.ModName)));
            }

            CommonParameters commonParams = new
            (
                taskDescriptor: OutputFileNameTextBox.Text != "" ? OutputFileNameTextBox.Text : "RnaSearchTask",
                maxThreadsToUsePerFile: TheTaskViewModel.MaxThreads,
                digestionParams: digestionParams,
                listOfModsVariable: listOfModsVariable,
                listOfModsFixed: listOfModsFixed,
                dissociationType: TheTaskViewModel.DissociationType,
                deconvolutionMaxAssumedChargeState: TheTaskViewModel.MaxAssumedChargeState,
                deconvolutionMassTolerance: new PpmTolerance(TheTaskViewModel.DeconvolutionMassTolerance),
                precursorMassTolerance: new PpmTolerance(TheTaskViewModel.PrecursorMassTolerance),
                productMassTolerance: new PpmTolerance(TheTaskViewModel.ProductMassTolerance),
                doPrecursorDeconvolution: TheTaskViewModel.DoPrecursorDeconvolution,
                useProvidedPrecursorInfo: TheTaskViewModel.UseProvidedPrecursorInfo,
                qValueThreshold: TheTaskViewModel.QValueCutoff,
                scoreCutoff: TheTaskViewModel.ScoreCutoff,
                deconvolutionIntensityRatio: 3
            );
            TheTask = new RnaSearchTask()
            {
                CommonParameters = commonParams,
                SearchParameters = searchParams,
            };

            DialogResult = true;
        }

        private void SaveAsDefault_Click(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}

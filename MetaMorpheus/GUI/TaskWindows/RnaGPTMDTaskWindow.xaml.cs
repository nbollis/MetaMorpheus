using EngineLayer;
using MassSpectrometry;
using MzLibUtil;
using Nett;
using Proteomics.ProteolyticDigestion;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TaskLayer;
using GuiFunctions;
using Omics.Digestion;
using Transcriptomics.Digestion;
using UsefulProteomicsDatabases;

namespace MetaMorpheusGUI
{
    /// <summary>
    /// Interaction logic for GptmdTaskWindow.xaml
    /// </summary>
    public partial class RnaGptmdTaskWindow : Window
    {
        private readonly ObservableCollection<ModTypeForTreeViewModel> FixedModTypeForTreeViewObservableCollection = new ObservableCollection<ModTypeForTreeViewModel>();
        private readonly ObservableCollection<ModTypeForTreeViewModel> VariableModTypeForTreeViewObservableCollection = new ObservableCollection<ModTypeForTreeViewModel>();
        private readonly ObservableCollection<ModTypeForLoc> LocalizeModTypeForTreeViewObservableCollection = new ObservableCollection<ModTypeForLoc>();
        private readonly ObservableCollection<ModTypeForTreeViewModel> GptmdModTypeForTreeViewObservableCollection = new ObservableCollection<ModTypeForTreeViewModel>();
        private bool AutomaticallyAskAndOrUpdateParametersBasedOnProtease = true;
        private CustomFragmentationWindow CustomFragmentationWindow;

        public RnaGptmdTaskWindow(GptmdTask myGPTMDtask)
        {
            InitializeComponent();

            // if no default saved, create a new task and override protein specific defaults
            TheTask = myGPTMDtask ?? new GptmdTask()
            {
                CommonParameters = new CommonParameters(
                    digestionParams: new RnaDigestionParams("RNase T1", 3),
                    taskDescriptor: "RnaGptmd",
                    listOfModsFixed: new List<(string, string)>(),
                    listOfModsVariable: new List<(string, string)>(),
                    deconvolutionMaxAssumedChargeState: -12,
                    trimMsMsPeaks: false)
                {
                    DeconvolutionParameters = { Polarity = Polarity.Negative}
                },
                GptmdParameters = new()
                {
                    ListOfModsGptmd = new List<(string, string)>()
                }
            };

            AutomaticallyAskAndOrUpdateParametersBasedOnProtease = false;
            PopulateChoices();
            UpdateFieldsFromTask(TheTask);

            if (myGPTMDtask == null)
            {
                this.saveButton.Content = "Add the GPTMD Task";
            }

            SearchModifications.Timer.Tick += new EventHandler(TextChangeTimerHandler);
            base.Closing += this.OnClosing;
        }

        internal GptmdTask TheTask { get; private set; }

        private void UpdateFieldsFromTask(GptmdTask task)
        {
            // Digestion Parameters
            ProteaseComboBox.SelectedItem = task.CommonParameters.DigestionParams.DigestionAgent;
            MissedCleavagesTextBox.Text = task.CommonParameters.DigestionParams.MaxMissedCleavages.ToString();
            MinPeptideLengthTextBox.Text = task.CommonParameters.DigestionParams.MinLength.ToString();
            MaxPeptideLengthTextBox.Text = task.CommonParameters.DigestionParams.MaxLength.ToString();
            MaxModIsoformsTextBox.Text = task.CommonParameters.DigestionParams.MaxModificationIsoforms.ToString();
            MaxModNumTextBox.Text = task.CommonParameters.DigestionParams.MaxMods.ToString();

            // Search Parameters
            PrecursorMassToleranceTextBox.Text = task.CommonParameters.PrecursorMassTolerance.Value.ToString();
            ProductMassToleranceTextBox.Text = task.CommonParameters.ProductMassTolerance.Value.ToString();
            DissociationTypeComboBox.SelectedItem = task.CommonParameters.DissociationType.ToString();
            QValueThresholdTextBox.Text = task.CommonParameters.QValueThreshold.ToString();
            ScoreCuttoffTextBox.Text = task.CommonParameters.ScoreCutoff.ToString();
     

            // deconvolution
            CustomFragmentationWindow = new CustomFragmentationWindow(task.CommonParameters.CustomIons, true);
            DeconTolerance.Text = task.CommonParameters.DeconvolutionMassTolerance.Value.ToString();
            DeconvolutePrecursors.IsChecked = task.CommonParameters.DoPrecursorDeconvolution;
            UseProvidedPrecursor.IsChecked = task.CommonParameters.UseProvidedPrecursorInfo;
            DeconvolutionMaxAssumedChargeStateTextBox.Text = task.CommonParameters.DeconvolutionMaxAssumedChargeState.ToString();

            // peak trimming
            TrimMsMs.IsChecked = task.CommonParameters.TrimMsMsPeaks;
            TrimMs1.IsChecked = task.CommonParameters.TrimMs1Peaks;
            NumberOfPeaksToKeepPerWindowTextBox.Text = task.CommonParameters.NumberOfPeaksToKeepPerWindow == int.MaxValue || !task.CommonParameters.NumberOfPeaksToKeepPerWindow.HasValue ? "" : task.CommonParameters.NumberOfPeaksToKeepPerWindow.Value.ToString(CultureInfo.InvariantCulture);
            MinimumAllowedIntensityRatioToBasePeakTexBox.Text = task.CommonParameters.MinimumAllowedIntensityRatioToBasePeak == double.MaxValue || !task.CommonParameters.MinimumAllowedIntensityRatioToBasePeak.HasValue ? "" : task.CommonParameters.MinimumAllowedIntensityRatioToBasePeak.Value.ToString(CultureInfo.InvariantCulture);
            WindowWidthThomsonsTextBox.Text = task.CommonParameters.WindowWidthThomsons == double.MaxValue || !task.CommonParameters.WindowWidthThomsons.HasValue ? "" : task.CommonParameters.WindowWidthThomsons.Value.ToString(CultureInfo.InvariantCulture);
            NumberOfWindowsTextBox.Text = task.CommonParameters.NumberOfWindows == int.MaxValue || !task.CommonParameters.NumberOfWindows.HasValue ? "" : task.CommonParameters.NumberOfWindows.Value.ToString(CultureInfo.InvariantCulture);
            NormalizePeaksInWindowCheckBox.IsChecked = task.CommonParameters.NormalizePeaksAccrossAllWindows;


            CustomFragmentationWindow = new CustomFragmentationWindow(task.CommonParameters.CustomIons);
            OutputFileNameTextBox.Text = task.CommonParameters.TaskDescriptor;
            MaxThreadsTextBox.Text = task.CommonParameters.MaxThreadsToUsePerFile.ToString(CultureInfo.InvariantCulture);

            // modifications 
            foreach (var mod in task.CommonParameters.ListOfModsFixed)
            {
                var theModType = FixedModTypeForTreeViewObservableCollection.FirstOrDefault(b => b.DisplayName.Equals(mod.Item1));
                if (theModType != null)
                {
                    var theMod = theModType.Children.FirstOrDefault(b => b.ModName.Equals(mod.Item2));
                    if (theMod != null)
                    {
                        theMod.Use = true;
                    }
                    else
                    {
                        theModType.Children.Add(new ModForTreeViewModel("UNKNOWN MODIFICATION!", true, mod.Item2, true, theModType));
                    }
                }
                else
                {
                    theModType = new ModTypeForTreeViewModel(mod.Item1, true);
                    FixedModTypeForTreeViewObservableCollection.Add(theModType);
                    theModType.Children.Add(new ModForTreeViewModel("UNKNOWN MODIFICATION!", true, mod.Item2, true, theModType));
                }
            }
            foreach (var mod in task.CommonParameters.ListOfModsVariable)
            {
                ModTypeForTreeViewModel theModType = VariableModTypeForTreeViewObservableCollection.FirstOrDefault(b => b.DisplayName.Equals(mod.Item1));
                if (theModType != null)
                {
                    var theMod = theModType.Children.FirstOrDefault(b => b.ModName.Equals(mod.Item2));
                    if (theMod != null)
                    {
                        theMod.Use = true;
                    }
                    else
                    {
                        theModType.Children.Add(new ModForTreeViewModel("UNKNOWN MODIFICATION!", true, mod.Item2, true, theModType));
                    }
                }
                else
                {
                    theModType = new ModTypeForTreeViewModel(mod.Item1, true);
                    VariableModTypeForTreeViewObservableCollection.Add(theModType);
                    theModType.Children.Add(new ModForTreeViewModel("UNKNOWN MODIFICATION!", true, mod.Item2, true, theModType));
                }
            }

            foreach (var heh in LocalizeModTypeForTreeViewObservableCollection)
            {
                heh.Use = false;
            }

            foreach (var mod in task.GptmdParameters.ListOfModsGptmd)
            {
                ModTypeForTreeViewModel theModType = GptmdModTypeForTreeViewObservableCollection.FirstOrDefault(b => b.DisplayName.Equals(mod.Item1));
                if (theModType != null)
                {
                    var theMod = theModType.Children.FirstOrDefault(b => b.ModName.Equals(mod.Item2));
                    if (theMod != null)
                    {
                        theMod.Use = true;
                    }
                    else
                    {
                        theModType.Children.Add(new ModForTreeViewModel("UNKNOWN MODIFICATION!", true, mod.Item2, true, theModType));
                    }
                }
                else
                {
                    theModType = new ModTypeForTreeViewModel(mod.Item1, true);
                    GptmdModTypeForTreeViewObservableCollection.Add(theModType);
                    theModType.Children.Add(new ModForTreeViewModel("UNKNOWN MODIFICATION!", true, mod.Item2, true, theModType));
                }
            }

            foreach (var ye in VariableModTypeForTreeViewObservableCollection)
            {
                ye.VerifyCheckState();
            }
            foreach (var ye in FixedModTypeForTreeViewObservableCollection)
            {
                ye.VerifyCheckState();
            }

            foreach (var ye in GptmdModTypeForTreeViewObservableCollection)
            {
                ye.VerifyCheckState();
            }
        }

        private void PopulateChoices()
        {
            // digestion
            foreach (Rnase protease in RnaseDictionary.Dictionary.Values)
            {
                ProteaseComboBox.Items.Add(protease);
            }
            Rnase t1 = RnaseDictionary.Dictionary["RNase T1"];
            ProteaseComboBox.SelectedItem = t1;

            // search
            foreach (string dissassociationType in GlobalVariables.AllSupportedDissociationTypes.Keys)
            {
                DissociationTypeComboBox.Items.Add(dissassociationType);
            }

            ProductMassToleranceComboBox.Items.Add("Da");
            ProductMassToleranceComboBox.Items.Add("ppm");
            PrecursorMassToleranceComboBox.Items.Add("Da");
            PrecursorMassToleranceComboBox.Items.Add("ppm");

            foreach (var hm in GlobalVariables.AllRnaModsKnown.GroupBy(b => b.ModificationType))
            {
                var theModType = new ModTypeForTreeViewModel(hm.Key, false);
                FixedModTypeForTreeViewObservableCollection.Add(theModType);
                foreach (var uah in hm)
                {
                    theModType.Children.Add(new ModForTreeViewModel(uah.ToString(), false, uah.IdWithMotif, false, theModType));
                }
            }
            fixedModsTreeView.DataContext = FixedModTypeForTreeViewObservableCollection;
            foreach (var hm in GlobalVariables.AllRnaModsKnown.GroupBy(b => b.ModificationType))
            {
                var theModType = new ModTypeForTreeViewModel(hm.Key, false);
                VariableModTypeForTreeViewObservableCollection.Add(theModType);
                foreach (var uah in hm)
                {
                    theModType.Children.Add(new ModForTreeViewModel(uah.ToString(), false, uah.IdWithMotif, false, theModType));
                }
            }
            variableModsTreeView.DataContext = VariableModTypeForTreeViewObservableCollection;

            foreach (var hm in GlobalVariables.AllRnaModsKnown.GroupBy(b => b.ModificationType))
            {
                var theModType = new ModTypeForTreeViewModel(hm.Key, false);
                GptmdModTypeForTreeViewObservableCollection.Add(theModType);
                foreach (var uah in hm)
                {
                    theModType.Children.Add(new ModForTreeViewModel(uah.ToString(), false, uah.IdWithMotif, false, theModType));
                }
            }
            gptmdModsTreeView.DataContext = GptmdModTypeForTreeViewObservableCollection;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string fieldNotUsed = "1";

            if (!GlobalGuiSettings.CheckTaskSettingsValidity(PrecursorMassToleranceTextBox.Text, ProductMassToleranceTextBox.Text, MissedCleavagesTextBox.Text,
                 MaxModIsoformsTextBox.Text, MinPeptideLengthTextBox.Text, MaxPeptideLengthTextBox.Text, MaxThreadsTextBox.Text, ScoreCuttoffTextBox.Text,
                fieldNotUsed, fieldNotUsed, DeconvolutionMaxAssumedChargeStateTextBox.Text, NumberOfPeaksToKeepPerWindowTextBox.Text, MinimumAllowedIntensityRatioToBasePeakTexBox.Text, 
                null, null, fieldNotUsed, fieldNotUsed, fieldNotUsed, null, null, null))
            {
                return;
            }

            DigestionAgent protease = (DigestionAgent)ProteaseComboBox.SelectedItem;
            int maxMissedCleavages = string.IsNullOrEmpty(MissedCleavagesTextBox.Text) ? int.MaxValue : (int.Parse(MissedCleavagesTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture));
            int minPeptideLength = int.Parse(MinPeptideLengthTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture);
            int maxPeptideLength = string.IsNullOrEmpty(MaxPeptideLengthTextBox.Text) ? int.MaxValue : (int.Parse(MaxPeptideLengthTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture));
            int maxModificationIsoforms = int.Parse(MaxModIsoformsTextBox.Text, CultureInfo.InvariantCulture);
            int maxModsPerPeptide = int.Parse(MaxModNumTextBox.Text, CultureInfo.InvariantCulture);

            DissociationType dissociationType = GlobalVariables.AllSupportedDissociationTypes[DissociationTypeComboBox.SelectedItem.ToString()];
            CustomFragmentationWindow.Close();

            Tolerance productMassTolerance;
            if (ProductMassToleranceComboBox.SelectedIndex == 0)
            {
                productMassTolerance = new AbsoluteTolerance(double.Parse(ProductMassToleranceTextBox.Text, CultureInfo.InvariantCulture));
            }
            else
            {
                productMassTolerance = new PpmTolerance(double.Parse(ProductMassToleranceTextBox.Text, CultureInfo.InvariantCulture));
            }

            Tolerance precursorMassTolerance;
            if (PrecursorMassToleranceComboBox.SelectedIndex == 0)
            {
                precursorMassTolerance = new AbsoluteTolerance(double.Parse(PrecursorMassToleranceTextBox.Text, CultureInfo.InvariantCulture));
            }
            else
            {
                precursorMassTolerance = new PpmTolerance(double.Parse(PrecursorMassToleranceTextBox.Text, CultureInfo.InvariantCulture));
            }

            List<(string, string)> listOfModsVariable = new List<(string, string)>();
            foreach (var heh in VariableModTypeForTreeViewObservableCollection)
            {
                listOfModsVariable.AddRange(heh.Children.Where(b => b.Use).Select(b => (b.Parent.DisplayName, b.ModName)));
            }

            if (!GlobalGuiSettings.VariableModCheck(listOfModsVariable))
            {
                return;
            }

            bool TrimMs1Peaks = TrimMs1.IsChecked.Value;
            bool TrimMsMsPeaks = TrimMsMs.IsChecked.Value;

            int? numPeaksToKeep = null;
            if (!string.IsNullOrWhiteSpace(NumberOfPeaksToKeepPerWindowTextBox.Text))
            {
                if (int.TryParse(NumberOfPeaksToKeepPerWindowTextBox.Text, out int numberOfPeaksToKeeep))
                {
                    numPeaksToKeep = numberOfPeaksToKeeep;
                }
                else
                {
                    MessageBox.Show("The value that you entered for number of peaks to keep is not acceptable. Try again.");
                    return;
                }
            }

            double? minimumAllowedIntensityRatioToBasePeak = null;
            if (!string.IsNullOrWhiteSpace(MinimumAllowedIntensityRatioToBasePeakTexBox.Text))
            {
                if (double.TryParse(MinimumAllowedIntensityRatioToBasePeakTexBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double minimumAllowedIntensityRatio))
                {
                    minimumAllowedIntensityRatioToBasePeak = minimumAllowedIntensityRatio;
                }
                else
                {
                    MessageBox.Show("The value that you entered for minimum allowed intensity ratio to keep is not acceptable. Try again.");
                    return;
                }
            }

            List<(string, string)> listOfModsFixed = new List<(string, string)>();
            foreach (var heh in FixedModTypeForTreeViewObservableCollection)
            {
                listOfModsFixed.AddRange(heh.Children.Where(b => b.Use).Select(b => (b.Parent.DisplayName, b.ModName)));
            }
            bool parseMaxThreadsPerFile = int.Parse(MaxThreadsTextBox.Text, CultureInfo.InvariantCulture) <= Environment.ProcessorCount && int.Parse(MaxThreadsTextBox.Text, CultureInfo.InvariantCulture) > 0;

            CommonParameters commonParamsToSave = new CommonParameters(
                useProvidedPrecursorInfo: UseProvidedPrecursor.IsChecked.Value,
                deconvolutionMaxAssumedChargeState: int.Parse(DeconvolutionMaxAssumedChargeStateTextBox.Text, CultureInfo.InvariantCulture),
                doPrecursorDeconvolution: DeconvolutePrecursors.IsChecked.Value,
                taskDescriptor: OutputFileNameTextBox.Text != "" ? OutputFileNameTextBox.Text : "RnaGPTMDTask",
                maxThreadsToUsePerFile: parseMaxThreadsPerFile ? int.Parse(MaxThreadsTextBox.Text, CultureInfo.InvariantCulture) : new CommonParameters().MaxThreadsToUsePerFile,
                digestionParams: new RnaDigestionParams(
                    rnase: protease.Name,
                    maxMissedCleavages: maxMissedCleavages,
                    minLength: minPeptideLength,
                    maxLength: maxPeptideLength,
                    maxModificationIsoforms: maxModificationIsoforms,
                    maxMods: maxModsPerPeptide),
                    dissociationType: dissociationType,
                    scoreCutoff: double.Parse(ScoreCuttoffTextBox.Text, CultureInfo.InvariantCulture),
                    precursorMassTolerance: precursorMassTolerance,
                    productMassTolerance: productMassTolerance,                    
                    trimMs1Peaks: TrimMs1Peaks,
                    trimMsMsPeaks: TrimMsMsPeaks,
                    numberOfPeaksToKeepPerWindow: numPeaksToKeep,
                    minimumAllowedIntensityRatioToBasePeak: minimumAllowedIntensityRatioToBasePeak,
                    listOfModsFixed: listOfModsFixed,
                    listOfModsVariable: listOfModsVariable);

            TheTask.GptmdParameters.ListOfModsGptmd = new List<(string, string)>();
            foreach (var heh in GptmdModTypeForTreeViewObservableCollection)
            {
                TheTask.GptmdParameters.ListOfModsGptmd.AddRange(heh.Children.Where(b => b.Use).Select(b => (b.Parent.DisplayName, b.ModName)));
            }

            TheTask.CommonParameters = commonParamsToSave;

            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            CustomFragmentationWindow.Close();
        }

        private void CheckIfNumber(object sender, TextCompositionEventArgs e)
        {
            e.Handled = GlobalGuiSettings.CheckIsPositiveInteger(e.Text);
        }

        private void KeyPressed(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                SaveButton_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                CancelButton_Click(sender, e);
            }
        }

        private void TextChanged_Fixed(object sender, TextChangedEventArgs args)
        {
            SearchModifications.SetTimer();
            SearchModifications.FixedSearch = true;
        }

        private void TextChanged_Var(object sender, TextChangedEventArgs args)
        {
            SearchModifications.SetTimer();
            SearchModifications.VariableSearch = true;
        }

        private void TextChanged_GPTMD(object sender, TextChangedEventArgs args)
        {
            SearchModifications.SetTimer();
            SearchModifications.GptmdSearch = true;
        }

        private void TextChangeTimerHandler(object sender, EventArgs e)
        {
            if (SearchModifications.FixedSearch)
            {
                SearchModifications.FilterTree(SearchFixMod, fixedModsTreeView, FixedModTypeForTreeViewObservableCollection);
                SearchModifications.FixedSearch = false;
            }

            if (SearchModifications.VariableSearch)
            {
                SearchModifications.FilterTree(SearchVarMod, variableModsTreeView, VariableModTypeForTreeViewObservableCollection);
                SearchModifications.VariableSearch = false;
            }

            if (SearchModifications.GptmdSearch)
            {
                SearchModifications.FilterTree(SearchGPTMD, gptmdModsTreeView, GptmdModTypeForTreeViewObservableCollection);
                SearchModifications.GptmdSearch = false;
            }
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            SearchModifications.Timer.Tick -= new EventHandler(TextChangeTimerHandler);
            // remove event handler from timer
            // keeping it will trigger an exception because the closed window stops existing

            CustomFragmentationWindow.Close();
        }

        private void SaveAsDefault_Click(object sender, RoutedEventArgs e)
        {
            SaveButton_Click(sender, e);
            Toml.WriteFile(TheTask, Path.Combine(GlobalVariables.DataDir, "DefaultParameters", @"RnaGptmdTaskDefault.toml"), MetaMorpheusTask.tomlConfig);
        }

        private void DissociationTypeComboBox_OnDropDownClosed(object sender, EventArgs e)
        {
            if (DissociationTypeComboBox.SelectedItem.ToString()!.Equals(DissociationType.Custom.ToString()))
            {
                CustomFragmentationWindow.Show();
            }
        }
    }
}
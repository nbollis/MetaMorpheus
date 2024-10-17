using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
using Nett;
using Omics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using TaskLayer;
using Transcriptomics.Digestion;
using UsefulProteomicsDatabases;

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
        private CustomFragmentationWindow CustomFragmentationWindow;
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
            ProductMassToleranceComboBox.SelectedIndex = task.CommonParameters.ProductMassTolerance is AbsoluteTolerance ? 0 : 1;
            PrecursorMassToleranceComboBox.SelectedIndex = task.CommonParameters.PrecursorMassTolerance is AbsoluteTolerance ? 0 : 1;
            DissociationTypeComboBox.SelectedItem = task.CommonParameters.DissociationType.ToString();
            QValueThresholdTextBox.Text = task.CommonParameters.QValueThreshold.ToString();
            ScoreCuttoffTextBox.Text = task.CommonParameters.ScoreCutoff.ToString();
            UseDecoysCheckBox.IsChecked = task.SearchParameters.DecoyType != DecoyType.None;
            MaxThreadsTextBox.Text = task.CommonParameters.MaxThreadsToUsePerFile.ToString(CultureInfo.InvariantCulture);
            foreach (var mod in task.CommonParameters.ListOfModsFixed)
            {
                var theModType = FixedModTypeForTreeViewObservableCollection.FirstOrDefault(b => b.DisplayName.Equals(mod.Item1));
                if (theModType != null)
                {
                    var theMod = theModType.Children.FirstOrDefault(b => b.ModName.Equals(mod.Item2));
                    if (theMod != null)
                        theMod.Use = true;
                    else
                        theModType.Children.Add(new ModForTreeViewModel("UNKNOWN MODIFICATION!", true, mod.Item2, true, theModType));
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
                var theModType = VariableModTypeForTreeViewObservableCollection.FirstOrDefault(b => b.DisplayName.Equals(mod.Item1));
                if (theModType != null)
                {
                    var theMod = theModType.Children.FirstOrDefault(b => b.ModName.Equals(mod.Item2));
                    if (theMod != null)
                        theMod.Use = true;
                    else
                        theModType.Children.Add(new ModForTreeViewModel("UNKNOWN MODIFICATION!", true, mod.Item2, true, theModType));
                }
                else
                {
                    theModType = new ModTypeForTreeViewModel(mod.Item1, true);
                    VariableModTypeForTreeViewObservableCollection.Add(theModType);
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

            // deconvolution
            CustomFragmentationWindow = new CustomFragmentationWindow(task.CommonParameters.CustomIons, true);
            DeconTolerance.Text = task.CommonParameters.DeconvolutionMassTolerance.Value.ToString();
            DeconvolutePrecursors.IsChecked = task.CommonParameters.DoPrecursorDeconvolution;
            UseProvidedPrecursor.IsChecked = task.CommonParameters.UseProvidedPrecursorInfo;
            DeconvolutionMaxAssumedChargeStateTextBox.Text = task.CommonParameters.DeconvolutionMaxAssumedChargeState.ToString();

            // mass diff acceptor
            MassDiffAcceptExact.IsChecked = task.SearchParameters.MassDiffAcceptorType == MassDiffAcceptorType.Exact;
            MassDiffAccept1mm.IsChecked = task.SearchParameters.MassDiffAcceptorType == MassDiffAcceptorType.OneMM;
            MassDiffAccept2mm.IsChecked = task.SearchParameters.MassDiffAcceptorType == MassDiffAcceptorType.TwoMM;
            MassDiffAccept3mm.IsChecked = task.SearchParameters.MassDiffAcceptorType == MassDiffAcceptorType.ThreeMM;
            MassDiffAcceptPlusOrMinusThree.IsChecked = task.SearchParameters.MassDiffAcceptorType == MassDiffAcceptorType.PlusOrMinusThreeMM;
            MassDiffAccept187.IsChecked = task.SearchParameters.MassDiffAcceptorType == MassDiffAcceptorType.ModOpen;
            MassDiffAcceptOpen.IsChecked = task.SearchParameters.MassDiffAcceptorType == MassDiffAcceptorType.Open;
            MassDiffAcceptCustom.IsChecked = task.SearchParameters.MassDiffAcceptorType == MassDiffAcceptorType.Custom;
            if (task.SearchParameters.MassDiffAcceptorType == MassDiffAcceptorType.Custom)
            {
                CustomkMdacTextBox.Text = task.SearchParameters.CustomMdac;
            }

            // peak trimming
            TrimMsMs.IsChecked = task.CommonParameters.TrimMsMsPeaks;
            TrimMs1.IsChecked = task.CommonParameters.TrimMs1Peaks;
            NumberOfPeaksToKeepPerWindowTextBox.Text = task.CommonParameters.NumberOfPeaksToKeepPerWindow == int.MaxValue || !task.CommonParameters.NumberOfPeaksToKeepPerWindow.HasValue ? "" : task.CommonParameters.NumberOfPeaksToKeepPerWindow.Value.ToString(CultureInfo.InvariantCulture);
            MinimumAllowedIntensityRatioToBasePeakTexBox.Text = task.CommonParameters.MinimumAllowedIntensityRatioToBasePeak == double.MaxValue || !task.CommonParameters.MinimumAllowedIntensityRatioToBasePeak.HasValue ? "" : task.CommonParameters.MinimumAllowedIntensityRatioToBasePeak.Value.ToString(CultureInfo.InvariantCulture);
            WindowWidthThomsonsTextBox.Text = task.CommonParameters.WindowWidthThomsons == double.MaxValue || !task.CommonParameters.WindowWidthThomsons.HasValue ? "" : task.CommonParameters.WindowWidthThomsons.Value.ToString(CultureInfo.InvariantCulture);
            NumberOfWindowsTextBox.Text = task.CommonParameters.NumberOfWindows == int.MaxValue || !task.CommonParameters.NumberOfWindows.HasValue ? "" : task.CommonParameters.NumberOfWindows.Value.ToString(CultureInfo.InvariantCulture);
            NormalizePeaksInWindowCheckBox.IsChecked = task.CommonParameters.NormalizePeaksAccrossAllWindows;

            // Output Parameters
            OutputFileNameTextBox.Text = task.CommonParameters.TaskDescriptor ?? "RnaSearchTask";
            WriteHighQPsmsCheckBox.IsChecked = task.SearchParameters.WriteHighQValueSpectralMatches;
            WriteDecoyCheckBox.IsChecked = task.SearchParameters.WriteDecoys;
            WriteContaminantCheckBox.IsChecked = task.SearchParameters.WriteContaminants;
            WriteAmbiguousCheckBox.IsChecked = task.SearchParameters.WriteAmbiguous;
            WriteIndividualFilesCheckBox.IsChecked = task.SearchParameters.WriteIndividualFiles;

        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
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

            // modifications
            foreach (var hm in GlobalVariables.AllRnaModsKnown.Where(b => b.ValidModification == true).GroupBy(b => b.ModificationType))
            {
                var theModType = new ModTypeForTreeViewModel(hm.Key, false);
                FixedModTypeForTreeViewObservableCollection.Add(theModType);
                foreach (var uah in hm)
                {
                    theModType.Children.Add(new ModForTreeViewModel(uah.ToString(), false, uah.IdWithMotif, false, theModType));
                }
            }
            FixedModsTreeView.DataContext = FixedModTypeForTreeViewObservableCollection;

            foreach (var hm in GlobalVariables.AllRnaModsKnown.Where(b => b.ValidModification == true).GroupBy(b => b.ModificationType))
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
            // digestion params
            var rnase = (Rnase)ProteaseComboBox.SelectedItem;
            DissociationType dissociationType = GlobalVariables.AllSupportedDissociationTypes[DissociationTypeComboBox.SelectedItem.ToString()];

            int maxMissedCleavages = string.IsNullOrEmpty(MissedCleavagesTextBox.Text) ? int.MaxValue : (int.Parse(MissedCleavagesTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture));
            int minPeptideLengthValue = (int.Parse(MinPeptideLengthTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture));
            int maxPeptideLengthValue = string.IsNullOrEmpty(MaxPeptideLengthTextBox.Text) ? int.MaxValue : (int.Parse(MaxPeptideLengthTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture));
            int maxModificationIsoformsValue = (int.Parse(MaxModIsoformsTextBox.Text, CultureInfo.InvariantCulture));
            int maxModsForPeptideValue = (int.Parse(MaxModNumTextBox.Text, CultureInfo.InvariantCulture));

            var digestionParams = new RnaDigestionParams(rnase.Name, maxMissedCleavages, minPeptideLengthValue,
                maxPeptideLengthValue, maxModificationIsoformsValue, maxModsForPeptideValue, FragmentationTerminus.Both);

            // modifications
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

            // common parameters
            Tolerance precursorTolerance;
            if (PrecursorMassToleranceComboBox.SelectedIndex == 0)
                precursorTolerance = new AbsoluteTolerance(double.Parse(PrecursorMassToleranceTextBox.Text, CultureInfo.InvariantCulture));
            else
                precursorTolerance = new PpmTolerance(double.Parse(PrecursorMassToleranceTextBox.Text, CultureInfo.InvariantCulture));

            Tolerance productTolerance;
            if (ProductMassToleranceComboBox.SelectedIndex == 0)
                productTolerance = new AbsoluteTolerance(double.Parse(ProductMassToleranceTextBox.Text, CultureInfo.InvariantCulture));
            else
                productTolerance = new PpmTolerance(double.Parse(ProductMassToleranceTextBox.Text, CultureInfo.InvariantCulture));


            var deconMassTolerance = new PpmTolerance(double.Parse(DeconTolerance.Text, CultureInfo.InvariantCulture));
            var qValueThreshold = double.Parse(QValueThresholdTextBox.Text, CultureInfo.InvariantCulture);
            var deconMaxChargeState = int.Parse(DeconvolutionMaxAssumedChargeStateTextBox.Text, CultureInfo.InvariantCulture);
            var scoreCutoff = double.Parse(ScoreCuttoffTextBox.Text, CultureInfo.InvariantCulture);

            // peak trimming
            bool TrimMs1Peaks = TrimMs1.IsChecked.Value;
            bool TrimMsMsPeaks = TrimMsMs.IsChecked.Value;

            int? numPeaksToKeep = null;
            if (int.TryParse(NumberOfPeaksToKeepPerWindowTextBox.Text, out int numberOfPeaksToKeeep))
            {
                numPeaksToKeep = numberOfPeaksToKeeep;
            }

            double? minimumAllowedIntensityRatioToBasePeak = null;
            if (double.TryParse(MinimumAllowedIntensityRatioToBasePeakTexBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double minimumAllowedIntensityRatio))
            {
                minimumAllowedIntensityRatioToBasePeak = minimumAllowedIntensityRatio;
            }

            double? windowWidthThompsons = null;
            if (double.TryParse(WindowWidthThomsonsTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double windowWidth))
            {
                windowWidthThompsons = windowWidth;
            }

            int? numberOfWindows = null;
            if (int.TryParse(NumberOfWindowsTextBox.Text, out int numWindows))
            {
                numberOfWindows = numWindows;
            }

            bool normalizePeaksAccrossAllWindows = NormalizePeaksInWindowCheckBox.IsChecked.Value;

            bool parseMaxThreadsPerFile = !MaxThreadsTextBox.Text.Equals("") && (int.Parse(MaxThreadsTextBox.Text) <= Environment.ProcessorCount && int.Parse(MaxThreadsTextBox.Text) > 0);


            CommonParameters commonParams = new
            (
                taskDescriptor: OutputFileNameTextBox.Text != "" ? OutputFileNameTextBox.Text : "RnaSearchTask",
                maxThreadsToUsePerFile: parseMaxThreadsPerFile ? int.Parse(MaxThreadsTextBox.Text, CultureInfo.InvariantCulture) : new CommonParameters().MaxThreadsToUsePerFile,
                digestionParams: digestionParams,
                listOfModsVariable: listOfModsVariable,
                listOfModsFixed: listOfModsFixed,
                dissociationType: dissociationType,
                deconvolutionMaxAssumedChargeState: deconMaxChargeState,
                deconvolutionMassTolerance: deconMassTolerance,
                precursorMassTolerance: precursorTolerance,
                productMassTolerance: productTolerance,
                doPrecursorDeconvolution: DeconvolutePrecursors.IsChecked.Value,
                useProvidedPrecursorInfo: UseProvidedPrecursor.IsChecked.Value,
                qValueThreshold: qValueThreshold,
                scoreCutoff: scoreCutoff,
                deconvolutionIntensityRatio: 3,
                trimMsMsPeaks: TrimMsMsPeaks,
                trimMs1Peaks: TrimMs1Peaks,
                numberOfPeaksToKeepPerWindow: numPeaksToKeep,
                minimumAllowedIntensityRatioToBasePeak: minimumAllowedIntensityRatioToBasePeak,
                windowWidthThomsons: windowWidthThompsons,
                numberOfWindows: numberOfWindows,
                normalizePeaksAccrossAllWindows: normalizePeaksAccrossAllWindows
            );

            // search parameters
            var writeDecoys = WriteDecoyCheckBox.IsChecked.Value;
            var writeContaminants = WriteContaminantCheckBox.IsChecked.Value;
            var writeHighQualityMatches = WriteHighQPsmsCheckBox.IsChecked.Value;
            var writeAmbiguousMatches = WriteAmbiguousCheckBox.IsChecked.Value;
            var writeIndividualFiles = WriteIndividualFilesCheckBox.IsChecked.Value;

            TheTask.SearchParameters.WriteAmbiguous = writeAmbiguousMatches;
            TheTask.SearchParameters.WriteContaminants = writeContaminants;
            TheTask.SearchParameters.WriteDecoys = writeDecoys;
            TheTask.SearchParameters.WriteHighQValueSpectralMatches = writeHighQualityMatches;
            TheTask.SearchParameters.WriteIndividualFiles = writeIndividualFiles;
            TheTask.SearchParameters.DecoyType = UseDecoysCheckBox.IsChecked.Value ? DecoyType.Reverse : DecoyType.None;
            if (MassDiffAcceptExact.IsChecked.HasValue && MassDiffAcceptExact.IsChecked.Value)
            {
                TheTask.SearchParameters.MassDiffAcceptorType = MassDiffAcceptorType.Exact;
            }
            if (MassDiffAccept1mm.IsChecked.HasValue && MassDiffAccept1mm.IsChecked.Value)
            {
                TheTask.SearchParameters.MassDiffAcceptorType = MassDiffAcceptorType.OneMM;
            }
            if (MassDiffAccept2mm.IsChecked.HasValue && MassDiffAccept2mm.IsChecked.Value)
            {
                TheTask.SearchParameters.MassDiffAcceptorType = MassDiffAcceptorType.TwoMM;
            }
            if (MassDiffAccept3mm.IsChecked.HasValue && MassDiffAccept3mm.IsChecked.Value)
            {
                TheTask.SearchParameters.MassDiffAcceptorType = MassDiffAcceptorType.ThreeMM;
            }
            if (MassDiffAccept187.IsChecked.HasValue && MassDiffAccept187.IsChecked.Value)
            {
                TheTask.SearchParameters.MassDiffAcceptorType = MassDiffAcceptorType.ModOpen;
            }
            if (MassDiffAcceptOpen.IsChecked.HasValue && MassDiffAcceptOpen.IsChecked.Value)
            {
                TheTask.SearchParameters.MassDiffAcceptorType = MassDiffAcceptorType.Open;
            }
            if (MassDiffAcceptPlusOrMinusThree.IsChecked.HasValue && MassDiffAcceptPlusOrMinusThree.IsChecked.Value)
            {
                TheTask.SearchParameters.MassDiffAcceptorType = MassDiffAcceptorType.PlusOrMinusThreeMM;
            }
            if (MassDiffAcceptCustom.IsChecked.HasValue && MassDiffAcceptCustom.IsChecked.Value)
            {
                try
                {
                    MassDiffAcceptor customMassDiffAcceptor = SearchTask.GetMassDiffAcceptor(null, MassDiffAcceptorType.Custom, CustomkMdacTextBox.Text);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not parse custom mass difference acceptor: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                TheTask.SearchParameters.MassDiffAcceptorType = MassDiffAcceptorType.Custom;
                TheTask.SearchParameters.CustomMdac = CustomkMdacTextBox.Text;
            }

            TheTask.CommonParameters = commonParams;


            DialogResult = true;
        }

        private void SaveAsDefault_Click(object sender, RoutedEventArgs e)
        {
            SaveButton_Click(sender, e);
            Toml.WriteFile(TheTask,
                System.IO.Path.Combine(GlobalVariables.DataDir, "DefaultParameters", @"RnaSearchTaskDefault.toml"),
                MetaMorpheusTask.tomlConfig);
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

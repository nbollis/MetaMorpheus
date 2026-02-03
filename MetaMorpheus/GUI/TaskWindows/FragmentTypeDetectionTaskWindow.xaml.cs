using EngineLayer;
using GuiFunctions;
using MassSpectrometry;
using MzLibUtil;
using Nett;
using Omics.Digestion;
using Omics.Fragmentation;
using Omics.Modifications;
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
using TaskLayer;
using TaskLayer.FragmentTypeDetection;
using Transcriptomics.Digestion;
using UsefulProteomicsDatabases;

namespace MetaMorpheusGUI;

/// <summary>
/// Interaction logic for FragmentTypeDetectionTaskWindow.xaml
/// </summary>
public partial class FragmentTypeDetectionTaskWindow : Window
{
    private MassDifferenceAcceptorSelectionViewModel _massDifferenceAcceptorViewModel;
    private DeconHostViewModel DeconHostViewModel;

    internal FragmentTypeDetectionTask TheTask { get; private set; }
    public FragmentTypeDetectionTaskWindow(FragmentTypeDetectionTask task)
    {
        InitializeComponent();
        if (task is null) // Happens when there is no default saved. 
        {
            TheTask = new FragmentTypeDetectionTask();
            if (GuiGlobalParamsViewModel.Instance.IsRnaMode)
            {
                Title = "RNA Fragment Detection Task";
                TheTask.SearchParameters = new RnaSearchParameters();
                TheTask.CommonParameters = new CommonParameters("RnaFragmentDetectionTask", digestionParams: new RnaDigestionParams("RNase T1", 3), dissociationType: DissociationType.CID, deconvolutionMaxAssumedChargeState: -20, precursorMassTolerance: new PpmTolerance(15));
            }
            else
            {
                Title = "Fragment Detection Task";
            }
        }
        else
            TheTask = task;

        PopulateChoices();
        UpdateFieldsFromTask(TheTask);
        DeisotopingControl.DataContext = DeconHostViewModel;
        MassDifferenceAcceptorControl.DataContext = _massDifferenceAcceptorViewModel;



        SearchModifications.Timer.Tick += new EventHandler(TextChangeTimerHandler);
        base.Closing += this.OnClosing;
    }

    private void PopulateChoices()
    {
        bool isRnaMode = GuiGlobalParamsViewModel.Instance.IsRnaMode;
        List<Modification> modsToUse = isRnaMode ? GlobalVariables.AllRnaModsKnown.ToList() : GlobalVariables.AllModsKnown.ToList();

        foreach (string separationType in GlobalVariables.SeparationTypes)
        {
            SeparationTypeComboBox.Items.Add(separationType);
        }
        SeparationTypeComboBox.SelectedItem = "HPLC";

        ProductMassToleranceComboBox.Items.Add("Da");
        ProductMassToleranceComboBox.Items.Add("ppm");

        PrecursorMassToleranceComboBox.Items.Add("Da");
        PrecursorMassToleranceComboBox.Items.Add("ppm");

        foreach (var hm in modsToUse.Where(b => b.ValidModification == true).GroupBy(b => b.ModificationType))
        {
            var theModType = new ModTypeForTreeViewModel(hm.Key, false);
            FixedModTypeForTreeViewObservableCollection.Add(theModType);
            foreach (var uah in hm)
            {
                theModType.Children.Add(new ModForTreeViewModel(uah.ToString(), false, uah.IdWithMotif, false, theModType));
            }
        }
        FixedModsTreeView.DataContext = FixedModTypeForTreeViewObservableCollection;

        foreach (var hm in modsToUse.Where(b => b.ValidModification == true).GroupBy(b => b.ModificationType))
        {
            var theModType = new ModTypeForTreeViewModel(hm.Key, false);
            VariableModTypeForTreeViewObservableCollection.Add(theModType);
            foreach (var uah in hm)
            {
                theModType.Children.Add(new ModForTreeViewModel(uah.ToString(), false, uah.IdWithMotif, false, theModType));
            }
        }
        VariableModsTreeView.DataContext = VariableModTypeForTreeViewObservableCollection;

        if (isRnaMode)
        {
            foreach (Rnase rnase in RnaseDictionary.Dictionary.Values)
            {
                ProteaseComboBox.Items.Add(rnase);
            }
            Rnase t1 = RnaseDictionary.Dictionary["RNase T1"];
            ProteaseComboBox.SelectedItem = t1;
        }
        else
        {
            foreach (Protease protease in ProteaseDictionary.Dictionary.Values)
            {
                ProteaseComboBox.Items.Add(protease);
            }
            Protease trypsin = ProteaseDictionary.Dictionary["trypsin"];
            ProteaseComboBox.SelectedItem = trypsin;

            foreach (string initiatior_methionine_behavior in Enum.GetNames(typeof(InitiatorMethionineBehavior)))
            {
                InitiatorMethionineBehaviorComboBox.Items.Add(initiatior_methionine_behavior);
            }
        }
    }

    private void UpdateFieldsFromTask(FragmentTypeDetectionTask task)
    {
        MetaMorpheusEngine.DetermineAnalyteType(TheTask.CommonParameters);
        if (task.CommonParameters.DigestionParams is DigestionParams digestionParams)
        {
            ProteaseComboBox.SelectedItem = digestionParams.SpecificProtease; //needs to be first, so nonspecific can override if necessary
                                                                              //do these in if statements so as not to trigger the change
            InitiatorMethionineBehaviorComboBox.SelectedIndex = (int)digestionParams.InitiatorMethionineBehavior;
        }
        else
        {
            ProteaseComboBox.SelectedItem = task.CommonParameters.DigestionParams.DigestionAgent;
        }

        CheckBoxTarget.IsChecked = task.SearchParameters.SearchTarget;
        CheckBoxDecoy.IsChecked = task.SearchParameters.DecoyType != DecoyType.None;
        RadioButtonReverseDecoy.IsChecked = task.SearchParameters.DecoyType == DecoyType.Reverse;
        RadioButtonSlideDecoy.IsChecked = task.SearchParameters.DecoyType == DecoyType.Slide;
        MissedCleavagesTextBox.Text = task.CommonParameters.DigestionParams.MaxMissedCleavages == int.MaxValue ? "" : task.CommonParameters.DigestionParams.MaxMissedCleavages.ToString(CultureInfo.InvariantCulture);
        MinPeptideLengthTextBox.Text = task.CommonParameters.DigestionParams.MinLength.ToString(CultureInfo.InvariantCulture);
        MaxPeptideLengthTextBox.Text = task.CommonParameters.DigestionParams.MaxLength == int.MaxValue ? "" : task.CommonParameters.DigestionParams.MaxLength.ToString(CultureInfo.InvariantCulture);
        MaxFragmentMassTextBox.Text = task.SearchParameters.MaxFragmentSize.ToString(CultureInfo.InvariantCulture); //put after max peptide length to allow for override of auto
        maxModificationIsoformsTextBox.Text = task.CommonParameters.DigestionParams.MaxModificationIsoforms.ToString(CultureInfo.InvariantCulture);
        MaxModNumTextBox.Text = task.CommonParameters.DigestionParams.MaxMods.ToString(CultureInfo.InvariantCulture);

        ProductMassToleranceTextBox.Text = task.CommonParameters.ProductMassTolerance.Value.ToString(CultureInfo.InvariantCulture);
        ProductMassToleranceComboBox.SelectedIndex = task.CommonParameters.ProductMassTolerance is AbsoluteTolerance ? 0 : 1;
        PrecursorMassToleranceTextBox.Text = task.CommonParameters.PrecursorMassTolerance.Value.ToString(CultureInfo.InvariantCulture);
        PrecursorMassToleranceComboBox.SelectedIndex = task.CommonParameters.PrecursorMassTolerance is AbsoluteTolerance ? 0 : 1;
        AddCompIonCheckBox.IsChecked = task.CommonParameters.AddCompIons;

        TrimMs1.IsChecked = task.CommonParameters.TrimMs1Peaks;
        TrimMsMs.IsChecked = task.CommonParameters.TrimMsMsPeaks;

        DeconHostViewModel = new DeconHostViewModel(TheTask.CommonParameters.PrecursorDeconvolutionParameters,
            TheTask.CommonParameters.ProductDeconvolutionParameters,
            TheTask.CommonParameters.UseProvidedPrecursorInfo, TheTask.CommonParameters.DoPrecursorDeconvolution);
        DeisotopingControl.DataContext = DeconHostViewModel;

        NumberOfPeaksToKeepPerWindowTextBox.Text = task.CommonParameters.NumberOfPeaksToKeepPerWindow == int.MaxValue || !task.CommonParameters.NumberOfPeaksToKeepPerWindow.HasValue ? "" : task.CommonParameters.NumberOfPeaksToKeepPerWindow.Value.ToString(CultureInfo.InvariantCulture);
        MinimumAllowedIntensityRatioToBasePeakTexBox.Text = task.CommonParameters.MinimumAllowedIntensityRatioToBasePeak == double.MaxValue || !task.CommonParameters.MinimumAllowedIntensityRatioToBasePeak.HasValue ? "" : task.CommonParameters.MinimumAllowedIntensityRatioToBasePeak.Value.ToString(CultureInfo.InvariantCulture);
        WindowWidthThomsonsTextBox.Text = task.CommonParameters.WindowWidthThomsons == double.MaxValue || !task.CommonParameters.WindowWidthThomsons.HasValue ? "" : task.CommonParameters.WindowWidthThomsons.Value.ToString(CultureInfo.InvariantCulture);
        NumberOfWindowsTextBox.Text = task.CommonParameters.NumberOfWindows == int.MaxValue || !task.CommonParameters.NumberOfWindows.HasValue ? "" : task.CommonParameters.NumberOfWindows.Value.ToString(CultureInfo.InvariantCulture);
        NormalizePeaksInWindowCheckBox.IsChecked = task.CommonParameters.NormalizePeaksAccrossAllWindows;

        MaxThreadsTextBox.Text = task.CommonParameters.MaxThreadsToUsePerFile.ToString(CultureInfo.InvariantCulture);
        MinVariantDepthTextBox.Text = task.CommonParameters.MinVariantDepth.ToString(CultureInfo.InvariantCulture);
        MaxHeterozygousVariantsTextBox.Text = task.CommonParameters.MaxHeterozygousVariants.ToString(CultureInfo.InvariantCulture);

        if (task.CommonParameters.QValueThreshold < 1)
        {
            QValueThresholdTextBox.Text = task.CommonParameters.QValueThreshold.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            QValueThresholdTextBox.Text = "0.01";
        }

        if (task.CommonParameters.PepQValueThreshold < 1)
        {
            PepQValueThresholdTextBox.Text = task.CommonParameters.PepQValueThreshold.ToString(CultureInfo.InvariantCulture);
            PepQValueThresholdCheckbox.IsChecked = true;
        }
        else
        {
            PepQValueThresholdTextBox.Text = "0.01";
        }

        OutputFileNameTextBox.Text = task.CommonParameters.TaskDescriptor;

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
            var theModType = VariableModTypeForTreeViewObservableCollection.FirstOrDefault(b => b.DisplayName.Equals(mod.Item1));
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

        foreach (var ye in VariableModTypeForTreeViewObservableCollection)
        {
            ye.VerifyCheckState();
        }
        foreach (var ye in FixedModTypeForTreeViewObservableCollection)
        {
            ye.VerifyCheckState();
        }

        _massDifferenceAcceptorViewModel = new(task.SearchParameters.MassDiffAcceptorType, task.SearchParameters.CustomMdac, task.CommonParameters.PrecursorMassTolerance.Value);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        string fieldNotUsed = "1";
        CleavageSpecificity searchModeType = CleavageSpecificity.Full;

        if (!TaskValidator.CheckTaskSettingsValidity(
            PrecursorMassToleranceTextBox.Text,
            ProductMassToleranceTextBox.Text,
            MissedCleavagesTextBox.Text,
            maxModificationIsoformsTextBox.Text,
            MinPeptideLengthTextBox.Text,
            MaxPeptideLengthTextBox.Text,
            MaxThreadsTextBox.Text,
            fieldNotUsed,
            fieldNotUsed,
            fieldNotUsed,
            fieldNotUsed,
            DeconHostViewModel.PrecursorDeconvolutionParameters.MaxAssumedChargeState.ToString(),
            NumberOfPeaksToKeepPerWindowTextBox.Text,
            MinimumAllowedIntensityRatioToBasePeakTexBox.Text,
            WindowWidthThomsonsTextBox.Text,
            NumberOfWindowsTextBox.Text,
            fieldNotUsed,
            MaxModNumTextBox.Text,
            MaxFragmentMassTextBox.Text,
            QValueThresholdTextBox.Text,
            PepQValueThresholdTextBox.Text,
            null))
        {
            return;
        }

        DigestionAgent protease = (DigestionAgent)ProteaseComboBox.SelectedItem;

        string separationType = SeparationTypeComboBox.SelectedItem.ToString();

        DissociationType dissociationType = DissociationType.Custom;
        FragmentationTerminus fragmentationTerminus = FragmentationTerminus.Both;

        TheTask.SearchParameters.SilacLabels = null;
        TheTask.SearchParameters.StartTurnoverLabel = null;
        TheTask.SearchParameters.EndTurnoverLabel = null;        

        int maxMissedCleavages = string.IsNullOrEmpty(MissedCleavagesTextBox.Text) ? int.MaxValue : (int.Parse(MissedCleavagesTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture));
        int minPeptideLengthValue = (int.Parse(MinPeptideLengthTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture));
        int maxPeptideLengthValue = string.IsNullOrEmpty(MaxPeptideLengthTextBox.Text) ? int.MaxValue : (int.Parse(MaxPeptideLengthTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture));
        int MinVariantDepth = int.Parse(MinVariantDepthTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture);
        int MaxHeterozygousVariants = int.Parse(MaxHeterozygousVariantsTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture);
        int maxModificationIsoformsValue = (int.Parse(maxModificationIsoformsTextBox.Text, CultureInfo.InvariantCulture));
        int maxModsForPeptideValue = (int.Parse(MaxModNumTextBox.Text, CultureInfo.InvariantCulture));
        InitiatorMethionineBehavior initiatorMethionineBehavior = ((InitiatorMethionineBehavior)InitiatorMethionineBehaviorComboBox.SelectedIndex);
        IDigestionParams digestionParamsToSave;
        if (GuiGlobalParamsViewModel.Instance.IsRnaMode)
        {
            digestionParamsToSave = new RnaDigestionParams(protease.Name,
                maxMissedCleavages,
                minPeptideLengthValue,
                maxPeptideLengthValue,
                maxModificationIsoformsValue,
                maxModsForPeptideValue,
                fragmentationTerminus);
        }
        else
        {
            digestionParamsToSave = new DigestionParams(
                protease: protease.Name,
                maxMissedCleavages: maxMissedCleavages,
                minPeptideLength: minPeptideLengthValue,
                maxPeptideLength: maxPeptideLengthValue,
                maxModificationIsoforms: maxModificationIsoformsValue,
                initiatorMethionineBehavior: initiatorMethionineBehavior,
                maxModsForPeptides: maxModsForPeptideValue,
                searchModeType: searchModeType,
                fragmentationTerminus: fragmentationTerminus,
                generateUnlabeledProteinsForSilac: false);
        }


        Tolerance ProductMassTolerance;
        if (ProductMassToleranceComboBox.SelectedIndex == 0)
        {
            ProductMassTolerance = new AbsoluteTolerance(double.Parse(ProductMassToleranceTextBox.Text, CultureInfo.InvariantCulture));
        }
        else
        {
            ProductMassTolerance = new PpmTolerance(double.Parse(ProductMassToleranceTextBox.Text, CultureInfo.InvariantCulture));
        }

        Tolerance PrecursorMassTolerance;
        if (PrecursorMassToleranceComboBox.SelectedIndex == 0)
        {
            PrecursorMassTolerance = new AbsoluteTolerance(double.Parse(PrecursorMassToleranceTextBox.Text, CultureInfo.InvariantCulture));
        }
        else
        {
            PrecursorMassTolerance = new PpmTolerance(double.Parse(PrecursorMassToleranceTextBox.Text, CultureInfo.InvariantCulture));
        }
        TheTask.SearchParameters.MaxFragmentSize = double.Parse(MaxFragmentMassTextBox.Text, CultureInfo.InvariantCulture);

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

        if (!TaskValidator.VariableModCheck(listOfModsVariable))
        {
            return;
        }

        bool TrimMs1Peaks = TrimMs1.IsChecked.Value;
        bool TrimMsMsPeaks = TrimMsMs.IsChecked.Value;
        bool AddTruncations = false;

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


        DeconvolutionParameters precursorDeconvolutionParameters = DeconHostViewModel.PrecursorDeconvolutionParameters.Parameters;
        DeconvolutionParameters productDeconvolutionParameters = DeconHostViewModel.ProductDeconvolutionParameters.Parameters;
        bool useProvidedPrecursorInfo = DeconHostViewModel.UseProvidedPrecursors;
        bool doPrecursorDeconvolution = DeconHostViewModel.DoPrecursorDeconvolution;

        CommonParameters commonParamsToSave = new CommonParameters(
            taskDescriptor: OutputFileNameTextBox.Text != "" ? OutputFileNameTextBox.Text : "SearchTask",
            maxThreadsToUsePerFile: parseMaxThreadsPerFile ? int.Parse(MaxThreadsTextBox.Text, CultureInfo.InvariantCulture) : new CommonParameters().MaxThreadsToUsePerFile,
            reportAllAmbiguity: true,
            totalPartitions: 1,
            doPrecursorDeconvolution: doPrecursorDeconvolution,
            useProvidedPrecursorInfo: useProvidedPrecursorInfo,
            qValueThreshold: !PepQValueThresholdCheckbox.IsChecked.Value ? double.Parse(QValueThresholdTextBox.Text, CultureInfo.InvariantCulture) : 1.0,
            pepQValueThreshold: PepQValueThresholdCheckbox.IsChecked.Value ? double.Parse(PepQValueThresholdTextBox.Text, CultureInfo.InvariantCulture) : 1.0,
            scoreCutoff: 5,
            listOfModsFixed: listOfModsFixed,
            listOfModsVariable: listOfModsVariable,
            dissociationType: dissociationType,
            precursorMassTolerance: PrecursorMassTolerance,
            productMassTolerance: ProductMassTolerance,
            digestionParams: digestionParamsToSave,
            separationType: separationType,
            trimMs1Peaks: TrimMs1Peaks,
            trimMsMsPeaks: TrimMsMsPeaks,
            addTruncations: AddTruncations,
            numberOfPeaksToKeepPerWindow: numPeaksToKeep,
            minimumAllowedIntensityRatioToBasePeak: minimumAllowedIntensityRatioToBasePeak,
            windowWidthThomsons: windowWidthThompsons,
            numberOfWindows: numberOfWindows,//maybe change this some day
            normalizePeaksAccrossAllWindows: normalizePeaksAccrossAllWindows,//maybe change this some day
            addCompIons: AddCompIonCheckBox.IsChecked.Value,
            assumeOrphanPeaksAreZ1Fragments: protease.Name != "top-down",
            minVariantDepth: MinVariantDepth,
            maxHeterozygousVariants: MaxHeterozygousVariants,
            precursorDeconParams: precursorDeconvolutionParameters,
            productDeconParams: productDeconvolutionParameters);


        TheTask.SearchParameters.SearchType = SearchType.Classic;
        TheTask.SearchParameters.MinAllowedInternalFragmentLength = 0;
        TheTask.SearchParameters.SearchTarget = CheckBoxTarget.IsChecked.Value;
        TheTask.SearchParameters.TCAmbiguity = TargetContaminantAmbiguity.RemoveContaminant;


        //TheTask.SearchParameters.OutPepXML = ckbPepXML.IsChecked.Value;

        if (CheckBoxDecoy.IsChecked.Value)
        {
            if (RadioButtonReverseDecoy.IsChecked.Value)
            {
                TheTask.SearchParameters.DecoyType = DecoyType.Reverse;
            }
            else //if (radioButtonSlideDecoy.IsChecked.Value)
            {
                TheTask.SearchParameters.DecoyType = DecoyType.Slide;
            }
        }
        else
        {
            TheTask.SearchParameters.DecoyType = DecoyType.None;
        }

        // Custom Mdac will be "" for all non-custom types, so no need to check for those.
        if (_massDifferenceAcceptorViewModel.CustomMdac != string.Empty)
        {
            try
            {
                MassDiffAcceptor customMassDiffAcceptor =
                    SearchTask.GetMassDiffAcceptor(null, MassDiffAcceptorType.Custom, _massDifferenceAcceptorViewModel.CustomMdac);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not parse custom mass difference acceptor: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        TheTask.SearchParameters.MassDiffAcceptorType = _massDifferenceAcceptorViewModel.SelectedType.Type;
        TheTask.SearchParameters.CustomMdac = _massDifferenceAcceptorViewModel.CustomMdac;

        // displays warning if classic search is enabled with an open search mode
        if (TheTask.SearchParameters.SearchType == SearchType.Classic &&
            (TheTask.SearchParameters.MassDiffAcceptorType == MassDiffAcceptorType.ModOpen || TheTask.SearchParameters.MassDiffAcceptorType == MassDiffAcceptorType.Open))
        {
            MessageBoxResult result = MessageBox.Show("Modern Search mode is recommended when conducting open precursor mass searches to reduce search time.\n\n" +
                "Continue anyway?", "Modern search recommended", MessageBoxButton.OKCancel);

            if (result == MessageBoxResult.Cancel)
            {
                return;
            }
        }

        TheTask.CommonParameters = commonParamsToSave;

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        this.DialogResult = false;
        this.Close();
    }


    private void SaveAsDefaultButton_Click(object sender, RoutedEventArgs e)
    {
        SaveButton_Click(sender, e);
        var prefix = GuiGlobalParamsViewModel.Instance.IsRnaMode ? "Rna" : "";
        Toml.WriteFile(TheTask, Path.Combine(GlobalVariables.DataDir, "DefaultParameters", $"{prefix}FragmentTypeDetectionTaskDefault.toml"), MetaMorpheusTask.tomlConfig);
    }



    #region Modificaiton Handling

    private readonly ObservableCollection<ModTypeForTreeViewModel> FixedModTypeForTreeViewObservableCollection = new ObservableCollection<ModTypeForTreeViewModel>();
    private readonly ObservableCollection<ModTypeForTreeViewModel> VariableModTypeForTreeViewObservableCollection = new ObservableCollection<ModTypeForTreeViewModel>();

    private void TextChanged_Fixed(object sender, TextChangedEventArgs e)
    {
        SearchModifications.SetTimer();
        SearchModifications.FixedSearch = true;
    }

    private void TextChanged_Var(object sender, TextChangedEventArgs e)
    {
        SearchModifications.SetTimer();
        SearchModifications.VariableSearch = true;
    }

    private void TextChangeTimerHandler(object sender, EventArgs e)
    {
        if (SearchModifications.FixedSearch)
        {
            SearchModifications.FilterTree(SearchFixMod, FixedModsTreeView, FixedModTypeForTreeViewObservableCollection);
            SearchModifications.FixedSearch = false;
        }

        if (SearchModifications.VariableSearch)
        {
            SearchModifications.FilterTree(SearchVarMod, VariableModsTreeView, VariableModTypeForTreeViewObservableCollection);
            SearchModifications.VariableSearch = false;
        }
    }

    #endregion


    private void OnClosing(object sender, CancelEventArgs e)
    {
        SearchModifications.Timer.Tick -= new EventHandler(TextChangeTimerHandler);
        // remove event handler from timer
        // keeping it will trigger an exception because the closed window stops existing
    }
}

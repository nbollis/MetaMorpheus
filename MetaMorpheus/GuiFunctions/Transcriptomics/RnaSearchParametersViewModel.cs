using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using MassSpectrometry;
using Omics.Fragmentation;
using TaskLayer;
using Transcriptomics.Digestion;
using UsefulProteomicsDatabases;

namespace GuiFunctions.Transcriptomics
{
    public class RnaSearchParametersViewModel : BaseViewModel
    {
        public RnaSearchParametersViewModel()
        {
            searchParams = new RnaSearchParameters();
            commonParams = new CommonParameters(digestionParams: new RnaDigestionParams(fragmentationTerminus: FragmentationTerminus.Both));
            selectedRnase = RnaseDictionary.Dictionary.Values.First().Name;
        }

        private RnaSearchParameters searchParams { get; set; }
        private CommonParameters commonParams { get; set; }

        #region Digestion Params

        public int MinLength
        {
            get => commonParams.DigestionParams.MinLength;
            set { commonParams.DigestionParams.MinLength = value; OnPropertyChanged(nameof(MinLength)); }
        }

        public int MaxLength
        {
            get => commonParams.DigestionParams.MaxLength;
            set { commonParams.DigestionParams.MaxLength = value; OnPropertyChanged(nameof(MaxLength)); }
        }

        public int MaxMissedCleavages
        {
            get => commonParams.DigestionParams.MaxMissedCleavages;
            set { commonParams.DigestionParams.MaxMissedCleavages = value; OnPropertyChanged(nameof(MaxMissedCleavages)); }
        }

        public int MaxMods
        {
            get => commonParams.DigestionParams.MaxMods;
            set { commonParams.DigestionParams.MaxMods = value; OnPropertyChanged(nameof(MaxMods)); }
        }

        public int MaxModificationIsoforms
        {
            get => commonParams.DigestionParams.MaxModificationIsoforms;
            set { commonParams.DigestionParams.MaxModificationIsoforms = value; OnPropertyChanged(nameof(MaxModificationIsoforms)); }
        }

        public ObservableCollection<string> AllRnases { get; set; } =
            new ObservableCollection<string>(RnaseDictionary.Dictionary.Values.Select(p => p.Name));

        private string selectedRnase { get; set; }
        public string SelectedRnase { get => selectedRnase; set { selectedRnase = value; OnPropertyChanged(nameof(SelectedRnase)); } }



        #endregion


        #region Output Options

        public bool WriteHighQValueOsms
        {
            get => searchParams.WriteHighQValueOsms;
            set { searchParams.WriteHighQValueOsms = value; OnPropertyChanged(nameof(WriteHighQValueOsms)); }
        }

        public bool WriteDecoys
        {
            get => searchParams.WriteDecoys;
            set { searchParams.WriteDecoys = value; OnPropertyChanged(nameof(WriteDecoys)); }
        }

        public bool WriteContaminants
        {
            get => searchParams.WriteContaminants;
            set { searchParams.WriteContaminants = value; OnPropertyChanged(nameof(WriteContaminants)); }
        }

        public bool WriteAmbiguous
        {
            get => searchParams.WriteAmbiguous;
            set { searchParams.WriteAmbiguous = value; OnPropertyChanged(nameof(WriteAmbiguous)); }
        }

        public bool WriteIndividualFiles
        {
            get => searchParams.WriteIndividualFiles;
            set { searchParams.WriteIndividualFiles = value; OnPropertyChanged(nameof(WriteIndividualFiles)); }
        }

        public Dictionary<string, int> ModsToWriteSelection
        {
            get => searchParams.ModsToWriteSelection;
            set { searchParams.ModsToWriteSelection = value; OnPropertyChanged(nameof(ModsToWriteSelection)); }
        }


        #endregion

        #region SearchTask Build Stuff

        public MassDiffAcceptorType MassDiffAcceptorType
        {
            get => searchParams.MassDiffAcceptorType;
            set { searchParams.MassDiffAcceptorType = value; OnPropertyChanged(nameof(MassDiffAcceptorType)); }
        }

        public string CustomMdac
        {
            get => searchParams.CustomMdac;
            set { searchParams.CustomMdac = value; OnPropertyChanged(nameof(CustomMdac)); }
        }

        public DecoyType DecoyType
        {
            get => searchParams.DecoyType;
            set { searchParams.DecoyType = value; OnPropertyChanged(nameof(DecoyType)); }
        }

        public ObservableCollection<DissociationType> AllDissociationTypes { get; set; } =
            new ObservableCollection<DissociationType>(Enum.GetValues(typeof(DissociationType)).Cast<DissociationType>());

        private DissociationType dissociationType { get; set; }
        public DissociationType DissociationType
        {
            get => dissociationType;
            set { dissociationType = value; OnPropertyChanged(nameof(DissociationType)); }
        }

        private int maxThreads { get; set; }

        public int MaxThreads
        {
            get => maxThreads;
            set { maxThreads = value; OnPropertyChanged(nameof(MaxThreads)); }
        }

        private List<(string, string)> variableMods { get; set; }

        public List<(string, string)> VariableMods
        {
            get => variableMods;
            set { variableMods = value; OnPropertyChanged(nameof(VariableMods)); }
        }

        private List<(string, string)> fixedMods { get; set; }

        public List<(string, string)> FixedMods
        {
            get => fixedMods;
            set { fixedMods = value; OnPropertyChanged(nameof(FixedMods)); }
        }

        private bool doPrecursorDeconvolution { get; set; }

        public bool DoPrecursorDeconvolution
        {
            get => doPrecursorDeconvolution;
            set { doPrecursorDeconvolution = value; OnPropertyChanged(nameof(DoPrecursorDeconvolution)); }
        }

        private bool useProvidedPrecursorInfo { get; set; }

        public bool UseProvidedPrecursorInfo
        {
            get => useProvidedPrecursorInfo;
            set { useProvidedPrecursorInfo = value; OnPropertyChanged(nameof(UseProvidedPrecursorInfo)); }
        }

        private int minAssumedChargeState { get; set; }

        public int MinAssumedChargeState
        {
            get => minAssumedChargeState;
            set { minAssumedChargeState = value; OnPropertyChanged(nameof(MinAssumedChargeState)); }
        }

        private int maxAssumedChargeState { get; set; }

        public int MaxAssumedChargeState
        {
            get => maxAssumedChargeState;
            set { maxAssumedChargeState = value; OnPropertyChanged(nameof(MaxAssumedChargeState)); }
        }

        private int precursorMassTolerance { get; set; }

        public int PrecursorMassTolerance
        {
            get => precursorMassTolerance;
            set { precursorMassTolerance = value; OnPropertyChanged(nameof(PrecursorMassTolerance)); }
        }

        private int productMassTolerance { get; set; }

        public int ProductMassTolerance
        {
            get => productMassTolerance;
            set { productMassTolerance = value; OnPropertyChanged(nameof(ProductMassTolerance)); }
        }

        private double qValueCutoff { get; set; }

        public double QValueCutoff
        {
            get => qValueCutoff;
            set { qValueCutoff = value; OnPropertyChanged(nameof(QValueCutoff)); }
        }

        private double scoreCutoff { get; set; }

        public double ScoreCutoff
        {
            get => scoreCutoff;
            set { scoreCutoff = value; OnPropertyChanged(nameof(ScoreCutoff)); }
        }



        #endregion

        

    }
}

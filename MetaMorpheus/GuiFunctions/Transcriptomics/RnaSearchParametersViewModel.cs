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
            digestionParams = new RnaDigestionParams(fragmentationTerminus: FragmentationTerminus.Both);
            selectedRnase = RnaseDictionary.Dictionary.Values.First().Name;
            MaxAssumedChargeState = -12;

        }

        public RnaSearchParameters searchParams { get; set; }
        public RnaDigestionParams digestionParams { get; set; }

        #region Digestion Params

        public int MinLength
        {
            get => digestionParams.MinLength;
            set { digestionParams.MinLength = value; OnPropertyChanged(nameof(MinLength)); }
        }

        public int MaxLength
        {
            get => digestionParams.MaxLength;
            set { digestionParams.MaxLength = value; OnPropertyChanged(nameof(MaxLength)); }
        }

        public int MaxMissedCleavages
        {
            get => digestionParams.MaxMissedCleavages;
            set { digestionParams.MaxMissedCleavages = value; OnPropertyChanged(nameof(MaxMissedCleavages)); }
        }

        public int MaxMods
        {
            get => digestionParams.MaxMods;
            set { digestionParams.MaxMods = value; OnPropertyChanged(nameof(MaxMods)); }
        }

        public int MaxModificationIsoforms
        {
            get => digestionParams.MaxModificationIsoforms;
            set { digestionParams.MaxModificationIsoforms = value; OnPropertyChanged(nameof(MaxModificationIsoforms)); }
        }

        public ObservableCollection<string> AllRnases { get; set; } =
            new ObservableCollection<string>(RnaseDictionary.Dictionary.Values.Select(p => p.Name));

        private string selectedRnase { get; set; }
        public string SelectedRnase { get => selectedRnase; set { selectedRnase = value; OnPropertyChanged(nameof(SelectedRnase)); } }



        #endregion


        #region Output Options

        public bool WriteHighQValueOsms
        {
            get => searchParams.WriteHighQValueSpectralMatches;
            set { searchParams.WriteHighQValueSpectralMatches = value; OnPropertyChanged(nameof(WriteHighQValueOsms)); }
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


        private int maxAssumedChargeState { get; set; }
        public int MaxAssumedChargeState
        {
            get => maxAssumedChargeState;
            set { maxAssumedChargeState = value; OnPropertyChanged(nameof(MaxAssumedChargeState)); }
        }

        private int deconvolutionMassTolerance { get; set; }
        public int DeconvolutionMassTolerance
        {
            get => deconvolutionMassTolerance;
            set { deconvolutionMassTolerance = value; OnPropertyChanged(nameof(DeconvolutionMassTolerance)); }
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

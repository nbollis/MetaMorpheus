using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MassSpectrometry;

namespace GuiFunctions
{
    public class DeconvolutionViewModel : BaseViewModel
    {
        #region Private Properties

        private DeconvolutionTypes selectedDconvolutionType;
        private bool useProvidedPrecursors;
        private bool deconvolutePrecursors;
        private DeconvolutionViewModel deconvolutionViewModel;

        #endregion

        #region Public Properties

        public DeconvolutionTypes SelectedDeconvolutionType
        {
            get => selectedDconvolutionType;
            set
            {
                selectedDconvolutionType = value;
                OnPropertyChanged(nameof(SelectedDeconvolutionType));
            }
        }

        public bool UseProvidedPrecursors
        {
            get => useProvidedPrecursors;
            set
            {
                useProvidedPrecursors = value;
                OnPropertyChanged(nameof(UseProvidedPrecursors));
            }
        }

        public bool DeconvolutePrecursors
        {
            get => deconvolutePrecursors;
            set
            {
                deconvolutePrecursors = value;
                OnPropertyChanged(nameof(DeconvolutePrecursors));
            }
        }

        public int MaxAssumedChargeState
        {
            get => deconvolutionViewModel.MaxAssumedChargeState;
            set
            {
                deconvolutionViewModel.MaxAssumedChargeState = value;
                OnPropertyChanged(nameof(MaxAssumedChargeState));
            }
        }

        public ObservableCollection<DeconvolutionTypes> DeconvolutionTypes { get; set; }

        #endregion

        #region Constructor

        public DeconvolutionViewModel()
        {
            DeconvolutionTypes = new ObservableCollection<DeconvolutionTypes>(Enum.GetValues<DeconvolutionTypes>()
                /*.Where(p => p != MassSpectrometry.DeconvolutionTypes.AlexDeconvolution)*/);
            selectedDconvolutionType = MassSpectrometry.DeconvolutionTypes.ClassicDeconvolution;
        }

        #endregion

        #region Command Methods

        internal void DeconvolutionTypeSelected()
        {

        }

        #endregion

    }
}

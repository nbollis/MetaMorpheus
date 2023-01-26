using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MassSpectrometry;

namespace GuiFunctions
{
    public class ClassicDeconvolutionViewModel : DeconvolutionViewModel
    {

        #region Private Properties

        internal ClassicDeconvolutionParameters parameters;

        #endregion

        #region Public Properties

        public int MinAssumedChargeState
        {
            get => parameters.MinAssumedChargeState;
            set
            {
                parameters.MinAssumedChargeState = value;
                OnPropertyChanged(nameof(MinAssumedChargeState));
            }
        }

        public new int MaxAssumedChargeState
        {
            get => parameters.MaxAssumedChargeState;
            set
            {
                parameters.MaxAssumedChargeState = value;
                OnPropertyChanged(nameof(MaxAssumedChargeState));
            }
        }

        public double DeconvolutionTolerancePpm
        {
            get => parameters.DeconvolutionTolerancePpm;
            set
            {
                parameters.DeconvolutionTolerancePpm = value;
                OnPropertyChanged(nameof(DeconvolutionTolerancePpm));
            }
        }

        public double IntensityRatioLimit
        {
            get => parameters.IntensityRatioLimit;
            set
            {
                parameters.IntensityRatioLimit = value;
                OnPropertyChanged(nameof(IntensityRatioLimit));
            }
        }

        #endregion

        #region Constructor

        public ClassicDeconvolutionViewModel()
        {
            // TODO: Remove this
            parameters = new ClassicDeconvolutionParameters(1, 60, 20, 3);
        }

        #endregion
    }
}

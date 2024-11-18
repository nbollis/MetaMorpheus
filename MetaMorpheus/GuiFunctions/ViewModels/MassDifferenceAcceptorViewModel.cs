using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskLayer;

namespace GuiFunctions
{
    public class MassDifferenceAcceptorViewModel : BaseViewModel
    {
        public MassDifferenceAcceptorViewModel(MassDiffAcceptorType selectedType, string customText) : base()
        {
            MassDiffAcceptorTypes = new ObservableCollection<MassDifferenceAcceptorTypeModel>(
                new[]
                {
                    CreateModel(MassDiffAcceptorType.OneMM),
                    CreateModel(MassDiffAcceptorType.TwoMM),
                    CreateModel(MassDiffAcceptorType.ThreeMM),
                    CreateModel(MassDiffAcceptorType.PlusOrMinusThreeMM),
                    CreateModel(MassDiffAcceptorType.ModOpen),
                    CreateModel(MassDiffAcceptorType.Open),
                    CreateModel(MassDiffAcceptorType.Custom)
                });
            SelectedType = CreateModel(selectedType);
            CustomMdac = customText;
        }

        public ObservableCollection<MassDifferenceAcceptorTypeModel> MassDiffAcceptorTypes { get; }
        private MassDifferenceAcceptorTypeModel _selectedType;
        public MassDifferenceAcceptorTypeModel SelectedType
        {
            get => _selectedType;
            set
            {
                _selectedType = value;
                OnPropertyChanged(nameof(SelectedType));
            }
        }

        private string _customMdac;
        public string CustomMdac
        {
            get => _customMdac;
            set
            {
                _customMdac = value;
                OnPropertyChanged(nameof(CustomMdac));
            }
        }

        private MassDifferenceAcceptorTypeModel CreateModel(MassDiffAcceptorType type)
        {
            string label = type switch
            {
                MassDiffAcceptorType.Exact => "Exact",
                MassDiffAcceptorType.OneMM => "1 Missed Monoisotopic Peak",
                MassDiffAcceptorType.TwoMM => "1 or 2 Missed Monoisotopic Peaks",
                MassDiffAcceptorType.ThreeMM => "1, 2, or 3 Missed Monoisotopic Peaks",
                MassDiffAcceptorType.PlusOrMinusThreeMM => "+- 3 Missed Monoisotopic Peaks",
                MassDiffAcceptorType.ModOpen => "-187 and Up",
                MassDiffAcceptorType.Open => "Accept all",
                MassDiffAcceptorType.Custom => "Custom",
                _ => throw new NotImplementedException(),
            };

            string toolTip = type switch
            {
                MassDiffAcceptorType.Exact => "Basic search where the observed and theoretical precursor masses must be equal (~0 Da precursor mass-difference). This search type assumes that there are no monoisotopic errors.",
                MassDiffAcceptorType.OneMM => "Basic search where the observed and theoretical precursor masses are allowed to disagree by 1 Da to allow for a 1 Da monoisotopic mass error.",
                MassDiffAcceptorType.TwoMM => "Basic search where the observed and theoretical precursor masses are allowed to disagree by 1 or 2 Da to allow for a 1 or 2 Da monoisotopic mass error.",
                MassDiffAcceptorType.ThreeMM => "Basic search where the observed and theoretical precursor masses are allowed to disagree by 1, 2, or 3 Da to allow for a 1, 2, or 3 Da monoisotopic mass error.",
                MassDiffAcceptorType.PlusOrMinusThreeMM => "Basic search where the observed and theoretical precursor masses are allowed to disagree by +-1, +-2, or +-3 Da in to allow for monoisotopic mass errors.",
                MassDiffAcceptorType.ModOpen => "An \"open-mass\" search that allows mass-differences between observed and theoretical precursor masses of -187 Da to infinity (observed can be infinitely more massive than the theoretical).\r\nThe purpose of this search type is to detect mass-differences corresponding to PTMs, amino acid variants, sample handling artifacts, etc.\r\nPlease use \"Modern Search\" mode when using this search type.",
                MassDiffAcceptorType.Open => "An \"open-mass\" search that allows mass-differences between observed and theoretical precursor masses of -infinity to infinity. The purpose of this search type is to detect mass-differences corresponding to PTMs, amino acid variants, sample handling artifacts, etc. Please use \"Modern Search\" mode when using this search type.",
                MassDiffAcceptorType.Custom => "A custom mass difference acceptor may be specified in multiple ways: * To accept a custom (other than the interval corresponding to the precursor tolerance) interval around zero daltons, specify a custom name, followed by \"ppmAroundZero\" or \"daltonsAroundZero\", followed by the numeric value corresponding to the interval width. Examples: * CustomPpmInterval ppmAroundZero 5 * CustomDaltonInterval daltonsAroundZero 2.1 * To accept a variety of pre-specified mass differences, use a custom name, followed by \"dot\", followed by a custom bin width, followed by comma separated acceptable mass differences. Examples: * CustomMissedIsotopePeaks dot 5 ppm 0,1.0029,2.0052 * CustomOxidationAllowed dot 0.1 da 0,16 * To accept mass differences in pre-specified dalton intervals, use a custom name, followed by \"interval\", followed by comma separated mass intervals in brackets. Example: * CustomPositiveIntervalAcceptror interval [0,200]",
                _ => throw new NotImplementedException(),
            };

            return new MassDifferenceAcceptorTypeModel
            {
                Type = type,
                Label = label,
                ToolTip = toolTip
            };
        }
    }

    public class MassDifferenceAcceptorTypeModel
    {
        public MassDiffAcceptorType Type { get; set; }
        public string Label { get; set; }
        public string ToolTip { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class MassDifferenceAcceptorModel : MassDifferenceAcceptorViewModel
    {
        public static MassDifferenceAcceptorModel Instance => new MassDifferenceAcceptorModel();
        public MassDifferenceAcceptorModel() : base(MassDiffAcceptorType.TwoMM, "")
        {
        
        }
    }
}

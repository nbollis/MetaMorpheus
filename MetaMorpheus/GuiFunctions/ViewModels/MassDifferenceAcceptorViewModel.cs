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
        public MassDifferenceAcceptorViewModel() : base()
        {
            MassDiffAcceptorTypes =
                new ObservableCollection<MassDiffAcceptorType>(Enum.GetValues<MassDiffAcceptorType>());
        }

        public ObservableCollection<MassDiffAcceptorType> MassDiffAcceptorTypes { get; }
        private MassDiffAcceptorType _selectedType;
        public MassDiffAcceptorType SelectedType
        {
            get => _selectedType;
            set
            {
                _selectedType = value;
                OnPropertyChanged(nameof(SelectedType));
            }
        }

        private string _customText;
        public string CustomText
        {
            get => _customText;
            set
            {
                _customText = value;
                OnPropertyChanged(nameof(CustomText));
            }
        }



    }

    [ExcludeFromCodeCoverage]
    public class MassDifferenceAcceptorModel : MassDifferenceAcceptorViewModel
    {
        public static MassDifferenceAcceptorModel Instance => new MassDifferenceAcceptorModel();
        public MassDifferenceAcceptorModel() : base()
        {
            SelectedType = MassDiffAcceptorType.TwoMM;
        }
    }
}

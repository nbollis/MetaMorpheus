using System.Windows.Media;

namespace GuiFunctions
{
    public class LegendItemViewModel : BaseViewModel
    {
        #region Private Properties

        protected SolidColorBrush _colorBrush;

        #endregion

        #region Public Properties

        public string Name { get; protected set; }
        public SolidColorBrush ColorBrush
        {
            get { return _colorBrush; }
            set
            {
                _colorBrush = value;
                OnPropertyChanged(nameof(ColorBrush));
            }
        }

        #endregion

        #region Constructor

        public LegendItemViewModel()
        {

        }

        #endregion
    }
}

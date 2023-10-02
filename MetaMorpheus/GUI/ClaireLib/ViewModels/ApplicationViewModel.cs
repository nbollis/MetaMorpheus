using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using GuiFunctions;
using HelpfulToolsGUI;

namespace MetaMorpheusGUI
{
    public class ApplicationViewModel : BaseViewModel
    {
        /// <summary>
        /// Image used in the upper left of the window and the taskbar icon
        /// </summary>
        private BitmapImage icon;

        /// <summary>
        /// Image shown in the upper left of the window and the taskbar icon
        /// </summary>
        public BitmapImage Icon
        {
            get { return icon; }
            set
            {
                icon = value;
                OnPropertyChanged(nameof(Icon));
            }
        }

        public ScramblerVM ScramblerVM { get; set; }
        public FragmentFrequencyVM FragmentFrequencyVM { get; set; }
        public DatabaseConverterViewModel DatabaseConverterVM { get; set; }
        public ApplicationViewModel()
        {
            string filepath = Path.Join(ApplicationPath, @"ClaireLib\Resources\LampClaire.png");
            icon = new BitmapImage(new Uri(filepath));
            ScramblerVM = new ScramblerVM();
            FragmentFrequencyVM = new FragmentFrequencyVM();
            DatabaseConverterVM = new DatabaseConverterViewModel();
        }

        
    }
}

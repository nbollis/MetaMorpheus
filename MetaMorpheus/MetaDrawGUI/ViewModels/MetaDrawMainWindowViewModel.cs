using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EngineLayer;
using MetaDrawBackend.DependencyInjection;

namespace MetaDrawGUI.ViewModels
{
    public class MetaDrawMainWindowViewModel : BaseMetaDrawViewModel
    {
        #region Construction

        public MetaDrawMainWindowViewModel()
        {
            // Command Assignment


            // Object Instantiation
            Psms = Data.PsmData.Value.ToObservableCollection();
        }

        #endregion

        #region Commands

      
        // TODO: 

        #endregion

        #region Properties

        private ObservableCollection<PsmFromTsv> _psms = new ObservableCollection<PsmFromTsv>();

        public ObservableCollection<PsmFromTsv> Psms
        {
            get => _psms;
            set
            { 
                _psms = value;
                OnPropertyChanged(nameof(Psms));
            }
        }

        #endregion

        #region Visibility

        private Visibility _busyVisibility = Visibility.Hidden;
        public Visibility BusyVisibility
        {
            get
            {
                return _busyVisibility;
            }
            set
            {
                _busyVisibility = value;
                OnPropertyChanged(nameof(BusyVisibility));
            }
        }

        #endregion

        #region Private Methods

        private void UISetBusy()
        {
            BusyVisibility = Visibility.Visible;
        }

        private void UIClearBusy()
        {
            BusyVisibility = Visibility.Hidden;
        }

        private void InvokeMethodThreaded(Action actionToExecute)
        {
            Thread t = new Thread(delegate ()
            {
                UISetBusy();
                actionToExecute();
                UIClearBusy();
            });
            t.Start();
        }

        #endregion
    }
}

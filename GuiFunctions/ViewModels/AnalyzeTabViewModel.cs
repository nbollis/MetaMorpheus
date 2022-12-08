using System;
using System.Collections.Generic;
<<<<<<< HEAD
using System.Collections.ObjectModel;
using System.IO;
=======
>>>>>>> 58794e94f748d5c1f3beddab7b6947fa687c9590
using System.Linq;
using System.Text;
using System.Threading.Tasks;

<<<<<<< HEAD
namespace GuiFunctions
{
    public class AnalyzeTabViewModel : BaseViewModel
    {
        #region Private Properties



        #endregion

        #region Public Properties

        public ObservableCollection<MetaMorpheusRun> MMRuns { get; set; }

        #endregion

        #region Constructor

        public AnalyzeTabViewModel()
        {
            MMRuns = new();
        }

        #endregion

        #region Command Methods



        #endregion

        #region Helpers

        public void InputSearchFolder(string folderPath)
        {
            
                MMRuns.Add(new MetaMorpheusRun(folderPath));
            
        }

        #endregion
    }
}

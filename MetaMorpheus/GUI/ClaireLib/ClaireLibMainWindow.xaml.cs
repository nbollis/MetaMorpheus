using System.Windows;
using UsefulProteomicsDatabases;

namespace MetaMorpheusGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class ClaireLibMainWindow : Window
    {
        public ApplicationViewModel ApplicationViewModel { get; set; }
        public ClaireLibMainWindow()
        {
            InitializeComponent();
            Loaders.LoadElements();
            ApplicationViewModel = new ApplicationViewModel();
            DataContext = ApplicationViewModel;
        }

        private void MainWindow_OnDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);


            switch (MainWindowTabControl.SelectedIndex)
            {
                case 0:
                    foreach (var path in files)
                    {
                        string extension = System.IO.Path.GetExtension(path);
                    }

                    break;
                case 1:
                    foreach (var path in files)
                    {
                        ApplicationViewModel.FragmentFrequencyVM.FileDropped(path);
                    }

                    break;
            }
        }
    }
}

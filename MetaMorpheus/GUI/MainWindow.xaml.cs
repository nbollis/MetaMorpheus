using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing.Imaging;
using System.IO;
using Easy.Common.Extensions;
using UsefulProteomicsDatabases;
using Path = System.IO.Path;

namespace MetaMorpheusGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ApplicationViewModel ApplicationViewModel { get; set; }
        public MainWindow()
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
            }
        }
    }
}

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

namespace MetaMorpheusGUI
{
    /// <summary>
    /// Interaction logic for DigestionFragmentationControl.xaml
    /// </summary>
    public partial class DigestionFragmentationControl : UserControl
    {
        public DigestionFragmentationControl()
        {
            InitializeComponent();
        }

        private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            var vm = (e.Source as Button)!.DataContext as DigestionFragmentationViewModel;
            while (!vm!.MassResults.Any())
            {
                await Task.Delay(500);
            }

            FragmentGrid.Columns.Clear();
            FragmentGrid.AutoGenerateColumns = false;
            // Add Info1 and Info2 columns
            FragmentGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Ion",
                Binding = new Binding("Product.Annotation")
                {
                    StringFormat = "N2"
                },
                IsReadOnly = true
            });
            
            // Generate columns for dictionary keys
            foreach (var item in vm.MassResults.First().MzValues)
            {
                FragmentGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = item.Key.ToString(),
                    Binding = new Binding($"MzValues[{item.Key}]")
                    {
                        StringFormat = "N2"
                    },
                    CellStyle = new Style(typeof(DataGridCell))
                    {
                        Setters =
                        {
                            new Setter
                            {
                                Property = BackgroundProperty,
                                Value = new Binding($"MzValues[{item.Key}]")
                                {
                                    Converter = new KeyValuePairConverter(),
                                    ConverterParameter = (vm.TargetMz, vm.PpmTolerance)
                                }
                            }
                        }
                    },
                    IsReadOnly = true
                });
            }

            FragmentGrid.ItemsSource = vm.MassResults;
        }

        private void TextBoxBase_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            (this.DataContext as DigestionFragmentationViewModel)!.FilterText = (e.Source as TextBox)!.Text;
        }
    }
}

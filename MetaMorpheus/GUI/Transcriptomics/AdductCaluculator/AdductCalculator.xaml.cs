using Newtonsoft.Json.Converters;
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
    /// Interaction logic for AdductCalculator.xaml
    /// </summary>
    public partial class AdductCalculator : UserControl
    {
        public AdductCalculator()
        {
            InitializeComponent();
        }


        private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            var vm = (e.Source as Button)!.DataContext as AdductCalculatorViewModel;
            while (!vm!.MassResults.Any())
            {
                await Task.Delay(500);
            }

            AdductGrid.Columns.Clear();
            AdductGrid.AutoGenerateColumns = false;
            // Add Info1 and Info2 columns
            AdductGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Precursor",
                Binding = new Binding("TotalMass")
                {
                    StringFormat = "N2"
                },
                IsReadOnly = true
            });
            AdductGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Adducts",
                Binding = new Binding("AdductString"),
                IsReadOnly = true
            });

            // Generate columns for dictionary keys
            foreach (var item in vm.MassResults.First().MzValues)
            {
                AdductGrid.Columns.Add(new DataGridTextColumn
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

            AdductGrid.ItemsSource = vm.MassResults;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using MathNet.Numerics;
using MzLibUtil;

namespace MetaMorpheusGUI
{
    internal class KeyValuePairConverter : BaseValueConverter<KeyValuePairConverter>
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var vm = parameter as AdductCalculatorViewModel;
            
            if (value is double doubleValue)
            {
                // Adjust these values and logic according to your tolerance object
               
                if (vm.PpmTolerance.Within(vm.TargetMz, doubleValue))
                {
                    return Brushes.LightGreen; // Value is within tolerance, highlight as green
                }
            }

            // If not within tolerance, return a default background color (e.g., transparent)
            return Brushes.Transparent;
        }

        public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

using System;
using System.Globalization;

namespace MetaMorpheusGUI
{
    public class BooleanInverter : BaseValueConverter<BooleanInverter>
    {

        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }

        public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value;
        }
    }
}
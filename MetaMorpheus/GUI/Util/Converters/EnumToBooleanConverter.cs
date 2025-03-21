using GuiFunctions;
using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace MetaMorpheusGUI;

/// <summary>
/// Converts an enum value to a boolean by comparing it to a parameter.
/// </summary>
public class EnumToBooleanConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 2)
            return false;
        if (values[0] == values[1])
            return true;

        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        return new object[] { Binding.DoNothing, Binding.DoNothing };
    }
}
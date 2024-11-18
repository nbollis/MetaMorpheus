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
        throw new NotImplementedException();
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
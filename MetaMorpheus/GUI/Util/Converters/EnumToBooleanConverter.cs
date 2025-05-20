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
        if (values.Length != 2 || values[0] == null || values[1] == null)
            return false;

        var type0 = values[0].GetType();
        var type1 = values[1].GetType();

        if (type0.IsEnum && type1.IsEnum && type0 == type1)
        {
            return values[0].Equals(values[1]);
        }

        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        return new object[] { Binding.DoNothing, Binding.DoNothing };
    }
}
using System;
using System.Globalization;
using System.Windows.Data;

namespace MetaMorpheusGUI;

/// <summary>
/// Converts an enum value to a boolean by comparing it to a parameter.
/// </summary>
public class EnumToBooleanConverter : BaseValueConverter<EnumToBooleanConverter>
{
    /// <summary>
    /// Converts an enum value to a boolean by comparing it to a parameter.
    /// </summary>
    /// <param name="value">The enum value to convert.</param>
    /// <param name="targetType">The type of the binding target property.</param>
    /// <param name="parameter">The parameter to compare with the enum value.</param>
    /// <param name="culture">The culture to use in the converter.</param>
    /// <returns>True if the enum value equals the parameter; otherwise, false.</returns>
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value.Equals(parameter);
    }

    /// <summary>
    /// Converts a boolean value back to an enum value.
    /// </summary>
    /// <param name="value">The boolean value to convert back.</param>
    /// <param="targetType">The type of the binding target property.</param>
    /// <param name="parameter">The parameter to return if the value is true.</param>
    /// <param name="culture">The culture to use in the converter.</param>
    /// <returns>The parameter if the value is true; otherwise, Binding.DoNothing.</returns>
    public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value.Equals(true) ? parameter : Binding.DoNothing;
    }
}
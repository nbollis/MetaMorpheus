using System;
using System.Globalization;
using System.Windows;
using TaskLayer;

namespace MetaMorpheusGUI;
public class MassDifferenceAcceptorTypeToCustomTextBoxVisibilityConverter : BaseValueConverter<MassDifferenceAcceptorTypeToCustomTextBoxVisibilityConverter>
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MassDiffAcceptorType type)
        {
            if (type == MassDiffAcceptorType.Adduct || type == MassDiffAcceptorType.Custom)
            {
                return Visibility.Visible;
            }
        }
        return Visibility.Collapsed;
    }

    public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class MassDifferenceAcceptorTypeToAdductSelectionVisibilityConverter : BaseValueConverter<MassDifferenceAcceptorTypeToAdductSelectionVisibilityConverter>
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MassDiffAcceptorType type)
        {
            if (type == MassDiffAcceptorType.Adduct)
            {
                return Visibility.Visible;
            }
        }
        return Visibility.Collapsed;
    }

    public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


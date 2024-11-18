using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TaskLayer;

namespace MetaMorpheusGUI;

public class MassDifferenceAcceptorTypeToContentTextConverter : BaseValueConverter<MassDifferenceAcceptorTypeToContentTextConverter>
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MassDiffAcceptorType massDiff)
        {
            return massDiff switch
            {
                MassDiffAcceptorType.Exact => "Exact",
                MassDiffAcceptorType.OneMM => "1 Missed Monoisotopic Peak",
                MassDiffAcceptorType.TwoMM => "1 or 2 Missed Monoisotopic Peaks",
                MassDiffAcceptorType.ThreeMM => "1, 2, or 3 Missed Monoisotopic Peaks",
                MassDiffAcceptorType.PlusOrMinusThreeMM => "+- 3 Missed Monoisotopic Peaks",
                MassDiffAcceptorType.Open => "Accept all",
                MassDiffAcceptorType.Custom => "Custom",
                _ => throw new NotImplementedException(),
            };
        }

        // Handle unexpected cases gracefully
        return DependencyProperty.UnsetValue; // Avoid crashing the UI
    }

    public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


public class MassDifferenceAcceptorTypeToToolTipConverter : BaseValueConverter<MassDifferenceAcceptorTypeToToolTipConverter>
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MassDiffAcceptorType massDiff)
        {
            return massDiff switch
            {
                MassDiffAcceptorType.Exact => "Basic search where the observed and theoretical precursor masses must be equal (~0 Da precursor mass-difference). This search type assumes that there are no monoisotopic errors.",
                MassDiffAcceptorType.OneMM => "Basic search where the observed and theoretical precursor masses are allowed to disagree by 1 Da to allow for a 1 Da monoisotopic mass error.",
                MassDiffAcceptorType.TwoMM => "Basic search where the observed and theoretical precursor masses are allowed to disagree by 1 or 2 Da to allow for a 1 or 2 Da monoisotopic mass error.",
                MassDiffAcceptorType.ThreeMM => "Basic search where the observed and theoretical precursor masses are allowed to disagree by 1, 2, or 3 Da to allow for a 1, 2, or 3 Da monoisotopic mass error.",
                MassDiffAcceptorType.PlusOrMinusThreeMM => "Basic search where the observed and theoretical precursor masses are allowed to disagree by +-1, +-2, or +-3 Da in to allow for monoisotopic mass errors.",
                MassDiffAcceptorType.Open => "An \"open-mass\" search that allows mass-differences between observed and theoretical precursor masses of -infinity to infinity. The purpose of this search type is to detect mass-differences corresponding to PTMs, amino acid variants, sample handling artifacts, etc. Please use \"Modern Search\" mode when using this search type.",
                MassDiffAcceptorType.Custom => "A custom mass difference acceptor may be specified in multiple ways: * To accept a custom (other than the interval corresponding to the precursor tolerance) interval around zero daltons, specify a custom name, followed by \"ppmAroundZero\" or \"daltonsAroundZero\", followed by the numeric value corresponding to the interval width. Examples: * CustomPpmInterval ppmAroundZero 5 * CustomDaltonInterval daltonsAroundZero 2.1 * To accept a variety of pre-specified mass differences, use a custom name, followed by \"dot\", followed by a custom bin width, followed by comma separated acceptable mass differences. Examples: * CustomMissedIsotopePeaks dot 5 ppm 0,1.0029,2.0052 * CustomOxidationAllowed dot 0.1 da 0,16 * To accept mass differences in pre-specified dalton intervals, use a custom name, followed by \"interval\", followed by comma separated mass intervals in brackets. Example: * CustomPositiveIntervalAcceptror interval [0,200]",
                _ => throw new NotImplementedException(),
            };
        }

        // Handle unexpected cases gracefully
        return DependencyProperty.UnsetValue; // Avoid crashing the UI
    }

    public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class MassDifferenceAcceptorTypeToCustomTextBoxVisibilityConverter : BaseValueConverter<MassDifferenceAcceptorTypeToCustomTextBoxVisibilityConverter>
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value!.Equals(MassDiffAcceptorType.Custom) ? Visibility.Visible : Visibility.Collapsed;
    }

    public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


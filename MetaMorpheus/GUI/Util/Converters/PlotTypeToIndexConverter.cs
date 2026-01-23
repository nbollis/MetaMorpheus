using GuiFunctions.ViewModels.ParallelSearchTask.Plots;
using System;
using System.Globalization;

namespace MetaMorpheusGUI
{
    /// <summary>
    /// Converts PlotType enum to tab index (0-based) for TabControl binding
    /// </summary>
    public class PlotTypeToIndexConverter : BaseValueConverter<PlotTypeToIndexConverter>
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PlotType plotType)
            {
                return (int)plotType;
            }
            return 0;
        }

        public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                return (PlotType)index;
            }
            return PlotType.ManhattanPlot;
        }
    }
}

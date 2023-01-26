using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MassSpectrometry;

namespace MetaMorpheusGUI
{
    /// <summary>
    /// Convert between the selected deconvolution type and the display for its parameters
    /// </summary>
    public class DeconvolutionParametersValueConverter : BaseValueConverter<DeconvolutionParametersValueConverter>

    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((DeconvolutionTypes)value)
            {
                case DeconvolutionTypes.ClassicDeconvolution:
                    return new ClassicDeconvolutionParametersControl();
                case DeconvolutionTypes.AlexDeconvolution:
                default:
                    return new NotImplementedDeconvolutionControl();
            }
        }

        public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using MassSpectrometry;
using MzLibUtil;
using ThermoFisher.CommonCore.Data.Business;
using mzPlot;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Wpf;
using Proteomics.Fragmentation;
using Canvas = System.Windows.Controls.Canvas;
using FontWeights = OxyPlot.FontWeights;
using LinearAxis = OxyPlot.Axes.LinearAxis;
using LineSeries = OxyPlot.Series.LineSeries;
using TextAnnotation = OxyPlot.Annotations.TextAnnotation;

namespace GuiFunctions
{
    public class MirrorSpectrum : mzPlot.Plot
    {
        public MirrorSpectrum(PlotView plotView, MsDataScan scan1, MsDataScan scan2, DoubleRange range) : base(plotView)
        {
            Scan1 = scan1;
            Scan2 = scan2;
            Range = range;
            PlotView = plotView;
            DrawSpectrum();
        }

        public MsDataScan Scan1 { get; }
        public MsDataScan Scan2 { get; }
        public DoubleRange Range { get; }
        public PlotView PlotView { get; }

        protected void DrawSpectrum()
        {
            double max = Scan1.MassSpectrum.Extract(Range).Max(p => p.Intensity) * 1.2;
            double min = -Scan2.MassSpectrum.Extract(Range).Max(p => p.Intensity) * 1.2;

            // set up axes
            Model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "m/z",
                Minimum = Range.Minimum,
                Maximum = Range.Maximum,
                AbsoluteMinimum = Math.Max(0, Range.Minimum - 100),
                AbsoluteMaximum = Range.Maximum + 100,
                MajorStep = 0.5,
                MinorStep = 0.1,
                MajorTickSize = 2,
                TitleFontWeight = FontWeights.Bold,
                TitleFontSize = 14
            });

            Model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Intensity",
                Minimum = 0,
                Maximum = Scan1.MassSpectrum.YArray.Max(),
                AbsoluteMinimum = 0,
                AbsoluteMaximum = Scan1.MassSpectrum.YArray.Max() * 2,
                MajorStep = max / 5,
                MinorStep = max / 25,
                StringFormat = "0e-0",
                MajorTickSize = 2,
                TitleFontWeight = FontWeights.Bold,
                TitleFontSize = 14,
                AxisTitleDistance = 10,
                ExtraGridlines = new double[] { 0 },
                ExtraGridlineColor = OxyColors.Black,
                ExtraGridlineThickness = 1
            });

            // draw all peaks in the scan
            foreach (var peak in Scan1.MassSpectrum.Extract(Range))
            {
                DrawPeak(peak.Mz, peak.Intensity, MetaDrawSettings.StrokeThicknessUnannotated, MetaDrawSettings.ProductTypeToColor[ProductType.y], null);
            }

            foreach (var peak in Scan2.MassSpectrum.Extract(Range))
            {
                DrawPeak(peak.Mz, -peak.Intensity, MetaDrawSettings.StrokeThicknessUnannotated, MetaDrawSettings.ProductTypeToColor[ProductType.b], null);
            }

            // zoom to accomodate the mirror plot
            Model.Axes[1].AbsoluteMinimum = min * 2;
            Model.Axes[1].AbsoluteMaximum = max * 2;
            Model.Axes[1].Zoom(min, max);
            Model.Axes[1].LabelFormatter = DrawnSequence.YAxisLabelFormatter;
            Model.Title = "Isolation Window of Scan 810 from Jurkat Fraciton 7";
        }

        /// <summary>
        /// Draws a peak on the spectrum
        /// </summary>
        /// <param name="mz">x value of peak to draw</param>
        /// <param name="intensity">y max of peak to draw</param>
        /// <param name="strokeWidth"></param>
        /// <param name="color">Color to draw peak</param>
        /// <param name="annotation">text to display above the peak</param>
        protected void DrawPeak(double mz, double intensity, double strokeWidth, OxyColor color, TextAnnotation annotation)
        {
            // peak line
            var line = new LineSeries();
            line.Color = color;
            line.StrokeThickness = strokeWidth;
            line.Points.Add(new DataPoint(mz, 0));
            line.Points.Add(new DataPoint(mz, intensity));

            if (annotation != null)
            {
                Model.Annotations.Add(annotation);
            }

            Model.Series.Add(line);
        }


    }
}

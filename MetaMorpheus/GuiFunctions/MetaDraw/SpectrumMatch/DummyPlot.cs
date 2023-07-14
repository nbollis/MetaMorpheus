using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chemistry;
using ExCSS;
using MassSpectrometry;
using Proteomics.Fragmentation;
using ThermoFisher.CommonCore.Data.Business;
using MassSpectrometry;
using mzPlot;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using Proteomics.Fragmentation;
using Canvas = System.Windows.Controls.Canvas;
using FontWeights = OxyPlot.FontWeights;
using HorizontalAlignment = OxyPlot.HorizontalAlignment;
using VerticalAlignment = OxyPlot.VerticalAlignment;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace GuiFunctions
{
    public class DummyPlot : Plot
    {
        protected List<MatchedFragmentIon> MatchedFragmentIons;
        public MsDataScan Scan { get; protected set; }
        public DummyPlot(MsDataScan scan, List<MatchedFragmentIon> matchedIons, OxyPlot.Wpf.PlotView plotView) : base(plotView)
        {
            Model.Title = string.Empty;
            Model.Subtitle = string.Empty;
            Scan = scan;
            MatchedFragmentIons = matchedIons;

            DrawSpectrum();

            AnnotateMatchedIons();



            ZoomAxes();
            RefreshChart();
        }



        /// <summary>
        /// Annotates all matched ion peaks
        /// </summary>
        /// <param name="isBetaPeptide"></param>
        /// <param name="matchedFragmentIons"></param>
        /// <param name="useLiteralPassedValues"></param>
        protected void AnnotateMatchedIons()
        {
            foreach (MatchedFragmentIon matchedIon in MatchedFragmentIons)
            {
                OxyColor color;
                switch (matchedIon.NeutralTheoreticalProduct.ProductType)
                {
                    case ProductType.a:
                    case ProductType.aStar:
                    case ProductType.aDegree:
                    case ProductType.adot:
                    case ProductType.aBase:
                        color = OxyColors.Fuchsia;
                        break;
                    case ProductType.b:
                    case ProductType.bAmmoniaLoss:
                    case ProductType.bWaterLoss:
                    case ProductType.bdot:
                    case ProductType.bBase:
                        color = OxyColors.Blue;
                        break;
                    case ProductType.c:
                    case ProductType.cdot:
                    case ProductType.cBase:
                        color = OxyColors.Gold;
                        break;
                    case ProductType.d:
                    case ProductType.ddot:
                    case ProductType.dBase:
                    case ProductType.dH2O:
                        color = OxyColors.Purple;
                        break;
                    case ProductType.w:
                    case ProductType.wdot:
                    case ProductType.wBase:
                        color = OxyColors.Green;
                        break;
                    case ProductType.x:
                    case ProductType.xdot:
                    case ProductType.xBase:
                        color = OxyColors.BurlyWood;
                        break;
                    case ProductType.y:
                    case ProductType.yAmmoniaLoss:
                    case ProductType.yWaterLoss:
                    case ProductType.ydot:
                    case ProductType.yBase:
                        color = OxyColors.Red;
                        break;
                    case ProductType.z:
                    case ProductType.zPlusOne:
                    case ProductType.zDot:
                    case ProductType.zBase:
                        color = OxyColors.Orange;
                        break;
                    case ProductType.M:
                    case ProductType.D:
                    case ProductType.Ycore:
                    case ProductType.Y:
                        color = OxyColors.LimeGreen;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }


                AnnotatePeak(matchedIon, color);
            }
        }

        /// <summary>
        /// Annotates a single matched ion peak
        /// </summary>
        /// <param name="matchedIon">matched ion to annotate</param>
        /// <param name="isBetaPeptide">is a beta x-linked peptide</param>
        /// <param name="useLiteralPassedValues"></param>
        protected void AnnotatePeak(MatchedFragmentIon matchedIon, OxyColor ionColorNullable)
        {
            OxyColor ionColor = ionColorNullable;
        

            int i = Scan.MassSpectrum.GetClosestPeakIndex(matchedIon.NeutralTheoreticalProduct.NeutralMass.ToMz(matchedIon.Charge));
            double mz = Scan.MassSpectrum.XArray[i];
            double intensity = Scan.MassSpectrum.YArray[i];

            // peak annotation
            string prefix = "";
            var peakAnnotation = new TextAnnotation();
            if (MetaDrawSettings.DisplayIonAnnotations)
            {
                string peakAnnotationText = prefix + matchedIon.NeutralTheoreticalProduct.Annotation;

                if (matchedIon.NeutralTheoreticalProduct.NeutralLoss != 0 && !peakAnnotationText.Contains("-" + matchedIon.NeutralTheoreticalProduct.NeutralLoss.ToString("F2")))
                {
                    peakAnnotationText += "-" + matchedIon.NeutralTheoreticalProduct.NeutralLoss.ToString("F2");
                }

                if (MetaDrawSettings.AnnotateCharges)
                {
                    peakAnnotationText += "+" + matchedIon.Charge;
                }

                if (MetaDrawSettings.AnnotateMzValues)
                {
                    peakAnnotationText += " (" + matchedIon.Mz.ToString("F3") + ")";
                }


                peakAnnotation.Font = "Arial";
                peakAnnotation.FontSize = MetaDrawSettings.AnnotatedFontSize;
                peakAnnotation.FontWeight = MetaDrawSettings.AnnotationBold ? FontWeights.Bold : 2.0;
                peakAnnotation.TextColor = ionColor;
                peakAnnotation.StrokeThickness = 0;
                peakAnnotation.Text = peakAnnotationText;
                peakAnnotation.TextPosition = new DataPoint(mz, intensity);
                peakAnnotation.TextVerticalAlignment = intensity < 0 ? VerticalAlignment.Top : VerticalAlignment.Bottom;
                peakAnnotation.TextHorizontalAlignment = HorizontalAlignment.Center;
            }
            else
            {
                peakAnnotation.Text = string.Empty;
            }
            if (matchedIon.NeutralTheoreticalProduct.SecondaryProductType != null && !MetaDrawSettings.DisplayInternalIonAnnotations) //if internal fragment
            {
                peakAnnotation.Text = string.Empty;
            }

            DrawPeak(mz, intensity, MetaDrawSettings.StrokeThicknessAnnotated, ionColor, peakAnnotation);
        }

        /// <summary>
        /// Adds the spectrum from the MSDataScan to the Model
        /// </summary>
        protected void DrawSpectrum()
        {
            // set up axes
            Model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "m/z",
                Minimum = Scan.ScanWindowRange.Minimum,
                Maximum = Scan.ScanWindowRange.Maximum,
                AbsoluteMinimum = Math.Max(0, Scan.ScanWindowRange.Minimum - 100),
                AbsoluteMaximum = Scan.ScanWindowRange.Maximum + 100,
                MajorStep = 200,
                MinorStep = 200,
                MajorTickSize = 2,
                TitleFontWeight = FontWeights.Bold,
                TitleFontSize = 14
            });

            Model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Intensity",
                Minimum = 0,
                Maximum = Scan.MassSpectrum.YArray.Max(),
                AbsoluteMinimum = 0,
                AbsoluteMaximum = Scan.MassSpectrum.YArray.Max() * 2,
                MajorStep = Scan.MassSpectrum.YArray.Max() / 10,
                MinorStep = Scan.MassSpectrum.YArray.Max() / 10,
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
            for (int i = 0; i < Scan.MassSpectrum.XArray.Length; i++)
            {
                double mz = Scan.MassSpectrum.XArray[i];
                double intensity = Scan.MassSpectrum.YArray[i];

                DrawPeak(mz, intensity, MetaDrawSettings.StrokeThicknessUnannotated, MetaDrawSettings.UnannotatedPeakColor, null);
            }
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


        /// <summary>
        /// Zooms the axis of the graph to the matched ions
        /// </summary>
        /// <param name="yZoom"></param>
        /// <param name="matchedFramgentIons">ions to zoom to. if null, it will used the stored protected matchedFragmentIons</param>
        protected void ZoomAxes(IEnumerable<MatchedFragmentIon> matchedFramgentIons = null, double yZoom = 1.2)
        {
            matchedFramgentIons ??= MatchedFragmentIons;
            double highestAnnotatedIntensity = 0;
            double highestAnnotatedMz = double.MinValue;
            double lowestAnnotatedMz = double.MaxValue;

            foreach (var ion in MatchedFragmentIons)
            {
                double mz = ion.NeutralTheoreticalProduct.NeutralMass.ToMz(ion.Charge);
                int i = Scan.MassSpectrum.GetClosestPeakIndex(mz);
                double intensity = Scan.MassSpectrum.YArray[i];

                if (intensity > highestAnnotatedIntensity)
                {
                    highestAnnotatedIntensity = intensity;
                }

                if (highestAnnotatedMz < mz)
                {
                    highestAnnotatedMz = mz;
                }

                if (mz < lowestAnnotatedMz)
                {
                    lowestAnnotatedMz = mz;
                }
            }

            if (highestAnnotatedIntensity > 0)
            {
                Model.Axes[1].Zoom(0, highestAnnotatedIntensity * yZoom);
            }

            if (highestAnnotatedMz > double.MinValue && lowestAnnotatedMz < double.MaxValue)
            {
                Model.Axes[0].Zoom(lowestAnnotatedMz - 100, highestAnnotatedMz + 100);
            }
        }
    }
}

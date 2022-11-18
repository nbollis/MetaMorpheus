using EngineLayer;
using MassSpectrometry;
using OxyPlot.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OxyPlot;
using Proteomics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using TextAnnotation = OxyPlot.Annotations.TextAnnotation;

namespace GuiFunctions
{
    public class OverlaidSpectrumMatchPlot : SpectrumMatchPlot
    {
        private static Queue<OxyColor> overflowColors;
        public static OxyColor MultipleProteinSharedColor;
        public static Dictionary<int, List<OxyColor>> ColorByProteinDictionary;
        public static Queue<OxyColor> OverflowColors
        {
            get => new Queue<OxyColor>(overflowColors.ToList());
        }

        public List<PsmFromTsv> SpectrumMatches { get; private set; }
        public OverlaidSpectrumMatchPlot(PlotView plotView, List<PsmFromTsv> psms, MsDataScan scan) : base(plotView, null, scan)
        {
            SpectrumMatches = psms;
            AnnotateChargeStates();
            RefreshChart();
        }

        protected void AnnotateChargeStates()
        {


            foreach (var psm in SpectrumMatches.DistinctBy(p => p.BaseSeq))
            {
                OxyColor color = ColorByProteinDictionary[SpectrumMatches.IndexOf(psm)][0];
                var protein = new PeptideWithSetModifications(psm.FullSequence.Split('|')[0],
                    GlobalVariables.AllModsKnownDictionary);

                double[] mzs = new double[20];
                int charge = psm.PrecursorCharge;
                int spread = 10;


                for (int i = charge - spread; i < charge + spread; i++)
                {
                    if (i <= 0)
                        continue;

                    mzs[i - charge + spread] = protein.MonoisotopicMass / i;
                }



                foreach (var mz in mzs)
                {
                    int j = Scan.MassSpectrum.GetClosestPeakIndex(mz);
                    double masstoCharge = Scan.MassSpectrum.XArray[j];
                    double intensity = Scan.MassSpectrum.YofPeakWithHighestY.Value * 0.75;


                    string peakAnnotationText = $"+{Math.Round(protein.MonoisotopicMass / masstoCharge, 0)}";
                    var peakAnnotation = new TextAnnotation();
                    peakAnnotation.Font = "Arial";
                    peakAnnotation.FontSize = MetaDrawSettings.AnnotatedFontSize;
                    peakAnnotation.FontWeight = MetaDrawSettings.AnnotationBold ? FontWeights.Bold : 2.0;
                    peakAnnotation.TextColor = color;
                    peakAnnotation.StrokeThickness = 0;
                    peakAnnotation.Text = peakAnnotationText;
                    peakAnnotation.TextPosition = new DataPoint(masstoCharge, intensity);
                    peakAnnotation.TextVerticalAlignment = intensity < 0 ? VerticalAlignment.Top : VerticalAlignment.Bottom;
                    peakAnnotation.TextHorizontalAlignment = HorizontalAlignment.Center;

                    DrawPeak(masstoCharge, intensity, MetaDrawSettings.StrokeThicknessAnnotated, color, peakAnnotation);

                }

                
            }
        }


        /// <summary>
        /// Initializes the colors to be used by the overlaid Plotter
        /// </summary>
        static OverlaidSpectrumMatchPlot()
        {
            MultipleProteinSharedColor = OxyColors.Black;
            ColorByProteinDictionary = new();
            ColorByProteinDictionary.Add(0, new List<OxyColor>()
            {
                OxyColors.Blue, OxyColors.SkyBlue, OxyColors.CornflowerBlue,
                OxyColors.DarkBlue, OxyColors.CadetBlue, OxyColors.SteelBlue, OxyColors.DodgerBlue
            });
            ColorByProteinDictionary.Add(1, new List<OxyColor>()
            {
                OxyColors.Red, OxyColors.LightCoral, OxyColors.PaleVioletRed,
                OxyColors.IndianRed, OxyColors.Firebrick, OxyColors.Maroon, OxyColors.Tomato
            });
            ColorByProteinDictionary.Add(2, new List<OxyColor>()
            {
                OxyColors.Green, OxyColors.MediumSpringGreen, OxyColors.LightGreen,
                OxyColors.Linen, OxyColors.SpringGreen, OxyColors.Chartreuse, OxyColors.DarkSeaGreen
            });
            ColorByProteinDictionary.Add(3, new List<OxyColor>()
            {
                OxyColors.Purple, OxyColors.MediumPurple, OxyColors.Violet,
                OxyColors.Plum, OxyColors.Orchid, OxyColors.BlueViolet, OxyColors.Magenta
            });
            ColorByProteinDictionary.Add(4, new List<OxyColor>()
            {
                OxyColors.Brown, OxyColors.SaddleBrown, OxyColors.Sienna, OxyColors.Chocolate,
                OxyColors.SandyBrown, OxyColors.Chocolate, OxyColors.Peru, OxyColors.Tan
            });
            ColorByProteinDictionary.Add(5, new List<OxyColor>()
            {
                OxyColors.Gold, OxyColors.DarkGoldenrod, OxyColors.Wheat, OxyColors.Goldenrod,
                OxyColors.DarkKhaki, OxyColors.Khaki, OxyColors.Moccasin
            });

            IEnumerable<OxyColor> overflow = new List<OxyColor>()
            {
                OxyColors.Cornsilk, OxyColors.BlanchedAlmond, OxyColors.Aqua, OxyColors.Aquamarine,
                OxyColors.HotPink, OxyColors.PaleGreen, OxyColors.Gray, OxyColors.SeaGreen,
                OxyColors.LemonChiffon, OxyColors.RosyBrown, OxyColors.MediumSpringGreen
            };
            overflowColors = new Queue<OxyColor>(overflow);
        }
    }
}

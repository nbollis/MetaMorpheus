using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using Chemistry;
using MassSpectrometry;
using mzPlot;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using FontWeights = OxyPlot.FontWeights;
using HorizontalAlignment = OxyPlot.HorizontalAlignment;
using VerticalAlignment = OxyPlot.VerticalAlignment;
using EngineLayer;
using Omics.Fragmentation;
using Readers;
using Transcriptomics;

namespace GuiFunctions
{
        public class DummyPlot : Plot
        {
            protected List<MatchedFragmentIon> MatchedFragmentIons;
            public MsDataScan Scan { get; protected set; }
            public DummyPlot(MsDataScan scan, List<MatchedFragmentIon> matchedIons, OxyPlot.Wpf.PlotView plotView, OsmFromTsv librarySpec = null) : base(plotView)
            {
                Model.Title = string.Empty;
                Model.Subtitle = string.Empty;
                Scan = scan;
                MatchedFragmentIons = matchedIons;

                DrawSpectrum();
                AnnotateMatchedIons(MatchedFragmentIons);

                List<MatchedFragmentIon> allIons = new();
                allIons.AddRange(matchedIons);
                if (librarySpec != null)
                {
                    AnnotateLibraryIons(librarySpec.MatchedIons, out List<MatchedFragmentIon> mirroredIons);
                    allIons.AddRange(mirroredIons);
                }

                RefreshChart();
            }

            protected void AnnotateLibraryIons(List<MatchedFragmentIon> libraryIons, out List<MatchedFragmentIon> mirroredIons)
            {
                // figure out the sum of the intensities of the matched fragment ions
                double sumOfMatchedIonIntensities = 0;
                double sumOfLibraryIntensities = 0;
                foreach (var libraryIon in libraryIons)
                {
                    var matchedIon = MatchedFragmentIons.FirstOrDefault(p =>
                        p.NeutralTheoreticalProduct.ProductType == libraryIon.NeutralTheoreticalProduct.ProductType
                        && p.NeutralTheoreticalProduct.FragmentNumber == libraryIon.NeutralTheoreticalProduct.FragmentNumber);

                    if (matchedIon == null)
                    {
                        continue;
                    }

                    int i = Scan.MassSpectrum.GetClosestPeakIndex(libraryIon.Mz);
                    double intensity = Scan.MassSpectrum.YArray[i];
                    sumOfMatchedIonIntensities += intensity;
                    sumOfLibraryIntensities += libraryIon.Intensity;
                }

                double multiplier = -1 * sumOfMatchedIonIntensities / sumOfLibraryIntensities;

                List<MatchedFragmentIon> mirroredLibraryIons = new List<MatchedFragmentIon>();

                foreach (MatchedFragmentIon libraryIon in libraryIons)
                {
                    var neutralProduct = new Product(libraryIon.NeutralTheoreticalProduct.ProductType, libraryIon.NeutralTheoreticalProduct.Terminus,
                        libraryIon.NeutralTheoreticalProduct.NeutralMass, libraryIon.NeutralTheoreticalProduct.FragmentNumber,
                        libraryIon.NeutralTheoreticalProduct.AminoAcidPosition, libraryIon.NeutralTheoreticalProduct.NeutralLoss);

                    mirroredLibraryIons.Add(new MatchedFragmentIon(neutralProduct, libraryIon.Mz, multiplier * libraryIon.Intensity, libraryIon.Charge));
                }

                AnnotateMatchedIons(mirroredLibraryIons, true);
                mirroredIons = mirroredLibraryIons;

                // zoom to accomodate the mirror plot
                double min = mirroredLibraryIons.Min(p => p.Intensity) * 1.2;
                Model.Axes[1].AbsoluteMinimum = min * 2;
                Model.Axes[1].AbsoluteMaximum = -min * 2;
                Model.Axes[1].Zoom(min, -min);
                Model.Axes[1].LabelFormatter = DrawnSequence.YAxisLabelFormatter;
            }

            /// <summary>
            /// Annotates all matched ion peaks
            /// </summary>
            /// <param name="isBetaPeptide"></param>
            /// <param name="matchedFragmentIons"></param>
            /// <param name="useLiteralPassedValues"></param>
            protected void AnnotateMatchedIons(List<MatchedFragmentIon> matchedIons, bool useLiteral = false)
            {
                foreach (MatchedFragmentIon matchedIon in matchedIons)
                {
                    OxyColor color = MetaDrawSettings.ProductTypeToColor[matchedIon.NeutralTheoreticalProduct.ProductType];
                    AnnotatePeak(matchedIon, color, useLiteral);
                }
            }

            /// <summary>
            /// Annotates a single matched ion peak
            /// </summary>
            /// <param name="matchedIon">matched ion to annotate</param>
            /// <param name="isBetaPeptide">is a beta x-linked peptide</param>
            /// <param name="useLiteralPassedValues"></param>
            protected void AnnotatePeak(MatchedFragmentIon matchedIon, OxyColor ionColorNullable, bool useLiteral)
            {
                OxyColor ionColor = ionColorNullable;


                int i = Scan.MassSpectrum.GetClosestPeakIndex(matchedIon.NeutralTheoreticalProduct.NeutralMass.ToMz(matchedIon.Charge));
                double mz = Scan.MassSpectrum.XArray[i];
                double intensity = Scan.MassSpectrum.YArray[i];

                if (useLiteral)
                {
                    mz = matchedIon.Mz;
                    intensity = matchedIon.Intensity;
                }

                // peak annotation
                string prefix = "";
                var peakAnnotation = new TextAnnotation();
                if (MetaDrawSettings.DisplayIonAnnotations)
                {
                    string peakAnnotationText = prefix;

                    if (MetaDrawSettings.SubAndSuperScriptIons)
                        foreach (var character in matchedIon.NeutralTheoreticalProduct.Annotation)
                        {
                            if (char.IsDigit(character))
                                peakAnnotationText += MetaDrawSettings.SubScriptNumbers[character - '0'];
                            else switch (character)
                            {
                                case '-':
                                    peakAnnotationText += "\u208B"; // sub scripted Hyphen
                                    break;
                                case '[':
                                case ']':
                                    continue;
                                default:
                                    peakAnnotationText += character;
                                    break;
                            }
                        }
                    else
                        peakAnnotationText += matchedIon.NeutralTheoreticalProduct.Annotation;

                    peakAnnotationText = peakAnnotationText.Replace("WaterLoss", "-H\u2082O").Replace("BaseLoss", "-B");

                    if (matchedIon.NeutralTheoreticalProduct.NeutralLoss != 0 && !peakAnnotationText.Contains("-" + matchedIon.NeutralTheoreticalProduct.NeutralLoss.ToString("F2")))
                    {
                        peakAnnotationText += "-" + matchedIon.NeutralTheoreticalProduct.NeutralLoss.ToString("F2");
                    }

                    if (MetaDrawSettings.AnnotateCharges)
                    {
                        char chargeAnnotation = matchedIon.Charge > 0 ? '+' : '-';
                        if (MetaDrawSettings.SubAndSuperScriptIons)
                        {
                            var superScript = new string(Math.Abs(matchedIon.Charge).ToString()
                                .Select(digit => MetaDrawSettings.SuperScriptNumbers[digit - '0'])
                                .ToArray());

                            peakAnnotationText += superScript;
                            if (chargeAnnotation == '+')
                                peakAnnotationText += (char)(chargeAnnotation + 8271);
                            else
                                peakAnnotationText += (char)(chargeAnnotation + 8270);
                        }
                        else
                            peakAnnotationText += chargeAnnotation.ToString() + matchedIon.Charge;
                    }

                    if (MetaDrawSettings.AnnotateMzValues)
                    {
                        var acceptableIons = new List<(ProductType, int, int)>
                        {
                            (ProductType.y, -3, 9),
                            (ProductType.y, -2, 6),
                            (ProductType.aBaseLoss, -2, 12),
                            (ProductType.aBaseLoss, -2, 13),
                        };

                        if (acceptableIons.Any(ion =>
                                matchedIon.NeutralTheoreticalProduct.ProductType == ion.Item1 &&
                                matchedIon.Charge == ion.Item2 &&
                                matchedIon.NeutralTheoreticalProduct.FragmentNumber == ion.Item3)) 
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

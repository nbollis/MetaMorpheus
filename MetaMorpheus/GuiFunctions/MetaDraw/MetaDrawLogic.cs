﻿global using PsmFromTsv = Readers.PsmFromTsv; // Temporary until a follow-up PR changes these to SpectrumMatchFromTsv
using EngineLayer;
using iText.IO.Image;
using iText.Kernel.Pdf;
using MassSpectrometry;
using OxyPlot.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Easy.Common.Extensions;
using Readers;
using System.Threading;
using Omics.Fragmentation;
using Omics.SpectrumMatch;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Test")]
namespace GuiFunctions
{
    public class MetaDrawLogic
    {
        public ObservableCollection<string> SpectralMatchResultFilePaths { get; private set; }
        public ObservableCollection<string> SpectraFilePaths { get; private set; }
        public ObservableCollection<string> SpectralLibraryPaths { get; private set; }
        public ObservableCollection<SpectrumMatchFromTsv> FilteredListOfPsms { get; private set; } // filtered list of PSMs after q-value filter, etc.
        public ObservableCollection<SpectrumMatchFromTsv> ChimericSpectralMatches { get; private set; }
        public Dictionary<string, ObservableCollection<SpectrumMatchFromTsv>> SpectralMatchesGroupedByFile { get; private set; }
        public DrawnSequence StationarySequence { get; set; }
        public DrawnSequence ScrollableSequence { get; set; }
        public DrawnSequence SequenceAnnotation { get; set; }
        public ChimeraSpectrumMatchPlot ChimeraSpectrumMatchPlot { get; set; }
        public SpectrumMatchPlot SpectrumAnnotation { get; set; }
        public object ThreadLocker;
        public ICollectionView PeptideSpectralMatchesView;

        private List<SpectrumMatchFromTsv> AllSpectralMatches; // all loaded PSMs
        internal Dictionary<string, MsDataFile> MsDataFiles; // key is file name without extension
        private List<SpectrumMatchPlot> CurrentlyDisplayedPlots;
        private Regex illegalInFileName = new Regex(@"[\\/:*?""<>|]");
        private SpectralLibrary SpectralLibrary;

        public MetaDrawLogic()
        {
            SpectralMatchResultFilePaths = new ObservableCollection<string>();
            SpectraFilePaths = new ObservableCollection<string>();
            SpectralLibraryPaths = new ObservableCollection<string>();
            FilteredListOfPsms = new ObservableCollection<SpectrumMatchFromTsv>();
            SpectralMatchesGroupedByFile = new Dictionary<string, ObservableCollection<SpectrumMatchFromTsv>>();
            AllSpectralMatches = new List<SpectrumMatchFromTsv>();
            MsDataFiles = new Dictionary<string, MsDataFile>();
            PeptideSpectralMatchesView = CollectionViewSource.GetDefaultView(FilteredListOfPsms);
            ThreadLocker = new object();
            CurrentlyDisplayedPlots = new List<SpectrumMatchPlot>();
            ChimericSpectralMatches = new();
        }

        public List<string> LoadFiles(bool loadSpectra, bool loadPsms)
        {
            List<string> errors = new List<string>();

            lock (ThreadLocker)
            {
                FilteredListOfPsms.Clear();
                SpectralMatchesGroupedByFile.Clear();
                AllSpectralMatches.Clear();
                MsDataFiles.Clear();
            }

            // load MS data files
            if (loadSpectra)
            {
                LoadSpectraFiles(out var errors1);
                errors.AddRange(errors1);
            }

            // load PSMs
            if (loadPsms)
            {
                LoadPsms(out var errors2, loadSpectra);
                errors.AddRange(errors2);
            }

            // load spectral libraries
            LoadSpectralLibraries(out var errors3);
            errors.AddRange(errors3);

            return errors;
        }

        public void DisplayChimeraSpectra(PlotView plotView, List<SpectrumMatchFromTsv> sms, out List<string> errors)
        {
            CleanUpCurrentlyDisplayedPlots();
            errors = null;

            // get the scan
            if (!MsDataFiles.TryGetValue(sms.First().FileNameWithoutExtension, out MsDataFile spectraFile))
            {
                errors = new List<string>();
                errors.Add("The spectra file could not be found for this PSM: " + sms.First().FileNameWithoutExtension);
                return;
            }
            MsDataScan scan = spectraFile.GetOneBasedScanFromDynamicConnection(sms.First().Ms2ScanNumber);
            
            ChimeraSpectrumMatchPlot = new ChimeraSpectrumMatchPlot(plotView, scan, sms);
            ChimeraSpectrumMatchPlot.RefreshChart();
            CurrentlyDisplayedPlots.Add(ChimeraSpectrumMatchPlot);
        }

        public void DisplaySpectrumMatch(PlotView plotView, SpectrumMatchFromTsv sm, ParentChildScanPlotsView parentChildScanPlotsView, out List<string> errors)
        {
            errors = null;

            // clear old parent/child scans
            parentChildScanPlotsView.Plots.Clear();
            CleanUpCurrentlyDisplayedPlots();

            // get the scan
            if (!MsDataFiles.TryGetValue(sm.FileNameWithoutExtension, out MsDataFile spectraFile))
            {
                errors = new List<string>();
                errors.Add("The spectra file could not be found for this PSM: " + sm.FileNameWithoutExtension);
                return;
            }

            spectraFile.InitiateDynamicConnection();
            MsDataScan scan = spectraFile.GetOneBasedScanFromDynamicConnection(sm.Ms2ScanNumber);

            LibrarySpectrum librarySpectrum = null;
            if (SpectralLibrary != null)
            {
                SpectralLibrary.TryGetSpectrum(sm.FullSequence, sm.PrecursorCharge, out var librarySpectrum1);
                librarySpectrum = librarySpectrum1;
            }

            //if not crosslinked
            if (!sm.IsCrossLinkedPeptide())
            {
                // get the library spectrum if relevant
                if (SpectralLibrary != null)
                {
                    SpectralLibrary.TryGetSpectrum(sm.FullSequence, sm.PrecursorCharge, out var librarySpectrum1);
                    librarySpectrum = librarySpectrum1;
                }

                SpectrumAnnotation = new PeptideSpectrumMatchPlot(plotView, sm, scan, sm.MatchedIons, librarySpectrum: librarySpectrum, stationarySequence: true);
            }
            else //crosslinked
            {
                // get the library spectrum if relevant
                if (SpectralLibrary != null)
                {
                    SpectralLibrary.TryGetSpectrum((sm as PsmFromTsv)!.UniqueSequence, sm.PrecursorCharge, out var librarySpectrum1);
                    librarySpectrum = librarySpectrum1;
                }

                SpectrumAnnotation = new CrosslinkSpectrumMatchPlot(plotView, sm as PsmFromTsv, scan, StationarySequence.SequenceDrawingCanvas, librarySpectrum: librarySpectrum);
            }

            CurrentlyDisplayedPlots.Add(SpectrumAnnotation);

            // plot parent/child scans
            if (sm.ChildScanMatchedIons != null)
            {
                // draw parent scan
                string parentAnnotation = "Scan: " + scan.OneBasedScanNumber
                        + " Dissociation Type: " + scan.DissociationType
                        + " MsOrder: " + scan.MsnOrder
                        + " Selected Mz: " + scan.SelectedIonMZ.Value.ToString("0.##")
                        + " Retention Time: " + scan.RetentionTime.ToString("0.##");

                var parentPlotView = new PlotView(); // placeholder
                var parentCanvas = new Canvas();
                DrawnSequence parentSequence = new(parentCanvas, sm, false);
                parentSequence.AnnotateBaseSequence(sm.BaseSeq, sm.FullSequence, 10, sm.MatchedIons, sm);
                var item = new ParentChildScanPlotTemplate()
                {
                    Plot = new PeptideSpectrumMatchPlot(parentPlotView, sm, scan, sm.MatchedIons),
                    SpectrumLabel = parentAnnotation,
                    TheCanvas = parentSequence.SequenceDrawingCanvas
                };

                parentChildScanPlotsView.Plots.Add(item);

                // remove model from placeholder (the model can only be referenced by 1 plotview at a time)
                parentPlotView.Model = null;

                // draw child scans
                HashSet<int> scansDrawn = new HashSet<int>();
                var allChildScanMatchedIons = sm.ChildScanMatchedIons;

                if (sm is PsmFromTsv { BetaPeptideChildScanMatchedIons: not null } peptide)
                {
                    allChildScanMatchedIons = allChildScanMatchedIons.Concat(peptide.BetaPeptideChildScanMatchedIons)
                        .GroupBy(p => p.Key)
                        .ToDictionary(p => p.Key, q => q.SelectMany(p => p.Value).ToList());
                }

                foreach (var childScanMatchedIons in allChildScanMatchedIons)
                {
                    int childScanNumber = childScanMatchedIons.Key;

                    if (scansDrawn.Contains(childScanNumber))
                    {
                        continue;
                    }

                    scansDrawn.Add(childScanNumber);

                    List<MatchedFragmentIon> matchedIons = childScanMatchedIons.Value;

                    MsDataScan childScan = spectraFile.GetOneBasedScanFromDynamicConnection(childScanNumber);

                    string childAnnotation = "Scan: " + childScan.OneBasedScanNumber
                        + " Dissociation Type: " + childScan.DissociationType
                        + " MsOrder: " + childScan.MsnOrder
                        + " Selected Mz: " + childScan.SelectedIonMZ.Value.ToString("0.##")
                        + " RetentionTime: " + childScan.RetentionTime.ToString("0.##");

                    Canvas childCanvas = new Canvas();
                    DrawnSequence childSequence = new(childCanvas, sm, false);
                    childSequence.AnnotateBaseSequence(sm.BaseSeq, sm.FullSequence, 10, matchedIons, sm);
                    PlotView childPlotView = new PlotView(); // placeholder

                    // make the plot
                    var childPlot = new PeptideSpectrumMatchPlot(childPlotView, sm, childScan, matchedIons, annotateProperties: false);
                    childPlot.Model.Title = null;
                    childPlot.Model.Subtitle = null;

                    item = new ParentChildScanPlotTemplate() { Plot = childPlot, SpectrumLabel = childAnnotation, TheCanvas = childSequence.SequenceDrawingCanvas };

                    // remove model from placeholder (the model can only be referenced by 1 plotview at a time)
                    childPlotView.Model = null;

                    parentChildScanPlotsView.Plots.Add(item);

                    CurrentlyDisplayedPlots.Add(childPlot);
                }
                spectraFile.CloseDynamicConnection();
            }
        }

        /// <summary>
        /// Draws the Sequences, both stationary and scrolling
        /// </summary>
        /// <param name="stationaryCanvas"></param>
        /// <param name="scrollableCanvas"></param>
        /// <param name="sm"></param>
        public void DisplaySequences(Canvas stationaryCanvas, Canvas scrollableCanvas, Canvas sequenceAnnotationCanvas, SpectrumMatchFromTsv sm)
        {
            if (!sm.FullSequence.Contains('|'))
            {
                if (scrollableCanvas != null)
                {
                    ScrollableSequence = new(scrollableCanvas, sm, false);
                }

                if (stationaryCanvas != null && MetaDrawSettings.DrawStationarySequence)
                {
                    if (!sm.IsCrossLinkedPeptide()) // if not crosslinked
                    {
                        StationarySequence = new(stationaryCanvas, sm, true);
                    }
                    else
                    {
                        StationarySequence = new(stationaryCanvas, sm, false);
                        StationarySequence.DrawCrossLinkSequence();
                    }
                }

                if (sequenceAnnotationCanvas != null)
                {
                   SequenceAnnotation = new(sequenceAnnotationCanvas, sm, false, true);
                }
            }   
        }

        //draw the sequence coverage map: write out the sequence, overlay modifications, and display matched fragments
        public void DrawSequenceCoverageMap(SpectrumMatchFromTsv sm, Canvas sequenceText, Canvas map)
        {
            map.Children.Clear();
            sequenceText.Children.Clear();

            int spacing = 20;
            const int textHeight = 140;
            const int heightIncrement = 5;
            const int xShift = 10;
            int peptideLength = sm.BaseSeq.Length;

            //intensity arrays for each ion type
            double[] nIntensityArray = new double[peptideLength - 1];
            double[] cIntensityArray = new double[peptideLength - 1];
            double[] internalIntensityArray = new double[peptideLength - 1];

            //colors for annotation
            Color nColor = DrawnSequence.ParseColorFromOxyColor(MetaDrawSettings.CoverageTypeToColor["N-Terminal Color"]);
            Color cColor = DrawnSequence.ParseColorFromOxyColor(MetaDrawSettings.CoverageTypeToColor["C-Terminal Color"]);
            Color internalColor = DrawnSequence.ParseColorFromOxyColor(MetaDrawSettings.CoverageTypeToColor["Internal Color"]);

            //draw sequence text
            for (int r = 0; r < sm.BaseSeq.Length; r++)
            {
                TextDrawing(sequenceText, new Point(r * spacing + xShift, textHeight - 30), (r + 1).ToString(), Brushes.Black, 8);
                TextDrawing(sequenceText, new Point(r * spacing + xShift, textHeight - 15), (sm.BaseSeq.Length - r).ToString(), Brushes.Black, 8);
                TextDrawing(sequenceText, new Point(r * spacing + xShift, textHeight), sm.BaseSeq[r].ToString(), Brushes.Black, 16);
            }

            //create circles for mods, if needed and able
            if (!sm.FullSequence.Contains("|")) //can't draw mods if not localized/identified
            {
                DrawnSequence.AnnotateModifications(sm, sequenceText, sm.FullSequence, textHeight-4, spacing, xShift+5);
            }

            //draw lines for each matched fragment
            List<bool[]> index = new List<bool[]>();

            //N-terminal
            List<MatchedFragmentIon> leftTermFragments = sm.MatchedIons.Where(x => x.NeutralTheoreticalProduct.Terminus is FragmentationTerminus.N or FragmentationTerminus.FivePrime).ToList();
            //C-terminal in reverse order
            List<MatchedFragmentIon> rightTermFragments = sm.MatchedIons.Where(x => x.NeutralTheoreticalProduct.Terminus is FragmentationTerminus.C or FragmentationTerminus.ThreePrime).OrderByDescending(x => x.NeutralTheoreticalProduct.FragmentNumber).ToList();
            //add internal fragments
            List<MatchedFragmentIon> internalFragments = sm.MatchedIons.Where(x => x.NeutralTheoreticalProduct.SecondaryProductType != null).OrderBy(x => x.NeutralTheoreticalProduct.FragmentNumber).ToList();

            //indexes to navigate terminal ions
            int n = 0;
            int c = 0;
            int heightForThisFragment = 70; //location to draw a fragment

            //line up terminal fragments so that complementary ions are paired on the same line
            while (n < leftTermFragments.Count && c < rightTermFragments.Count)
            {
                MatchedFragmentIon nProduct = leftTermFragments[n];
                MatchedFragmentIon cProduct = rightTermFragments[c];
                int expectedComplementary = peptideLength - nProduct.NeutralTheoreticalProduct.FragmentNumber;
                //if complementary pair
                if (cProduct.NeutralTheoreticalProduct.FragmentNumber == expectedComplementary)
                {
                    //plot sequences
                    DrawHorizontalLine(0, nProduct.NeutralTheoreticalProduct.FragmentNumber, map, heightForThisFragment, nColor, spacing);
                    DrawHorizontalLine(peptideLength - cProduct.NeutralTheoreticalProduct.FragmentNumber, peptideLength, map, heightForThisFragment, cColor, spacing);

                    //record intensities
                    nIntensityArray[nProduct.NeutralTheoreticalProduct.FragmentNumber - 1] += nProduct.Intensity;
                    cIntensityArray[peptideLength - cProduct.NeutralTheoreticalProduct.FragmentNumber - 1] += cProduct.Intensity;

                    //increment indexes
                    n++;
                    c++;
                }
                //if n-terminal ion is present without complementary
                else if (cProduct.NeutralTheoreticalProduct.FragmentNumber < expectedComplementary)
                {
                    DrawHorizontalLine(0, nProduct.NeutralTheoreticalProduct.FragmentNumber, map, heightForThisFragment, nColor, spacing);
                    nIntensityArray[nProduct.NeutralTheoreticalProduct.FragmentNumber - 1] += nProduct.Intensity;
                    n++;
                }
                //if c-terminal ion is present without complementary
                else
                {
                    DrawHorizontalLine(peptideLength - cProduct.NeutralTheoreticalProduct.FragmentNumber, peptideLength, map, heightForThisFragment, cColor, spacing);
                    cIntensityArray[peptideLength - cProduct.NeutralTheoreticalProduct.FragmentNumber] += cProduct.Intensity;
                    c++;
                }
                heightForThisFragment += heightIncrement;
            }
            //wrap up leftover fragments without complementary pairs
            for (; n < leftTermFragments.Count; n++)
            {
                MatchedFragmentIon nProduct = leftTermFragments[n];
                DrawHorizontalLine(0, nProduct.NeutralTheoreticalProduct.FragmentNumber, map, heightForThisFragment, nColor, spacing);
                nIntensityArray[nProduct.NeutralTheoreticalProduct.FragmentNumber - 1] += nProduct.Intensity;
                heightForThisFragment += heightIncrement;
            }
            for (; c < rightTermFragments.Count; c++)
            {
                MatchedFragmentIon cProduct = rightTermFragments[c];
                DrawHorizontalLine(peptideLength - cProduct.NeutralTheoreticalProduct.FragmentNumber, peptideLength, map, heightForThisFragment, cColor, spacing);
                cIntensityArray[peptideLength - cProduct.NeutralTheoreticalProduct.FragmentNumber - 1] += cProduct.Intensity;
                heightForThisFragment += heightIncrement;
            }

            //internal fragments
            if (MetaDrawSettings.DisplayInternalIons)
            {
                foreach (MatchedFragmentIon fragment in internalFragments)
                {
                    DrawHorizontalLine(fragment.NeutralTheoreticalProduct.FragmentNumber,
                        fragment.NeutralTheoreticalProduct.SecondaryFragmentNumber, map, heightForThisFragment,
                        internalColor, spacing);
                    internalIntensityArray[fragment.NeutralTheoreticalProduct.FragmentNumber - 1] += fragment.Intensity;
                    internalIntensityArray[fragment.NeutralTheoreticalProduct.SecondaryFragmentNumber - 1] +=
                        fragment.Intensity;
                    heightForThisFragment += heightIncrement;
                }
            }

            map.Height = heightForThisFragment + 100;
            map.Width = spacing * sm.BaseSeq.Length + 100;
            sequenceText.Width = spacing * sm.BaseSeq.Length + 100;

            ////PLOT INTENSITY HISTOGRAM////
            double[] intensityArray = new double[peptideLength - 1];
            for (int i = 0; i < intensityArray.Length; i++)
            {
                intensityArray[i] = nIntensityArray[i] + cIntensityArray[i] + internalIntensityArray[i];
            }

            double maxIntensity = intensityArray.Max();

            //foreach cleavage site
            for (int i = 0; i < intensityArray.Length; i++)
            {
                //if anything
                if (intensityArray[i] > 0)
                {
                    int x = (i + 1) * spacing + 7;
                    //n-terminal
                    int nY = 100 - (int)Math.Round(nIntensityArray[i] * 100 / maxIntensity, 0);
                    if (nY != 100)
                    {
                        DrawVerticalLine(104, nY + 4, sequenceText, (i + 1) * spacing + 5, nColor);
                    }
                    //c-terminal
                    int cY = nY - (int)Math.Round(cIntensityArray[i] * 100 / maxIntensity, 0);
                    if (nY != cY)
                    {
                        DrawVerticalLine(nY + 2, cY + 2, sequenceText, (i + 1) * spacing + 5, cColor);
                    }
                    //internal
                    int iY = cY - (int)Math.Round(internalIntensityArray[i] * 100 / maxIntensity, 0);
                    if (cY != iY)
                    {
                        DrawVerticalLine(cY, iY, sequenceText, (i + 1) * spacing + 5, internalColor);
                    }
                }
            }
        }

        public static void TextDrawing(Canvas sequenceText, Point loc, string txt, Brush clr, int fontSize)
        {
            TextBlock tb = new TextBlock();
            tb.Foreground = clr;
            tb.Text = txt;
            tb.FontSize = fontSize;
            if (clr == Brushes.Black)
            {
                tb.FontWeight = System.Windows.FontWeights.Bold;
            }
            else
            {
                tb.FontWeight = System.Windows.FontWeights.ExtraBold;
            }
            tb.FontFamily = new FontFamily("Arial"); // monospaced font

            Canvas.SetTop(tb, loc.Y);
            Canvas.SetLeft(tb, loc.X);
            Panel.SetZIndex(tb, 2); //lower priority
            sequenceText.Children.Add(tb);
            sequenceText.UpdateLayout();
        }

        public static void DrawHorizontalLine(int start, int end, Canvas map,
       int height, Color clr, int spacing)
        {
            DrawLine(map, new Point(start * spacing + 7, height),
                new Point(end * spacing + 4, height), clr);
        }

        public static void DrawVerticalLine(int start, int end, Canvas map,
      int x, Color clr)
        {
            DrawLine(map, new Point(x, start),
                new Point(x, end), clr);
        }

        public static void DrawLine(Canvas cav, Point start, Point end, Color clr)
        {
            Line line = new Line();
            line.Stroke = new SolidColorBrush(clr);

            line.X1 = start.X;
            line.X2 = end.X;
            line.Y1 = start.Y;
            line.Y2 = end.Y;
            line.StrokeThickness = 3.25;
            line.StrokeStartLineCap = PenLineCap.Round;
            line.StrokeEndLineCap = PenLineCap.Round;

            cav.Children.Add(line);

            Canvas.SetZIndex(line, 1); //on top of any other things in canvas
        }

        public void ExportPlot(PlotView plotView, Canvas stationaryCanvas, List<SpectrumMatchFromTsv> spectrumMatches,
            ParentChildScanPlotsView parentChildScanPlotsView, string directory, out List<string> errors,
            Canvas legendCanvas = null, Vector ptmLegendLocationVector = new(), FragmentationReanalysisViewModel? reFragment = null)
        {
            errors = new List<string>();

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            foreach (var psm in spectrumMatches)
            {
                // get the scan
                if (!MsDataFiles.TryGetValue(psm.FileNameWithoutExtension, out MsDataFile spectraFile))
                {
                    errors.Add("The spectra file could not be found for this PSM: " + psm.FileNameWithoutExtension);
                    return;
                }

                // if we have ions that were not originally search for, cache original, find new ions, plot, replace original
                List<MatchedFragmentIon> oldMatchedIons = null;
                if (reFragment is not null)
                {
                    oldMatchedIons = psm.MatchedIons;
                    var scan = GetMs2ScanFromPsm(psm);
                    var newIons = reFragment.MatchIonsWithNewTypes(scan, psm);
                    psm.MatchedIons = newIons;
                }

                if (plotView.Name == "plotView")
                {
                    DisplaySequences(stationaryCanvas, null, null, psm);
                    DisplaySpectrumMatch(plotView, psm, parentChildScanPlotsView, out errors);
                }
                else if (plotView.Name == "chimeraPlot")
                {
                    List<SpectrumMatchFromTsv> chimericSms = FilteredListOfPsms
                        .Where(p => p.Ms2ScanNumber == psm.Ms2ScanNumber && p.FileNameWithoutExtension == psm.FileNameWithoutExtension).ToList();
                    DisplayChimeraSpectra(plotView, chimericSms, out errors);
                }
                

                if (errors != null)
                {
                    errors.AddRange(errors); 
                }

                string sequence = psm.IsPeptide()
                    ? !(psm as PsmFromTsv).UniqueSequence.IsNullOrEmptyOrWhiteSpace()
                        ? illegalInFileName.Replace((psm as PsmFromTsv).UniqueSequence, string.Empty)
                        : illegalInFileName.Replace(psm.FullSequence, string.Empty)
                    : psm.FullSequence;

                if (sequence.Length > 30)
                {
                    sequence = sequence.Substring(0, 30);
                }

                foreach (var plot in CurrentlyDisplayedPlots)
                {
                    string filePath = System.IO.Path.Combine(directory, plot.Scan.OneBasedScanNumber + "_" + sequence + "." + MetaDrawSettings.ExportType);

                    int i = 2;
                    while (File.Exists(filePath))
                    {
                        filePath = System.IO.Path.Combine(directory, plot.Scan.OneBasedScanNumber + "_" + sequence + "_" + i + "." + MetaDrawSettings.ExportType);
                        i++;
                    }

                    var type = plot.GetType();

                    switch (type.Name)
                    {
                        case "PeptideSpectrumMatchPlot":
                            if (!plot.SpectrumMatch.FullSequence.Contains('['))
                                legendCanvas = null;
                            ((PeptideSpectrumMatchPlot)plot).ExportPlot(filePath, StationarySequence.SequenceDrawingCanvas,
                                legendCanvas, ptmLegendLocationVector, plotView.ActualWidth, plotView.ActualHeight);
                            break;

                        case "ChimeraSpectrumMatchPlot":
                            ((ChimeraSpectrumMatchPlot)plot).ExportPlot(filePath, legendCanvas, plotView.ActualWidth,
                                plotView.ActualHeight);
                            break;

                        case "CrosslinkSpectrumMatchPlot":
                            ((CrosslinkSpectrumMatchPlot)plot).ExportPlot(filePath, StationarySequence.SequenceDrawingCanvas,
                                legendCanvas, ptmLegendLocationVector, plotView.ActualWidth, plotView.ActualHeight);
                            break;
                    }
                }
                // put the original ions back in place if they were altered
                if (oldMatchedIons != null && !psm.MatchedIons.SequenceEqual(oldMatchedIons))
                    psm.MatchedIons = oldMatchedIons;
            }

            if (plotView.Name == "plotView")
            {
                var psm = spectrumMatches.First();

                // if we have ions that were not originally search for, cache original, find new ions, plot, replace original
                List<MatchedFragmentIon> oldMatchedIons = null;
                if (reFragment is not null && reFragment.Persist)
                {
                    oldMatchedIons = psm.MatchedIons;
                    var scan = GetMs2ScanFromPsm(psm);
                    var newIons = reFragment.MatchIonsWithNewTypes(scan, psm);
                    psm.MatchedIons = newIons;
                }
                DisplaySequences(stationaryCanvas, null, null, psm);
                DisplaySpectrumMatch(plotView, psm, parentChildScanPlotsView, out errors);

                // put the original ions back in place if they were altered
                if (oldMatchedIons != null && !psm.MatchedIons.SequenceEqual(oldMatchedIons))
                    psm.MatchedIons = oldMatchedIons;
            }
            else if (plotView.Name == "chimeraPlot")
            {
                List<SpectrumMatchFromTsv> chimericPsms = FilteredListOfPsms
                    .Where(p => p.Ms2ScanNumber == spectrumMatches.First().Ms2ScanNumber &&
                                p.FileNameWithoutExtension == spectrumMatches.First().FileNameWithoutExtension)
                    .ToList();
                DisplayChimeraSpectra(plotView, chimericPsms, out errors);
            }
        }

        public MsDataScan GetMs2ScanFromPsm(SpectrumMatchFromTsv spectralMatch)
        {
            return !MsDataFiles.TryGetValue(spectralMatch.FileNameWithoutExtension, out MsDataFile spectraFile) 
                ? null 
                : spectraFile.GetOneBasedScanFromDynamicConnection(spectralMatch.Ms2ScanNumber);
        }

        /// <summary>
        /// Exports the sequence coverage view to an image file
        /// </summary>
        /// <param name="textCanvas">representes the text and intensity bars</param>
        /// <param name="mapCanvas">represents the sequence coverage map</param>
        /// <param name="directory">where the files will be outputted</param>
        /// <param name="fullSequence">fullsequence of the sm map being outputted</param>
        /// <param name="scanNumber">MS2 scan number of the sm map being outputted</param>
        public void ExportSequenceCoverage(Canvas textCanvas, Canvas mapCanvas, string directory, SpectrumMatchFromTsv sm)
        {
            // initialize values
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string sequence = illegalInFileName.Replace(sm.FullSequence, string.Empty);
            if (sequence.Length > 30)
            {
                sequence = sequence.Substring(0, 30);
            }
            string path = System.IO.Path.Combine(directory, sm.Ms2ScanNumber + "_" + sequence + "_SequenceCoverage." + MetaDrawSettings.ExportType);

            // convert to format for export
            System.Drawing.Bitmap textBitmap = ConvertCanvasToBitmap(textCanvas, directory);
            Point textPoint = new(0, 0);
            System.Drawing.Bitmap mapBitmap = ConvertCanvasToBitmap(mapCanvas, directory);
            Point mapPoint = new(0, textCanvas.ActualHeight );

            List<System.Drawing.Bitmap> toCombine = new List<System.Drawing.Bitmap>() { textBitmap, mapBitmap };
            List<Point> points = new List<Point>() { textPoint, mapPoint };
            System.Drawing.Bitmap combinedBitmap = CombineBitmap(toCombine, points, false);

            ExportBitmap(combinedBitmap, path);
        }

        /// <summary>
        /// Exports the sequence annotation view to an image file
        /// </summary>
        /// <param name="sequenceAnnotaitonCanvas">canvas of the sequence annotaiton</param>
        /// <param name="ptmLegend">current depiction of the ptm legend</param>
        /// <param name="psm">the sm being annotated</param>
        /// <param name="directory">where the files will be outputte</param>
        /// <param name="width">width of the annotation area</param>
        public void ExportAnnotatedSequence(Canvas sequenceAnnotaitonCanvas, System.Windows.UIElement ptmLegend, SpectrumMatchFromTsv psm, string directory, int width)
        {
            // initialize values
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string sequence = illegalInFileName.Replace(psm.FullSequence, string.Empty);
            if (sequence.Length > 30)
            {
                sequence = sequence.Substring(0, 30);
            }
            string path = System.IO.Path.Combine(directory, psm.Ms2ScanNumber + "_" + sequence + "_SequenceAnnotation." + MetaDrawSettings.ExportType);
            int rows = (int)Math.Ceiling((double)psm.BaseSeq.Length / (MetaDrawSettings.SequenceAnnotaitonResiduesPerSegment * MetaDrawSettings.SequenceAnnotationSegmentPerRow)); ;

            // convert to format for export
            sequenceAnnotaitonCanvas.Width = width;
            System.Drawing.Bitmap annotationBitmap = ConvertCanvasToBitmap(sequenceAnnotaitonCanvas, directory);
            Point annotationPoint = new(-100, 0);
            
            System.Drawing.Bitmap ptmLegendBitmap = ConvertUIElementToBitmap(ptmLegend, directory);
            Point ptmLegendPoint = new((annotationBitmap.Width / 2) - (ptmLegend.RenderSize.Width / 2) - 50, sequenceAnnotaitonCanvas.Height);

            List<System.Drawing.Bitmap> toCombine = new List<System.Drawing.Bitmap>() { annotationBitmap, ptmLegendBitmap };
            List<Point> points = new List<Point>() { annotationPoint, ptmLegendPoint };
            System.Drawing.Bitmap combinedBitmap = CombineBitmap(toCombine, points, false);
            System.Drawing.Bitmap finalBitmap = combinedBitmap.Clone(new System.Drawing.Rectangle(0, 0, combinedBitmap.Width - 140, combinedBitmap.Height), combinedBitmap.PixelFormat);
            ExportBitmap(finalBitmap, path);
            combinedBitmap.Dispose();
            finalBitmap.Dispose();
        }

        /// <summary>
        /// Used to combine multiple bitmap objects
        /// </summary>
        /// <param name="images">list of objects to combine</param>
        /// <param name="points">the position to begin drawing each</param>
        /// <param name="overlap">true of they should overlap, false if they should stack ontop of one another vertically</param>
        /// <returns></returns>
        public static System.Drawing.Bitmap CombineBitmap(List<System.Drawing.Bitmap> images, List<Point> points, bool overlap = true)
        {
            System.Drawing.Bitmap finalImage = null;

            try
            {
                int width = 0;
                int height = 0;

                foreach (var image in images)
                {
                    //update the size of the final bitmap
                    if (overlap)
                    {
                        width = image.Width > width ? image.Width : width;
                        height = image.Height > height ? image.Height : height;
                    }
                    else
                    {
                        width = Math.Max(image.Width, width);
                        height += image.Height;
                    }
                }

                //create a bitmap to hold the combined image
                finalImage = new System.Drawing.Bitmap(width, height);

                //get a graphics object from the image so we can draw on it
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(finalImage))
                {
                    //set background color
                    g.Clear(System.Drawing.Color.White);

                    //go through each image and draw it on the final image
                    for (int i = 0; i < images.Count; i++)
                    {
                        g.DrawImage(images[i],
                          new System.Drawing.Rectangle((int)points[i].X, (int)points[i].Y, images[i].Width, images[i].Height));
                    }
                }

                return finalImage;
            }
            catch (Exception)
            {
                finalImage?.Dispose();
                throw;
            }
            finally
            {
                //clean up memory
                foreach (System.Drawing.Bitmap image in images)
                {
                    image.Dispose();
                }
            }
        }

        public void FilterPsms()
        {
            lock (ThreadLocker)
            {
                FilteredListOfPsms.Clear();

                foreach (var psm in AllSpectralMatches.Where(p => MetaDrawSettings.FilterAcceptsPsm(p)))
                {
                    FilteredListOfPsms.Add(psm);
                }
            }
        }

        public void FilterPsmsByString(string searchString)
        {
            if (searchString == "")
            {
                PeptideSpectralMatchesView.Filter = null;
            }
            else
            {
                PeptideSpectralMatchesView.Filter = obj =>
                {
                    SpectrumMatchFromTsv psm = obj as SpectrumMatchFromTsv;
                    return ((psm.Ms2ScanNumber.ToString()).StartsWith(searchString) || psm.FullSequence.ToUpper().Contains(searchString.ToUpper()) 
                    || psm.Name.Contains(searchString) || psm.OrganismName.Contains(searchString));
                };
            }
        }

        public void FilterPsmsToChimerasOnly()
        {
            lock (ThreadLocker)
            {
                FilteredListOfPsms.Clear();

                var filteredChimericPsms = ChimericSpectralMatches.Where(MetaDrawSettings.FilterAcceptsPsm);
                foreach (var psm in filteredChimericPsms)
                {
                    if (filteredChimericPsms.Count(p => p.Ms2ScanNumber == psm.Ms2ScanNumber && p.FileNameWithoutExtension == psm.FileNameWithoutExtension) > 1)
                        FilteredListOfPsms.Add(psm);
                }
            }

        }

        public void CleanUpResources()
        {
            lock (ThreadLocker)
            {
                CleanUpPSMFiles();
                CleanUpSpectraFiles();
                CleanUpSpectralLibraryFiles();
            }
        }

        public void CleanUpSpectraFiles()
        {
            lock (ThreadLocker)
            {
                SpectraFilePaths.Clear();
                foreach (var connection in MsDataFiles)
                {
                    connection.Value.CloseDynamicConnection();
                }
                Thread.Sleep(1000); // sleep for one second to allow for test methods ran asychronously in parallel to finish, otherwise the tests will fail
                MsDataFiles.Clear();
            }
        }

        public void CleanUpPSMFiles()
        {
            lock (ThreadLocker)
            {
                AllSpectralMatches.Clear();
                FilteredListOfPsms.Clear();
                SpectralMatchResultFilePaths.Clear();
            }
        }

        public void CleanUpSpectralLibraryFiles()
        {
            lock (ThreadLocker)
            {
                SpectralLibraryPaths.Clear();
                if (SpectralLibrary != null)
                {
                    SpectralLibrary.CloseConnections();
                }
            }
        }

        public void CleanUpCurrentlyDisplayedPlots()
        {
            if (CurrentlyDisplayedPlots != null && CurrentlyDisplayedPlots.Any())
                CurrentlyDisplayedPlots.Clear();
        }

        #region Private Helpers

        /// <summary>
        /// Converts a canvas to a bitmap object
        /// </summary>
        /// <param name="canvas">canvas to be converted</param>
        /// <param name="directory">directory for the temporary file to be stored</param>
        /// <returns></returns>
        private static System.Drawing.Bitmap ConvertCanvasToBitmap(Canvas canvas, string directory)
        {
            double dpiScale = MetaDrawSettings.CanvasPdfExportDpi / 96.0;
            string tempBitmapPath = System.IO.Path.Combine(directory, "temp.bmp");
            int height = (int)canvas.Height == -2147483648 ? (int)canvas.ActualHeight : (int)canvas.Height;
            int width = (int)canvas.Width == -2147483648 ? (int)canvas.ActualWidth : (int)canvas.Width;
            Size canvasSize = new Size(width, height);
            canvas.Measure(canvasSize);
            canvas.Arrange(new Rect(canvasSize));
            RenderTargetBitmap renderCanvasBitmap = new((int)(dpiScale * width), (int)(dpiScale * height),
                MetaDrawSettings.CanvasPdfExportDpi, MetaDrawSettings.CanvasPdfExportDpi, PixelFormats.Pbgra32);
            renderCanvasBitmap.Render(canvas);

            BmpBitmapEncoder encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderCanvasBitmap));
            using (FileStream file = File.Create(tempBitmapPath))
            {
                encoder.Save(file);
            }

            System.Drawing.Bitmap unformattedBitmap = new(tempBitmapPath);
            System.Drawing.Bitmap bitmap = new(unformattedBitmap, new System.Drawing.Size(width, height));
            unformattedBitmap.Dispose();
            File.Delete(tempBitmapPath);
            return bitmap;
        }

        /// <summary>
        /// converts a given UI element to a bitmap representation
        /// </summary>
        /// <param name="visual">element to be converted</param>
        /// <param name="directory">directory for temporary file storage</param>
        /// <returns></returns>
        private static System.Drawing.Bitmap ConvertUIElementToBitmap(System.Windows.UIElement visual, string directory)
        {
            // initialize values
            double dpiScale = MetaDrawSettings.CanvasPdfExportDpi / 96.0;
            string tempBitmapPath = System.IO.Path.Combine(directory, "temp.bmp");

            if (visual == null)
            {
                return null;
            }

            int width = (int)visual.RenderSize.Width == 0 ? 200 : (int)visual.RenderSize.Width;
            int height = (int)visual.RenderSize.Height == 0 ? 100 : (int)visual.RenderSize.Height;
            Size size = new Size(width, height);
            Rect bounds = new(size);
            visual.Measure(size);
            visual.Arrange(bounds);
            visual.UpdateLayout();

            RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap((int)(width * dpiScale), (int)(height * dpiScale),
                MetaDrawSettings.CanvasPdfExportDpi, MetaDrawSettings.CanvasPdfExportDpi, PixelFormats.Pbgra32);
            VisualBrush visualBrush = new(visual);

            // draw UIElement on a bitmap
            DrawingVisual drawingVisual = new();
            DrawingContext drawingContext = drawingVisual.RenderOpen();
            using (drawingContext)
            {
                drawingContext.DrawRectangle(visualBrush, null, new Rect(new Point(0, 0), new Point(width, height)));
            }
            renderTargetBitmap.Render(drawingVisual);

            // export and reload bitmap in correct formatting
            BitmapEncoder encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));
            using (FileStream file = File.Create(tempBitmapPath))
            {
                encoder.Save(file);
            }

            System.Drawing.Bitmap unformattedBitmap = new System.Drawing.Bitmap(tempBitmapPath);
            System.Drawing.Bitmap bitmap = new(unformattedBitmap, new System.Drawing.Size(width, height));
            unformattedBitmap.Dispose();
            File.Delete(tempBitmapPath);
            return bitmap;
        }

        /// <summary>
        /// Exports a bitmap as the specified file type
        /// </summary>
        /// <param name="bitmap">image to be exported</param>
        /// <param name="path">where it should be exported to</param>
        private void ExportBitmap(System.Drawing.Bitmap bitmap, string path)
        {
            switch (MetaDrawSettings.ExportType)
            {
                case "Pdf":
                    string tempImagePath = path.Replace(".Pdf", ".png");
                    bitmap.Save(tempImagePath, System.Drawing.Imaging.ImageFormat.Png);
                    ImageData imageData = ImageDataFactory.Create(tempImagePath);
                    File.Delete(tempImagePath);
                    iText.Layout.Element.Image pdfImage = new(imageData);

                    PdfDocument pdfDocument = new(new PdfWriter(path));
                    iText.Layout.Document document = new(pdfDocument);
                    document.Add(pdfImage);
                    pdfDocument.Close();
                    document.Close();
                    break;

                case "Png":
                    bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                    break;

                case "Jpeg":
                    bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Jpeg);
                    break;

                case "Tiff":
                    bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Tiff);
                    break;

                case "Wmf":
                    bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Wmf);
                    break;

                case "Bmp":
                    bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Bmp);
                    break;
            }
        }

        private void LoadPsms(out List<string> errors, bool haveLoadedSpectra)
        {
            errors = new List<string>();

            HashSet<string> fileNamesWithoutExtension = new HashSet<string>(
                SpectraFilePaths.Select(p => System.IO.Path.GetFileName(p.Replace(GlobalVariables.GetFileExtension(p), string.Empty))));
            List<SpectrumMatchFromTsv> psmsThatDontHaveMatchingSpectraFile = new List<SpectrumMatchFromTsv>();

            try
            {
                foreach (var resultsFile in SpectralMatchResultFilePaths)
                {
                    lock (ThreadLocker)
                    {
                        var psms = SpectrumMatchTsvReader.ReadTsv(resultsFile, out List<string> warnings);
                        foreach (SpectrumMatchFromTsv psm in psms)
                        {
                            if (fileNamesWithoutExtension.Contains(psm.FileNameWithoutExtension) || !haveLoadedSpectra)
                            {
                                AllSpectralMatches.Add(psm);
                            }
                            else
                            {
                                psmsThatDontHaveMatchingSpectraFile.Add(psm);
                            }

                            if (SpectralMatchesGroupedByFile.TryGetValue(psm.FileNameWithoutExtension, out var psmsForThisFile))
                            {
                                psmsForThisFile.Add(psm);
                            }
                            else
                            {
                                SpectralMatchesGroupedByFile.Add(psm.FileNameWithoutExtension, new ObservableCollection<SpectrumMatchFromTsv> { psm });
                            }
                        }
                    }
                }

                foreach (var group in AllSpectralMatches
                             .GroupBy(p => (p.Ms2ScanNumber, p.FileNameWithoutExtension))
                             .Where(group => group.Count() > 1))
                {
                    foreach (var psm in group)
                    {
                        ChimericSpectralMatches.Add(psm);
                    }
                }

            }
            catch (Exception e)
            {
                errors.Add("Error reading PSM file:\n" + e.Message);
            }

            if (psmsThatDontHaveMatchingSpectraFile.Any())
            {
                foreach (var file in psmsThatDontHaveMatchingSpectraFile.GroupBy(p => p.FileNameWithoutExtension))
                {
                    errors.Add(file.Count() + " PSMs from " + file.Key + " were not loaded because this spectra file was not found");
                }
            }

            FilterPsms();
        }

        private void LoadSpectraFiles(out List<string> errors)
        {
            errors = new List<string>();

            foreach (var filepath in SpectraFilePaths)
            {
                lock (ThreadLocker)
                {
                    var fileNameWithoutExtension = System.IO.Path.GetFileName(filepath.Replace(GlobalVariables.GetFileExtension(filepath), string.Empty));
                    var spectraFile = MsDataFileReader.GetDataFile(filepath);
                    if (spectraFile is TimsTofFileReader timsTofDataFile)
                    {
                        timsTofDataFile.LoadAllStaticData(maxThreads: Environment.ProcessorCount - 1); // timsTof files need to load all static data before they can be used, as dynamic access is not available for them
                    }
                    else
                    {
                        spectraFile.InitiateDynamicConnection();
                    }

                    if (!MsDataFiles.TryAdd(fileNameWithoutExtension, spectraFile))
                    {
                        spectraFile.CloseDynamicConnection();
                        // print warning? but probably unnecessary. this means the data file was loaded twice. 
                        // which is an error but not an important one because the data is loaded
                    }
                }
            }
        }

        private void LoadSpectralLibraries(out List<string> errors)
        {
            errors = new List<string>();

            try
            {
                SpectralLibrary = new SpectralLibrary(SpectralLibraryPaths.ToList());
            }
            catch (Exception e)
            {
                SpectralLibrary = null;
                errors.Add("Problem loading spectral library: " + e.Message);
            }
        }
    }

    #endregion
}
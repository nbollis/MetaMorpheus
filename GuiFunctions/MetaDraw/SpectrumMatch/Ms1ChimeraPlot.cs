using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chemistry;
using Easy.Common.Extensions;
using Easy.Common.Interfaces;
using EngineLayer;
using MassSpectrometry;
using MzLibUtil;
using Nett;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Wpf;
using Proteomics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using TaskLayer;
using LineAnnotation = OxyPlot.Wpf.LineAnnotation;
using LineSeries = OxyPlot.Series.LineSeries;

namespace GuiFunctions.MetaDraw.SpectrumMatch
{
    public class Ms1ChimeraPlot : SpectrumMatchPlot
    {
        public ChimeraGroupViewModel ChimeraGroup { get; private set; }
        public PlotView PlotView { get; private set; }
        public DoubleRange Range { get; private set; }

        public Ms1ChimeraPlot(PlotView plotView, ChimeraGroupViewModel chimeraGroupVm) : base(plotView, null,
            chimeraGroupVm.Ms1Scan)
        {
            PlotView = plotView;
            Range = chimeraGroupVm.Ms2Scan.IsolationRange;
            ChimeraGroup = chimeraGroupVm;

            AnnotateChimericPeaks(chimeraGroupVm);
            Model.Axes[0].MajorStep = 1;
            Model.Axes[0].MinorStep = 0.2;
            SetTitle();
            ZoomAxes();
            AnnotateIsolationWindow();
            RefreshChart();
        }

        private void AnnotateIsolationWindow()
        {
            var isolationWindow = ChimeraGroup.Ms2Scan.IsolationRange;
            List<DataPoint> points = new List<DataPoint>()
            {
                new(isolationWindow.Minimum, Model.Axes[1].Maximum),
                new(isolationWindow.Minimum, 0),
                new(isolationWindow.Maximum, 0),
                new(isolationWindow.Maximum, Model.Axes[1].Maximum),
            };
            var lineSeries = new LineSeries();
            lineSeries.Points.AddRange(points);
            lineSeries.LineStyle = LineStyle.Dash;
            lineSeries.Color = OxyColors.Red;
            Model.Series.Add(lineSeries);
        }


        private void AnnotateChimericPeaks(ChimeraGroupViewModel chimeraGroupVm)
        {
            foreach (var ionGroup in chimeraGroupVm.PrecursorIonsByColor)
            {
                var color = ionGroup.Key;
                ionGroup.Value.ForEach(p => AnnotatePeak(p.Item1, false, false, color, p.Item2));
            }
        }

        private static string _FractPattern = @"fract(\d+)";
        private void SetTitle()
        {
            string title = "";
            var match = System.Text.RegularExpressions.Regex.Match(ChimeraGroup.FileNameWithoutExtension, _FractPattern);
            if (match.Success)
            {
                var fract = match.Groups[1].Value;
                if (ChimeraGroup.FileNameWithoutExtension.Contains("-calib-averaged"))
                {
                    title = $"Jurkat Fraction {fract} - Precursor Spectrum {ChimeraGroup.Ms1Scan.OneBasedScanNumber}";
                }
                else if (ChimeraGroup.FileNameWithoutExtension.Contains("-calib"))
                {
                    title = $"Jurkat Fraction {fract} - Calibrated Only - Scan Number {ChimeraGroup.Ms1Scan.OneBasedScanNumber}";
                }
            }
            else
            {
                title = ChimeraGroup.FileNameWithoutExtension + '-' + ChimeraGroup.Ms1Scan.OneBasedScanNumber;
            }

            Model.Title = title;
        }

        public void ZoomAxes()
        {
            var maxIntensity = ChimeraGroup.Ms1Scan.MassSpectrum.Extract(Range).Max(p => p.Intensity) * 1.4;
            Model.Axes[0].Zoom(Range.Minimum -1, Range.Maximum + 1);
            Model.Axes[1].Zoom(0, maxIntensity);
        }
    }

    public class CustomComparer<T> : IEqualityComparer<T>
    {
        private readonly Func<T, object>[] propertySelectors;

        public CustomComparer(params Func<T, object>[] propertySelectors)
        {
            this.propertySelectors = propertySelectors;
        }

        public bool Equals(T x, T y)
        {
            if (x == null && y == null)
                return true;
            if (x == null || y == null)
                return false;

            foreach (var selector in propertySelectors)
            {
                if (!Equals(selector(x), selector(y)))
                    return false;
            }

            return true;
        }

        public int GetHashCode(T obj)
        {
            unchecked
            {
                int hash = 17;
                foreach (var selector in propertySelectors)
                {
                    hash = hash * 23 + (selector(obj)?.GetHashCode() ?? 0);
                }
                return hash;
            }
        }
    }
}

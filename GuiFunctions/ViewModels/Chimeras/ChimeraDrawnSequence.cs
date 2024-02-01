using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using EngineLayer;
using Proteomics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using FontFamily = System.Windows.Media.FontFamily;
using Path = System.IO.Path;
using iText.IO.Image;
using iText.Kernel.Pdf;

namespace GuiFunctions
{
    public class ChimeraDrawnSequence
    {

        public Canvas SequenceDrawingCanvas;
        public ChimeraGroupViewModel ChimeraGroupViewModel;
        private readonly int _numSequences;
        private static readonly int _yStep = 40;
        private static readonly int _canvasBuffer = 20;
        private ChimeraAnalysisTabViewModel? _parent;

        public ChimeraDrawnSequence(Canvas sequenceDrawingCanvas, ChimeraGroupViewModel chimeraGroupViewModel,
            ChimeraAnalysisTabViewModel parent = null)
        {
            SequenceDrawingCanvas = sequenceDrawingCanvas;
            ChimeraGroupViewModel = chimeraGroupViewModel;
            _numSequences = ChimeraGroupViewModel.ChimericPsms.Count;
            _parent = parent;

            DrawnSequence.ClearCanvas(SequenceDrawingCanvas);
            SetDrawingDimensions();
            DrawSequences();
        }

        private void SetDrawingDimensions()
        {
            var longestSequenceLength = ChimeraGroupViewModel.ChimericPsms.Max(psm => psm.Psm.BaseSeq.Length);
            SequenceDrawingCanvas.Width = (longestSequenceLength + 4) * MetaDrawSettings.AnnotatedSequenceTextSpacing + _canvasBuffer;
            SequenceDrawingCanvas.Height = _yStep * _numSequences + _canvasBuffer;
        }

        private void DrawSequences()
        {
            if (_parent is not null && _parent.GroupProteinsInSequenceAnnotation)
            {
                int index = 0;
                foreach (var baseSeqGroup in ChimeraGroupViewModel.ChimericPsms.GroupBy(p => p.Psm.BaseSeq))
                {
                    DrawBaseSequence(baseSeqGroup.First(), index);
                    foreach (var psm in baseSeqGroup)
                    {
                        AddModifications(psm, index);
                        AddMatchedIons(psm, index);
                    }

                    index++;
                }

                return;
            }


            int maxBaseSeqLength = ChimeraGroupViewModel.ChimericPsms.Max(p => p.Psm.BaseSeq.Length);
            for (var index = 0; index < ChimeraGroupViewModel.ChimericPsms.Count; index++)
            {
                var psm = ChimeraGroupViewModel.ChimericPsms[index];
                DrawBaseSequence(psm, index);
                AddModifications(psm, index);
                AddMatchedIons(psm, index);
                AddCircles(psm, index, maxBaseSeqLength);
            }
        }

        private void AddCircles(ChimericPsmModel psm, int row, int maxBaseSeqLength)
        {
            var color = DrawnSequence.ParseColorBrushFromOxyColor(psm.Color);
            DrawnSequence.DrawCircle(SequenceDrawingCanvas, new Point(GetX(maxBaseSeqLength + 1), GetY(row)), color);
            DrawnSequence.DrawCircle(SequenceDrawingCanvas, new Point(GetX(-2), GetY(row)), color);
        }

        private void DrawBaseSequence(ChimericPsmModel psm, int row)
        {
            var baseSeq = psm.Psm.BaseSeq.Split('|')[0];
            int index = 0;
            for (; index < baseSeq.Length; index++)
            {
                var x = GetX(index);
                var y = GetY(row);
                DrawnSequence.DrawText(SequenceDrawingCanvas, new Point(x, y), baseSeq[index].ToString(), Brushes.Black);
            }
        }

        private void AddModifications(ChimericPsmModel psm, int row)
        {
            var peptide = new PeptideWithSetModifications(psm.Psm.FullSequence.Split('|')[0], GlobalVariables.AllModsKnownDictionary);

            foreach (var mod in peptide.AllModsOneIsNterminus)
            {
                var x = GetX(mod.Key - 2);
                var y = GetY(row);
                var color = DrawnSequence.ParseColorBrushFromOxyColor(psm.Color);
                DrawnSequence.DrawCircle(SequenceDrawingCanvas, new Point(x,y), color);
            }
        }

        private void AddMatchedIons(ChimericPsmModel psm, int row)
        {
            _internalIndex = 0;
            Color color;
            bool drawInternal = _parent is not null && _parent.AnnotateInternalIonsInSequenceAnnotation;
            var internalCount = psm.Psm.MatchedIons.Count(p => p.NeutralTheoreticalProduct.SecondaryProductType != null);

            // Shared Ions
            if (ChimeraGroupViewModel.MatchedFragmentIonsByColor.ContainsKey(ChimeraSpectrumMatchPlot.MultipleProteinSharedColor))
            {
                foreach (var ion in ChimeraGroupViewModel
                             .MatchedFragmentIonsByColor[ChimeraSpectrumMatchPlot.MultipleProteinSharedColor]
                             .Select(p => p.Item1))
                {
                    if (!psm.Psm.MatchedIons.Contains(ion)) continue;
                    color = DrawnSequence.ParseColorFromOxyColor(ChimeraSpectrumMatchPlot.MultipleProteinSharedColor);
                    AddMatchedIon(ion, color, row, drawInternal, psm.Psm.BaseSeq.Length, internalCount);
                }
            }

            // Protein Shared Ions
            if (ChimeraGroupViewModel.MatchedFragmentIonsByColor.ContainsKey(psm.ProteinColor))
            {
                foreach (var ion in ChimeraGroupViewModel
                             .MatchedFragmentIonsByColor[psm.ProteinColor]
                             .Select(p => p.Item1))
                {
                    if (!psm.Psm.MatchedIons.Contains(ion)) continue;
                    color = DrawnSequence.ParseColorFromOxyColor(psm.ProteinColor);
                    AddMatchedIon(ion, color, row, drawInternal, psm.Psm.BaseSeq.Length, internalCount);
                }
            }

            // Unique Ions
            if (ChimeraGroupViewModel.MatchedFragmentIonsByColor.ContainsKey(psm.Color))
            {
                foreach (var ion in ChimeraGroupViewModel
                             .MatchedFragmentIonsByColor[psm.Color]
                             .Select(p => p.Item1))
                {
                    color = DrawnSequence.ParseColorFromOxyColor(psm.Color);
                    AddMatchedIon(ion, color, row, drawInternal, psm.Psm.BaseSeq.Length, internalCount);
                }
            }
        }

        private static int _internalIndex = 0;
        private void AddMatchedIon(MatchedFragmentIon ion, Color color, int row, bool drawInternal, int sequenceLength, int internalCount)
        {
            double x, y;
            var residueNum = ion.NeutralTheoreticalProduct.ProductType == ProductType.y
                ? sequenceLength - ion.NeutralTheoreticalProduct.FragmentNumber
                : ion.NeutralTheoreticalProduct.AminoAcidPosition;
            x = GetX(residueNum);
            y = GetY(row) + MetaDrawSettings.ProductTypeToYOffset[ion.NeutralTheoreticalProduct.ProductType];

            // is internal
            if (ion.NeutralTheoreticalProduct.SecondaryProductType != null)
            {
                if (drawInternal) // draw internals as a line
                {
                    double endX;
                    switch (ion.NeutralTheoreticalProduct.SecondaryProductType)
                    {
                        case ProductType.b:
                            residueNum = ion.NeutralTheoreticalProduct.SecondaryFragmentNumber;
                            endX = GetX(residueNum);
                            break;
                        case ProductType.y:
                            residueNum = sequenceLength - ion.NeutralTheoreticalProduct.SecondaryFragmentNumber;
                            endX = GetX(residueNum);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    var min = y + 5;
                    var max = GetY(row + 1) - 5;
                    var yStep = (max - min) * (_internalIndex / (double)internalCount);

                    var internalY = y + yStep;
                    DrawInternal(SequenceDrawingCanvas, x, endX, internalY, color);
                    _internalIndex++;
                }
            }
            else
            {
                if (ion.NeutralTheoreticalProduct.Terminus == FragmentationTerminus.C)
                {
                    DrawnSequence.DrawCTermIon(SequenceDrawingCanvas, new Point(x, y), color, "", 2);
                }
                else if (ion.NeutralTheoreticalProduct.Terminus == FragmentationTerminus.N)
                {
                    DrawnSequence.DrawNTermIon(SequenceDrawingCanvas, new Point(x, y), color, "", 2);
                }
            }
            
        }

        internal void Export(string path)
        {
            // change path to .png
            path = Path.ChangeExtension(path, "png");

            // convert canvas to bitmap
            Rect bounds = VisualTreeHelper.GetDescendantBounds(SequenceDrawingCanvas);
            double dpi = 96d;

            RenderTargetBitmap rtb = new(
                (int)bounds.Width, //width
                (int)bounds.Height, //height
                dpi, //dpi x
                dpi, //dpi y
                System.Windows.Media.PixelFormats.Default // pixelformat
            );

            DrawingVisual dv = new();
            using (DrawingContext dc = dv.RenderOpen())
            {
                VisualBrush vb = new(SequenceDrawingCanvas);
                dc.DrawRectangle(vb, null, new Rect(new System.Windows.Point(), bounds.Size));
            }

            rtb.Render(dv);

            // export
            using (FileStream stream = new(path, FileMode.Create))
            {
                PngBitmapEncoder encoder = new();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                encoder.Save(stream);
            }
        }


        internal static void DrawInternal(Canvas cav, double startX, double endX, double y, Color col)
        {
            Polyline bot = new Polyline();
            bot.Points = new PointCollection()
            {
                new Point(startX, y),
                new Point(endX, y),
            };
            bot.Stroke = new SolidColorBrush(col);
            bot.StrokeThickness = 1;
            cav.Children.Add(bot);
        }

        private static double GetY(int row)
        {
            return (row * _yStep) + 10;
        }

        private static double GetX(int residueIndex)
        {
            return (residueIndex + 1) * MetaDrawSettings.AnnotatedSequenceTextSpacing + 22;
        }

   
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;
using FontFamily = System.Windows.Media.FontFamily;

namespace GuiFunctions
{
    public class ChimeraDrawnSequence
    {

        public Canvas SequenceDrawingCanvas;
        public ChimeraGroupViewModel ChimeraGroupViewModel;
        private readonly int _numSequences;

        public ChimeraDrawnSequence(Canvas sequenceDrawingCanvas, ChimeraGroupViewModel chimeraGroupViewModel)
        {
            SequenceDrawingCanvas = sequenceDrawingCanvas;
            ChimeraGroupViewModel = chimeraGroupViewModel;
            _numSequences = ChimeraGroupViewModel.ChimericPsms.Count;

            DrawnSequence.ClearCanvas(SequenceDrawingCanvas);
            SetDrawingDimensions();
            DrawBaseSequences();
        }

        private void SetDrawingDimensions()
        {
            SequenceDrawingCanvas.Width = 600;
            SequenceDrawingCanvas.Height = 30 * _numSequences + 10;
        }

        private void DrawBaseSequences()
        {
            for (var index = 0; index < ChimeraGroupViewModel.ChimericPsms.Count; index++)
            {
                var psm = ChimeraGroupViewModel.ChimericPsms[index];
                DrawBaseSequence(psm, index);
            }
        }

        private void DrawBaseSequence(ChimericPsmModel psm, int row)
        {
            for (int i = 0; i < psm.Psm.BaseSeq.Length; i++)
            {
                double x = i * MetaDrawSettings.AnnotatedSequenceTextSpacing + 10;
                var y = (row * 30) + 10;
                DrawText(SequenceDrawingCanvas, new Point(x, y), psm.Psm.BaseSeq[i].ToString(), Brushes.Black);
                AddModifications();
                AddMatchedIons();
            }
        }

        private void AddModifications()
        {

        }

        private void AddMatchedIons()
        {

        }

        /// <summary>
        /// Create text blocks on canvas
        /// </summary>
        private static void DrawText(Canvas cav, Point loc, string txt, Brush clr)
        {
            TextBlock tb = new TextBlock();
            tb.Foreground = clr;
            tb.Text = txt;
            tb.Height = 30;
            tb.FontSize = 25;
            tb.FontWeight = System.Windows.FontWeights.Bold;
            tb.FontFamily = new FontFamily("Arial");
            tb.TextAlignment = TextAlignment.Center;
            tb.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            tb.Width = 24; // W (tryptophan) seems to be widest letter, make sure it fits if you're editing this

            Canvas.SetTop(tb, loc.Y);
            Canvas.SetLeft(tb, loc.X);
            Panel.SetZIndex(tb, 2); //lower priority
            cav.Children.Add(tb);
            cav.UpdateLayout();
        }
    }
}

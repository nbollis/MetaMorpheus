using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EngineLayer;
using GuiFunctions.MetaDraw.SpectrumMatch;
using iText.IO.Source;
using MassSpectrometry;
using Nett;
using OxyPlot;
using pepXML.Generated;
using TaskLayer;
using ImageFormat = System.Drawing.Imaging.ImageFormat;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace GuiFunctions
{
    public class ChimeraAnalysisTabViewModel : BaseViewModel
    {
        public ChimeraSpectrumMatchPlot ChimeraSpectrumMatchPlot { get; set; }
        public Ms1ChimeraPlot Ms1ChimeraPlot { get; set; }
        public List<ChimeraGroupViewModel> ChimeraGroupViewModels { get; set; }

        private ChimeraGroupViewModel _selectedChimeraGroup;
        public ChimeraGroupViewModel SelectedChimeraGroup
        {
            get => _selectedChimeraGroup;
            set
            {
                _selectedChimeraGroup = value;
                ChimeraLegendViewModel.ChimeraLegendItems = value.LegendItems;
                OnPropertyChanged(nameof(SelectedChimeraGroup));
            }
        }

        private ChimeraLegendViewModel _chimeraLegendViewModel;
        public ChimeraLegendViewModel ChimeraLegendViewModel
        {
            get => _chimeraLegendViewModel;
            set
            {
                _chimeraLegendViewModel = value;
                OnPropertyChanged(nameof(ChimeraLegendViewModel));
            }
        }

        private ChimeraDrawnSequence _chimeraDrawnSequence;
        public ChimeraDrawnSequence ChimeraDrawnSequence
        {
            get => _chimeraDrawnSequence;
            set
            {
                _chimeraDrawnSequence = value;
                OnPropertyChanged(nameof(ChimeraDrawnSequence));
            }
        }

        private bool _annotateInternalIonsInSequenceAnnotation;
        public bool AnnotateInternalIonsInSequenceAnnotation
        {
            get => _annotateInternalIonsInSequenceAnnotation;
            set
            {
                _annotateInternalIonsInSequenceAnnotation = value;
                OnPropertyChanged(nameof(AnnotateInternalIonsInSequenceAnnotation));
            }
        }

        private bool _groupProteinsInSequenceAnnotation;
        public bool GroupProteinsInSequenceAnnotation
        {
            get => _groupProteinsInSequenceAnnotation;
            set
            {
                _groupProteinsInSequenceAnnotation = value;
                OnPropertyChanged(nameof(GroupProteinsInSequenceAnnotation));
            }
        }

        public ChimeraAnalysisTabViewModel(List<PsmFromTsv> allPsms, Dictionary<string, MsDataFile> dataFiles)
        {
            ChimeraGroupViewModels = ConstructChimericPsms(allPsms, dataFiles)
                .OrderByDescending(p => p.Count)
                .ThenByDescending(p => p.ChimeraScore)
                .ToList();
            SelectedExportType = "Png";
            ExportTypes = new ObservableCollection<string>
            {
                "Pdf",
                "Png",
                "Svg"
            };
            OnPropertyChanged(nameof(ExportTypes));

            ChimeraLegendViewModel = new ChimeraLegendViewModel();
            ExportSequenceCoverageCommand = new RelayCommand(() => ExportSequenceCoverage());
            ExportMs1Command = new RelayCommand(() => ExportMs1Plot());
            ExportMs2Command = new RelayCommand(() => ExportMs2Plot());
            ExportLegendCommand = new DelegateCommand(ExportLegend);
            ExportBulkCommand = new DelegateCommand(ExportBulk2);
            ExportWootCommand = new RelayCommand(ExportWoot);

            new Thread(() => BackgroundLoader()) { IsBackground = true }.Start();
        }



        #region IO 

        private static string _directory = @"D:\Projects\SpectralAveraging\PaperTestOutputs\ChimeraImages";
        private string _exportType { get; set; }
        public string ExportType
        {
            get => _exportType;
            set
            {
                _exportType = value;
                OnPropertyChanged(nameof(ExportType));
            }
        }

        public ObservableCollection<string> ExportTypes { get; set; }

        private string _selectedExportType { get; set; }
        public string SelectedExportType
        {
            get => _selectedExportType;
            set
            {
                _selectedExportType = value;
                OnPropertyChanged(nameof(SelectedExportType));
            }
        }

        public ICommand ExportSequenceCoverageCommand { get; set; }
        private void ExportSequenceCoverage(string directory = null)
        {
            string path = Path.Combine(directory ?? _directory,
                               $"{SelectedChimeraGroup.FileNameWithoutExtension}_{SelectedChimeraGroup.Ms1Scan.OneBasedScanNumber}_{SelectedChimeraGroup.Ms2Scan.OneBasedScanNumber}_SequenceCoverage.{SelectedExportType.ToLower()}");
            ChimeraDrawnSequence.Export(path);
        }

        public ICommand ExportBulkCommand { get; set; }


        private void ExportBulk2(FrameworkElement element)
        {
            // initialize all 
            string path = Path.Combine(_directory,
                $"{SelectedChimeraGroup.FileNameWithoutExtension}_{SelectedChimeraGroup.Ms1Scan.OneBasedScanNumber}_{SelectedChimeraGroup.Ms2Scan.OneBasedScanNumber}_Combined.{SelectedExportType.ToLower()}");

            List<System.Drawing.Bitmap> bitmaps = new();
            List<Point> points = new();
            double dpi = 120d;
            var paperWidth = 8.5 * dpi;
            var paperHeight = 11 * dpi;

            // scale and rotate sequence annotation
            Rect annotationBounds = VisualTreeHelper.GetDescendantBounds(ChimeraDrawnSequence.SequenceDrawingCanvas);
            var scaleY = paperHeight / annotationBounds.Width;
            var annotationWidth = annotationBounds.Height * scaleY;

            var rtb = new RenderTargetBitmap((int)(paperHeight), (int)(paperWidth), dpi, dpi, PixelFormats.Default);
            var dv = new DrawingVisual();
            using (DrawingContext ctx = dv.RenderOpen())
            {
                ctx.PushTransform(new ScaleTransform(scaleY, scaleY));
                VisualBrush vb = new VisualBrush(ChimeraDrawnSequence.SequenceDrawingCanvas);
                ctx.DrawRectangle(vb, null, new Rect(new Point(), annotationBounds.Size));
            }
            rtb.Render(dv);

            var temp1 = Path.Combine(_directory, "temp1_120.png");
            using (var temp = new FileStream(temp1, FileMode.Create))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                encoder.Save(temp);
            }

            TransformedBitmap rotatedBitmap = new TransformedBitmap(rtb, new RotateTransform(270));
            var temp2 = Path.Combine(_directory, "temp2_120.png");
            using (var temp = new FileStream(temp2, FileMode.Create))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rotatedBitmap));
                encoder.Save(temp);
            }


            using (var memory = new MemoryStream())
            {
                BitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rotatedBitmap));
                encoder.Save(memory);
                bitmaps.Add(new System.Drawing.Bitmap(memory));
            }
            
            annotationWidth = bitmaps[0].Width;
            points.Add(new Point(0, 0));

            // legend
            Rect legendBounds = VisualTreeHelper.GetDescendantBounds(element);
            var scaleX = (paperWidth - annotationWidth) / legendBounds.Width;
            var legendHeight = legendBounds.Height * scaleX;
            rtb = new RenderTargetBitmap((int)(paperWidth - annotationWidth), (int)(legendHeight), dpi, dpi, PixelFormats.Default);
            dv = new DrawingVisual();
            using (DrawingContext ctx = dv.RenderOpen())
            {
                ctx.PushTransform(new ScaleTransform(scaleX, scaleX));
                VisualBrush vb = new VisualBrush(element);
                ctx.DrawRectangle(vb, null, new Rect(new Point(), legendBounds.Size));
            }
            rtb.Render(dv);
            using (var memory = new MemoryStream())
            {
                BitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                encoder.Save(memory);
                bitmaps.Add(new System.Drawing.Bitmap(memory));
            }
            points.Add(new Point(bitmaps[0].Width, paperHeight - legendHeight));

            // create temporary exports for spectra
            // give spectra the remaining space
            string tempDir = Path.Combine(_directory, "temp");
            if (!Directory.Exists(tempDir))
                Directory.CreateDirectory(tempDir);
            int remainingX = (int)(paperWidth - annotationWidth);
            double remainingY = paperHeight - legendHeight;
            int specHeight = (int)(remainingY / 2);

            var ms1TempPath = Path.Combine(tempDir, "ms1.png");
            Ms1ChimeraPlot.ExportToPng(ms1TempPath, remainingX, specHeight);
            bitmaps.Add(new Bitmap(ms1TempPath));
            points.Add(new Point(annotationWidth, 0));


            var ms2TempPath = Path.Combine(tempDir, "ms2.png");
            ChimeraSpectrumMatchPlot.ExportToPng(ms2TempPath, remainingX, specHeight);
            bitmaps.Add(new Bitmap(ms2TempPath));
            points.Add(new Point(annotationWidth, specHeight));

            var combinedBitmap = new System.Drawing.Bitmap((int)paperWidth, (int)paperHeight);
            using (var g = System.Drawing.Graphics.FromImage(combinedBitmap))
            {
                //g.ScaleTransform(scaleX, scaleY);
                g.Clear(System.Drawing.Color.White);
                for (int i = 0; i < bitmaps.Count; i++)
                {
                    g.DrawImage(bitmaps[i], (float)points[i].X, (float)points[i].Y);
                }
            }
            combinedBitmap.Save(path, ImageFormat.Png);

            // clean up
            bitmaps.ForEach(b => b.Dispose());
            Directory.Delete(tempDir, true);
        }


        private void ExportBulk(FrameworkElement element)
        {
            // initialize all 
            string path = Path.Combine(_directory,
                $"{SelectedChimeraGroup.FileNameWithoutExtension}_{SelectedChimeraGroup.Ms1Scan.OneBasedScanNumber}_{SelectedChimeraGroup.Ms2Scan.OneBasedScanNumber}_Combined.{SelectedExportType.ToLower()}");

            List<System.Drawing.Bitmap> bitmaps = new();
            List<Point> points = new();
            double dpi = 96d;

            // handle sequence annotation
            Rect bounds = VisualTreeHelper.GetDescendantBounds(ChimeraDrawnSequence.SequenceDrawingCanvas);
            RenderTargetBitmap rtb = new RenderTargetBitmap((int)(bounds.Width),
                               (int)(bounds.Height), dpi, dpi, PixelFormats.Default);
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext ctx = dv.RenderOpen())
            {
                VisualBrush vb = new VisualBrush(ChimeraDrawnSequence.SequenceDrawingCanvas);
                ctx.DrawRectangle(vb, null, new Rect(new Point(), bounds.Size));
            }
            rtb.Render(dv);
            
            TransformedBitmap rotatedBitmap = new TransformedBitmap(rtb, new RotateTransform(270));
            using (var memory = new MemoryStream())
            {
                BitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rotatedBitmap));
                encoder.Save(memory);
                bitmaps.Add(new System.Drawing.Bitmap(memory));
            }
            var sequenceHeight = (int)bounds.Height;
            points.Add(new Point(0, 0));
            double yMax = (int)bounds.Width;

            // handle legend
            bounds = VisualTreeHelper.GetDescendantBounds(element);
            rtb = new RenderTargetBitmap((int)(bounds.Width),
                (int)(bounds.Height),
                dpi,
                dpi,
                PixelFormats.Default);
            dv = new DrawingVisual();
            using (DrawingContext ctx = dv.RenderOpen())
            {
                VisualBrush vb = new VisualBrush(element);
                ctx.DrawRectangle(vb, null, new Rect(new Point(), bounds.Size));
            }
            rtb.Render(dv);
            using (var memory = new MemoryStream())
            {
                BitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                encoder.Save(memory);
                bitmaps.Add(new System.Drawing.Bitmap(memory));
            }
            var legendHeight = (int)bounds.Height;
            points.Add(new Point(sequenceHeight, yMax - legendHeight));
            double xMax = (int)bounds.Width + sequenceHeight;

            // create temporary exports for spectra
            // give spectra the remaining space
            string tempDir = Path.Combine(_directory, "temp");
            if (!Directory.Exists(tempDir))
                Directory.CreateDirectory(tempDir);
            int remainingX = (int)(xMax - sequenceHeight);
            double remainingY = yMax - legendHeight;
            int specHeight = (int)(remainingY / 2);

            var ms1TempPath = Path.Combine(tempDir, "ms1.png");
            Ms1ChimeraPlot.ExportToPng(ms1TempPath, remainingX, specHeight);
            bitmaps.Add(new Bitmap(ms1TempPath));
            points.Add(new Point(sequenceHeight, 0));


            var ms2TempPath = Path.Combine(tempDir, "ms2.png");
            ChimeraSpectrumMatchPlot.ExportToPng(ms2TempPath, remainingX, specHeight);
            bitmaps.Add(new Bitmap(ms2TempPath));
            points.Add(new Point(sequenceHeight, specHeight));

            //  transform to paper size, combine, and export
            var paperWidth = 8.5 * dpi;
            var paperHeight = 11 * dpi;
            float scaleX = (float)(paperWidth / xMax);
            float scaleY = (float)(paperHeight / yMax);

            var combinedBitmap = new System.Drawing.Bitmap((int)paperWidth, (int)paperHeight);
            using (var g = System.Drawing.Graphics.FromImage(combinedBitmap))
            {
                //g.ScaleTransform(scaleX, scaleY);
                g.Clear(System.Drawing.Color.White);
                for (int i = 0; i < bitmaps.Count; i++)
                {
                    g.DrawImage(bitmaps[i], (float)points[i].X, (float)points[i].Y);
                }
            }
            combinedBitmap.Save(path, ImageFormat.Png);

            // clean up
            bitmaps.ForEach(b => b.Dispose());
            Directory.Delete(tempDir, true);
        }

       

        public ICommand ExportMs1Command { get; set; }
        private void ExportMs1Plot(string directory = null)
        {
            string path = Path.Combine(directory ?? _directory,
                $"{SelectedChimeraGroup.FileNameWithoutExtension}_{SelectedChimeraGroup.Ms1Scan.OneBasedScanNumber}_{SelectedChimeraGroup.Ms2Scan.OneBasedScanNumber}_MS1.{SelectedExportType.ToLower()}");
            switch (SelectedExportType)
            {
                case "Pdf":
                    Ms1ChimeraPlot.ExportToPdf(path, (int)Ms1ChimeraPlot.Model.Width, (int)Ms1ChimeraPlot.Model.Height);
                    break;
                case "Png":
                    Ms1ChimeraPlot.ExportToPng(path, (int)Ms1ChimeraPlot.Model.Width, (int)Ms1ChimeraPlot.Model.Height);
                    break;
                case "Svg":
                    Ms1ChimeraPlot.ExportToSvg(path, (int)Ms1ChimeraPlot.Model.Width, (int)Ms1ChimeraPlot.Model.Height);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public ICommand ExportMs2Command { get; set; }
        private void ExportMs2Plot(string directory = null)
        {
            string path = Path.Combine(directory ?? _directory,
                $"{SelectedChimeraGroup.FileNameWithoutExtension}_{SelectedChimeraGroup.Ms1Scan.OneBasedScanNumber}_{SelectedChimeraGroup.Ms2Scan.OneBasedScanNumber}_MS2.{SelectedExportType.ToLower()}");
            switch (SelectedExportType)
            {
                case "Pdf":
                    ChimeraSpectrumMatchPlot.ExportToPdf(path, (int)ChimeraSpectrumMatchPlot.Model.Width, (int)ChimeraSpectrumMatchPlot.Model.Height);
                    break;
                case "Png":
                    ChimeraSpectrumMatchPlot.ExportToPng(path, (int)ChimeraSpectrumMatchPlot.Model.Width, (int)ChimeraSpectrumMatchPlot.Model.Height);
                    break;
                case "Svg":
                    ChimeraSpectrumMatchPlot.ExportToSvg(path, (int)ChimeraSpectrumMatchPlot.Model.Width, (int)ChimeraSpectrumMatchPlot.Model.Height);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public ICommand ExportWootCommand { get; set; }
        private void ExportWoot()
        {
            string directoryPath = Path.Combine(_directory, "ForWout");
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);
            ExportMs1Plot(directoryPath);

            string tsvPath = Path.Combine(directoryPath,
                               $"TopDownChimeraExamples.tsv");
            string tsvHeader = "Fraction\tMs1 Scan Number\tMS2 Scan Number\tIsolation Min\tIsolation Max\tPrecursor mzs\tPrecursor Intensities\tProteinName\tFullSequence";
            
            if (!File.Exists(tsvPath))
            {
                using var sw = new StreamWriter(File.Create(tsvPath));
                sw.WriteLine(tsvHeader);
                foreach (var line in SelectedChimeraGroup.ChimericPsms
                             .Select(p =>
                                 $"{p.Psm.FileNameWithoutExtension}\t{p.Psm.PrecursorScanNum}\t{p.Psm.Ms2ScanNumber}\t{SelectedChimeraGroup.Ms2Scan.IsolationRange.Minimum}\t{SelectedChimeraGroup.Ms2Scan.IsolationRange.Maximum}" +
                                 $"\t{string.Join(',', p.Ms2Scan.PrecursorEnvelope.Peaks.Select(m => m.mz))}\t{string.Join(',', p.Ms2Scan.PrecursorEnvelope.Peaks.Select(m => m.intensity))}" +
                                 $"\t{p.Psm.ProteinName}\t{p.Psm.FullSequence.Split('|')[0]}"))
                {
                    sw.WriteLine(line);
                }
                sw.Dispose();
            }
            else
            {
                var lines = SelectedChimeraGroup.ChimericPsms
                             .Select(p =>
                                 $"{p.Psm.FileNameWithoutExtension}\t{p.Psm.PrecursorScanNum}\t{p.Psm.Ms2ScanNumber}\t{SelectedChimeraGroup.Ms2Scan.IsolationRange.Minimum}\t{SelectedChimeraGroup.Ms2Scan.IsolationRange.Maximum}" +
                                 $"\t{string.Join(',', p.Ms2Scan.PrecursorEnvelope.Peaks.Select(m => m.mz))}\t{string.Join(',', p.Ms2Scan.PrecursorEnvelope.Peaks.Select(m => m.intensity))}" +
                                 $"\t{p.Psm.ProteinName}\t{p.Psm.FullSequence.Split('|')[0]}\t");
                
                    File.AppendAllLines(tsvPath, lines);
            }
        }

        public ICommand ExportLegendCommand { get; set; }
        private void ExportLegend(FrameworkElement element)
        {
            string path = Path.Combine( _directory,
                               $"{SelectedChimeraGroup.FileNameWithoutExtension}_{SelectedChimeraGroup.Ms1Scan.OneBasedScanNumber}_{SelectedChimeraGroup.Ms2Scan.OneBasedScanNumber}_Legend.{SelectedExportType.ToLower()}");

            var bounds = VisualTreeHelper.GetDescendantBounds(element);
            double dpi = 96d;

            RenderTargetBitmap rtb = new RenderTargetBitmap((int)(bounds.Width),
                (int)(bounds.Height),
                dpi,
                dpi,
                PixelFormats.Pbgra32);

            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext ctx = dv.RenderOpen())
            {
                VisualBrush vb = new VisualBrush(element);
                ctx.DrawRectangle(vb, null, new Rect(new Point(), bounds.Size));
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

        #endregion


        private async Task BackgroundLoader()
        {
            foreach (var chimeraGroup in ChimeraGroupViewModels)
            {
                var temp = chimeraGroup.PrecursorIonsByColor;
            }
        }

        private IEnumerable<ChimeraGroupViewModel> ConstructChimericPsms(List<PsmFromTsv> psms, Dictionary<string, MsDataFile> dataFiles)
        {
            //// TODO: Delet this when done with stuffs
            //int[] acceptableScans = new[] { 2461, 2477, 2513, 2984, 3089, 2367, 2227, 2477, 2461, 2477, 2516, 3008 };

            foreach (var group in psms.Where(p => p.QValue <= 0.01 && p.DecoyContamTarget == "T")
                         .GroupBy(p => p, ChimeraComparer)
                         .Where(p => p.Count() >= MetaDrawSettings.MinChimera /*|| acceptableScans.Any(m => p.First().PrecursorScanNum == m)*/)
                         .OrderByDescending(p => p.Count()))
            {
                // get the scan
                if (!dataFiles.TryGetValue(group.First().FileNameWithoutExtension, out MsDataFile spectraFile))
                    continue;

                var ms1Scan = spectraFile.GetOneBasedScanFromDynamicConnection(group.First().PrecursorScanNum);
                var ms2Scan = spectraFile.GetOneBasedScanFromDynamicConnection(group.First().Ms2ScanNumber);

                if (ms1Scan == null || ms2Scan == null)
                    continue;
                var groupVm = new ChimeraGroupViewModel(ms2Scan, ms1Scan, group.OrderBy(p => p.PrecursorMz).ToList());
                if (groupVm.ChimericPsms.Count > 0)
                    yield return groupVm;
            }
        }


        #region Static

        public static Func<PsmFromTsv, object>[] ChimeraSelector =
        {
            psm => psm.PrecursorScanNum,
            psm => psm.Ms2ScanNumber,
            psm => psm.FileNameWithoutExtension.Replace("-averaged", "")
        };
        public static CustomComparer<PsmFromTsv> ChimeraComparer = new CustomComparer<PsmFromTsv>(ChimeraSelector);

        private static CommonParameters _commonParameters { get; set; }
        public static CommonParameters CommonParameters => _commonParameters ?? Toml
            .ReadFile<SearchTask>(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ViewModels", "Chimeras", "AveragingPaperSearchTask.toml"),
                //@"C:\Users\nboll\OneDrive\SupplementalFile4_Task4-SearchTaskconfig.toml",
                //@"C:\Users\Nic\OneDrive - UW-Madison\AUSTIN V CARR - AUSTIN V CARR's files\SpectralAveragingPaper\Supplemental Information\Tasks\SupplementalFile4_Task4-SearchTaskconfig.toml",
                MetaMorpheusTask.tomlConfig).CommonParameters;

        #endregion
    }

    
}

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
using OxyPlot.Wpf;
using Proteomics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using TaskLayer;

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
            ZoomAxes();
            RefreshChart();
        }


        private void AnnotateChimericPeaks(ChimeraGroupViewModel chimeraGroupVm)
        {
            foreach (var ionGroup in chimeraGroupVm.PrecursorIonsByColor)
            {
                var color = ionGroup.Key;
                ionGroup.Value.ForEach(p => AnnotatePeak(p, false, false, color));
            }
        }


        public void ZoomAxes()
        {
            var maxIntensity = ChimeraGroup.Ms1Scan.MassSpectrum.Extract(Range).Max(p => p.Intensity) * 1.2;
            Model.Axes[0].Zoom(Range.Minimum, Range.Maximum);
            Model.Axes[1].Zoom(0, maxIntensity);
        }
    }




    public class ChimeraGroup
    {
        public string DataFile { get; set; }
        public int OneBasedPrecursorScanNumber { get; set; }
        public int Ms2ScanNumber { get; set; }
        public int Count => Psms.Count;
        internal Dictionary<string, List<PsmFromTsv>> PsmsByProteinDictionary { get; set; }
        internal List<PsmFromTsv> Psms { get; set; }
        public Dictionary<PsmFromTsv, OxyColor> PsmToColorDictionary { get; set; }
        public Dictionary<PsmFromTsv, Ms2ScanWithSpecificMass> PsmToMs2ScanWithSpecificMassesDictionary { get; set; }
        public Ms2ScanWithSpecificMass Ms2Scan { get; private set; }

        public ChimeraGroup(List<PsmFromTsv> psms)
        {
            if (psms.Select(p => (p.PrecursorScanNum, p.Ms2ScanNumber, p.FileNameWithoutExtension)).Distinct()
                    .Count() != 1)
                throw new ArgumentException("Not a chimeric group of psms");


            DataFile = psms.First().FileNameWithoutExtension;
            OneBasedPrecursorScanNumber = psms.First().PrecursorScanNum;
            Ms2ScanNumber = psms.First().Ms2ScanNumber;
            Psms = psms;
            PsmsByProteinDictionary = psms.GroupBy(p => p.ProteinAccession)
                .ToDictionary(p => p.Key, p => p.ToList());
            PsmToMs2ScanWithSpecificMassesDictionary = new Dictionary<PsmFromTsv, Ms2ScanWithSpecificMass>();
            SetColorDictionary();
        }

        private void SetColorDictionary()
        {
            PsmToColorDictionary = new Dictionary<PsmFromTsv, OxyColor>();
            var ColorByProteinDictionary = ChimeraSpectrumMatchPlot.ColorByProteinDictionary;
            Queue<OxyColor> overflowColors = ChimeraSpectrumMatchPlot.OverflowColors;

            int proteinIndex = 0;
            foreach (var protein in PsmsByProteinDictionary)
            {
                for (int i = 0; i < protein.Value.Count; i++)
                {
                    // more proteins than protein programmed colors
                    if (proteinIndex >= ColorByProteinDictionary.Keys.Count)
                    {
                        proteinIndex = 0;
                    }

                    // more proteoforms than programmed colors
                    if (i + 1 >= ColorByProteinDictionary[proteinIndex].Count)
                    {
                        PsmToColorDictionary.Add(protein.Value[i], overflowColors.Dequeue());
                    }
                    else
                    {
                        PsmToColorDictionary.Add(protein.Value[i], ColorByProteinDictionary[proteinIndex][i + 1]);
                    }
                }

                proteinIndex++;
            }
        }

        public bool IsGroup(PsmFromTsv psm)
        {
            return psm.FileNameWithoutExtension.Equals(DataFile) &&
                   psm.PrecursorScanNum.Equals(OneBasedPrecursorScanNumber) &&
                   psm.Ms2ScanNumber.Equals(Ms2ScanNumber);
        }

        public override string ToString()
        {
            return $"{OneBasedPrecursorScanNumber},{Ms2ScanNumber},{Count},{DataFile}";
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

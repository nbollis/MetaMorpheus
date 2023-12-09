using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using OxyPlot;

namespace GuiFunctions
{
    public class ChimericPsmModel
    {
        public PsmFromTsv Psm { get; set; }
        public Ms2ScanWithSpecificMass Ms2Scan { get; set; }
        public OxyColor Color { get; set; }
        public OxyColor ProteinColor { get; set; }

        public ChimericPsmModel(PsmFromTsv psm, Ms2ScanWithSpecificMass scan, OxyColor color, OxyColor proteinColor)
        {
            Psm = psm;
            Ms2Scan = scan;
            Color = color;
            ProteinColor = proteinColor;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using MzLibUtil;
using OxyPlot;
using Proteomics;

namespace GuiFunctions
{
    public class ChimericPsmModel
    {
        public PsmFromTsv Psm { get; set; }
        public Ms2ScanWithSpecificMass Ms2Scan { get; set; }
        public OxyColor Color { get; set; }
        public OxyColor ProteinColor { get; set; }
        public string ModString { get; set; }
        public string ProteinName => Psm.ProteinName;
        public string MonoMass => Psm.PeptideMonoMass;
        public string Score => Psm.Score.ToString("F2");
        public string QValue => Psm.QValue.ToString("F2");
        public string PrecursorMz => Psm.PrecursorMz.ToString("F2");

        public ChimericPsmModel(PsmFromTsv psm, Ms2ScanWithSpecificMass scan, OxyColor color, OxyColor proteinColor)
        {
            Psm = psm;
            Ms2Scan = scan;
            Color = color;
            ProteinColor = proteinColor;
            ModString = GetModsStringFromPsm(GlobalVariables.AllModsKnownDictionary);
        }


        private string GetModsStringFromPsm(Dictionary<string, Modification> idToMod)
        {
            var _allModsOneIsNterminus = new Dictionary<int, Modification>();
            int startIndex = 0;
            int key1 = 1;
            bool flag = false;
            int num = 0;
            for (int index = 0; index < Psm.FullSequence.Length; ++index)
            {
                switch (Psm.FullSequence[index])
                {
                    case '[':
                        flag = true;
                        if (num == 0)
                            startIndex = index + 1;
                        ++num;
                        break;
                    case ']':
                        --num;
                        if (num == 0)
                        {
                            string key2;
                            try
                            {
                                string str = Psm.FullSequence.Substring(startIndex, index - startIndex);
                                int length = str.IndexOf(':');
                                str.Substring(0, length);
                                key2 = str.Substring(length + 1, str.Length - length - 1);
                            }
                            catch (Exception ex)
                            {
                                throw new MzLibException("Error while trying to parse string into peptide: " + ex.Message);
                            }
                            Modification modification;
                            if (!idToMod.TryGetValue(key2, out modification))
                                throw new MzLibException("Could not find modification while reading string: " + Psm.FullSequence);
                            if (modification.LocationRestriction.Contains("C-terminal.") && index == Psm.FullSequence.Length - 1)
                                key1 = Psm.BaseSeq.Length + 2;
                            _allModsOneIsNterminus.Add(key1, modification);
                            flag = false;
                            break;
                        }
                        break;
                    default:
                        if (!flag)
                        {
                            ++key1;
                            break;
                        }
                        break;
                }
            }
            return string.Join(", ",
                _allModsOneIsNterminus.Select(p => p.Key + " - " + p.Value.IdWithMotif));
        }
    }
}

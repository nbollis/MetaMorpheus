using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using EngineLayer;
using FlashLFQ;
using Proteomics;
using Proteomics.ProteolyticDigestion;
using TopDownProteomics;

namespace GuiFunctions
{
    public class UwuVM : BaseViewModel
    {
        private string proteinSequence;
        private PeptideWithSetModifications peptideWithSetMods;
        private double massDif;
        private double targetMass;
        private bool truncations;
        private bool splices;
        private double minLength;

        public string ProteinSequence
        {
            get => proteinSequence;
            set { proteinSequence = value; OnPropertyChanged(nameof(ProteinSequence)); }
        }

        public PeptideWithSetModifications PeptideWithSetMods
        {
            get => peptideWithSetMods;
            set
            {
                peptideWithSetMods = value; 
                OnPropertyChanged(nameof(PeptideWithSetMods));
                OnPropertyChanged(nameof(MonoIsotopicMass));
                OnPropertyChanged(nameof(MostAbundantMass));
            }
        }

        public double MonoIsotopicMass => PeptideWithSetMods?.MonoisotopicMass ?? 0.0;
        public double MostAbundantMass => PeptideWithSetMods?.MostAbundantMonoisotopicMass ?? 0.0;

        public double MassDifference
        {
            get => massDif;
            set { massDif = value; OnPropertyChanged(nameof(MassDifference)); }
        }

        public double TargetMass
        {
            get => targetMass;
            set { targetMass = value; OnPropertyChanged(nameof(TargetMass)); }
        }

        public bool Truncations
        {
            get => truncations;
            set { truncations = value; OnPropertyChanged(nameof(Truncations)); }
        }

        public bool SpliceVariants
        {
            get => splices;
            set { splices = value; OnPropertyChanged(nameof(SpliceVariants)); }
        }

        public double MinProteinLength
        {
            get => minLength;
            set { minLength = value; OnPropertyChanged(nameof(MinProteinLength)); }
        }

        public UwuVM()
        {
            massDif = 1;
        }

        

        public void CalculateProteinInfo()
        {
            if (ProteinSequence == null)
                return;
            PeptideWithSetMods = new(ProteinSequence, GlobalVariables.AllModsKnownDictionary);
        }

        public void RunAnalysis()
        {
            // generate all possible strings
            List<string> allPossibleStrings = new();



            // group by length



            // eliminate groups based upon difference from target



            // spit out what remains
        }

        #region String Generation

        public IEnumerable<string> GetTruncySequences()
        {
            List<string> list = new();
            for (int i = 0; i < PeptideWithSetMods.BaseSequence.Length; i++)
            {
                yield return PeptideWithSetMods.BaseSequence.Substring(0, PeptideWithSetMods.BaseSequence.Length - i);
            }
            for (int i = 1; i < PeptideWithSetMods.BaseSequence.Length; i++)
            {
                yield return PeptideWithSetMods.BaseSequence.Substring(i, PeptideWithSetMods.BaseSequence.Length - i);
            }
        }

        public IEnumerable<string> GetSplicedSequences(string seqToSplice)
        {
            List<string> strings = new();


            int maxToRemove = seqToSplice.Length - 2;
            for (int toRemove = 1; toRemove < maxToRemove; toRemove++)
            {
                for (int start = 1; start < seqToSplice.Length - 1; start++)
                {
                    // if segment will be longer than ac
                    if (start + toRemove >= seqToSplice.Length)
                        break;

                    //TODO: Fix this
                    if (start + toRemove < MinProteinLength)
                        break;


                    var result = seqToSplice.Substring(0, start ) +
                                 seqToSplice.Substring(start+toRemove, seqToSplice.Length - start - toRemove);

                    strings.Add(result);
                }
            }

            return strings;
        }

        #endregion
    }

    public class UwuModel : UwuVM
    {
        public static UwuModel Instance => new UwuModel();

        private UwuModel()
        {
            ProteinSequence = "PEPTIDE";
            CalculateProteinInfo();
        }

    }
}

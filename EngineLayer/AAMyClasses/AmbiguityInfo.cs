using Proteomics.ProteolyticDigestion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EngineLayer
{
    public class AmbiguityInfo
    {
        public bool PTMLocalization { get; set; }
        public bool PTMID { get; set; }
        public bool SeqDefined { get; set; }
        public bool GeneID { get; set; }
        public string AmbigType { get; set; }
        public bool[] AllValues
        {
            get { return new bool[] { PTMLocalization, PTMID, SeqDefined, GeneID }; }
        }

        public static void SetAmbiguityInfo(PsmFromTsv psm)
        {
            //separate delimited input
            string[] sequences = psm.FullSequence.Split('|');
            string[] genes = psm.GeneName.Split('|');


            //determine sequence ambiguity
            string firstBaseSequence = PeptideWithSetModifications.GetBaseSequenceFromFullSequence(sequences[0]).ToUpper(); //get first sequence with modifications removed
            bool sequenceIdentified = !SequenceContainsUnknownAminoAcids(firstBaseSequence); //check if there are any ambiguous amino acids (i.e. B, J, X, Z)
            //for every other sequence reported
            if (sequenceIdentified) //if there weren't any unknown amino acids reported.
            {
                for (int i = 1; i < sequences.Length; i++)
                {
                    //if the unmodified sequences don't match, then there's sequence ambiguity
                    if (!firstBaseSequence.Equals(PeptideWithSetModifications.GetBaseSequenceFromFullSequence(sequences[i]).ToUpper()))
                    {
                        sequenceIdentified = false;
                        break;
                    }
                }
            }


            //determine PTM localization and identification
            List<(int index, string ptm)> firstPTMsSortedByIndex = GetPTMs(sequences[0]); //get ptms from the first sequence reported
            List<string> firstPTMsSortedByPTM = firstPTMsSortedByIndex.Select(x => x.ptm).OrderBy(x => x).ToList(); //sort ptms alphabetically
            //check if there are unknown mass shifts
            bool ptmsIdentified = !PtmsContainUnknownMassShifts(firstPTMsSortedByPTM);
            bool ptmsLocalized = true; //assume these are localized unless we determine otherwise
            //for every other sequence reported
            for (int seqIndex = 1; seqIndex < sequences.Length; seqIndex++)
            {
                List<(int index, string ptm)> currentPTMsSortedByIndex = GetPTMs(sequences[seqIndex]); //get ptms from this sequence
                List<string> currentPTMsSortedByPTM = currentPTMsSortedByIndex.Select(x => x.ptm).OrderBy(x => x).ToList(); //sort ptms alphabetically

                //are number of PTMs the same?
                if (firstPTMsSortedByIndex.Count == currentPTMsSortedByIndex.Count)
                {
                    //check localization (are indexes conserved?)
                    for (int i = 0; i < firstPTMsSortedByIndex.Count; i++)
                    {
                        if (firstPTMsSortedByIndex[i].index != currentPTMsSortedByIndex[i].index)
                        {
                            ptmsLocalized = false;
                            break;
                        }
                    }
                    //check PTM identification
                    for (int i = 0; i < firstPTMsSortedByPTM.Count; i++)
                    {
                        if (!firstPTMsSortedByPTM[i].Equals(currentPTMsSortedByPTM[i]))
                        {
                            ptmsIdentified = false;
                            break;
                        }
                    }
                }
                else
                {
                    ptmsIdentified = false;
                    ptmsLocalized = false;
                }
            }
            //handle an edge case where two PTMs are identified and localized to two residues, but it's unclear which PTM is localized to which residue.
            if (ptmsIdentified && ptmsLocalized)
            {
                for (int seqIndex = 1; seqIndex < sequences.Length; seqIndex++)
                {
                    List<(int index, string ptm)> currentPTMsSortedByIndex = GetPTMs(sequences[seqIndex]); //get ptms from this sequence
                    //check that the mods are in the same position
                    for (int ptmIndex = 0; ptmIndex < currentPTMsSortedByIndex.Count; ptmIndex++)
                    {
                        if (!firstPTMsSortedByIndex[ptmIndex].ptm.Equals(currentPTMsSortedByIndex[ptmIndex]))
                        {
                            ptmsLocalized = false;
                            break;
                        }
                    }
                }
            }

            //determine gene ambiguity
            bool geneIdentified = genes.Length == 1;

            psm.AmbiguityInfo.PTMLocalization = ptmsLocalized;
            psm.AmbiguityInfo.PTMID = ptmsIdentified;
            psm.AmbiguityInfo.SeqDefined = sequenceIdentified;
            psm.AmbiguityInfo.GeneID = geneIdentified;

            SetAmbiguityType(psm);

        }

        private static void SetAmbiguityType(PsmFromTsv psm)
        {
            if (psm.AmbiguityInfo.AllValues.Where(p => p == true).Count() == 4)
                psm.AmbiguityInfo.AmbigType = "1";

            else if (psm.AmbiguityInfo.AllValues.Where(p => p == true).Count() == 3)
            {
                if (!psm.AmbiguityInfo.PTMLocalization)
                    psm.AmbiguityInfo.AmbigType = "2A";
                if (!psm.AmbiguityInfo.PTMID)
                    psm.AmbiguityInfo.AmbigType = "2B";
                if (!psm.AmbiguityInfo.SeqDefined)
                    psm.AmbiguityInfo.AmbigType = "2C";
                if (!psm.AmbiguityInfo.GeneID)
                    psm.AmbiguityInfo.AmbigType = "2D";
            }
            else if (psm.AmbiguityInfo.AllValues.Where(p => p == true).Count() == 2)
            {
                if (!psm.AmbiguityInfo.PTMLocalization && !psm.AmbiguityInfo.PTMID)
                    psm.AmbiguityInfo.AmbigType = "3AB";
                if (!psm.AmbiguityInfo.PTMLocalization && !psm.AmbiguityInfo.SeqDefined)
                    psm.AmbiguityInfo.AmbigType = "3AC";
                if (!psm.AmbiguityInfo.PTMLocalization && !psm.AmbiguityInfo.GeneID)
                    psm.AmbiguityInfo.AmbigType = "3AD";
                if (!psm.AmbiguityInfo.PTMID && !psm.AmbiguityInfo.SeqDefined)
                    psm.AmbiguityInfo.AmbigType = "3BC";
                if (!psm.AmbiguityInfo.PTMID && !psm.AmbiguityInfo.GeneID)
                    psm.AmbiguityInfo.AmbigType = "3BD";
                if (!psm.AmbiguityInfo.SeqDefined && !psm.AmbiguityInfo.GeneID)
                    psm.AmbiguityInfo.AmbigType = "3CD";
            }
            else if (psm.AmbiguityInfo.AllValues.Where(p => p == true).Count() == 1)
            {
                if (psm.AmbiguityInfo.PTMLocalization)
                    psm.AmbiguityInfo.AmbigType = "4A";
                if (psm.AmbiguityInfo.PTMID)
                    psm.AmbiguityInfo.AmbigType = "4B";
                if (psm.AmbiguityInfo.SeqDefined)
                    psm.AmbiguityInfo.AmbigType = "4C";
                if (psm.AmbiguityInfo.GeneID)
                    psm.AmbiguityInfo.AmbigType = "4D";
            }
            else if (psm.AmbiguityInfo.AllValues.Where(p => p == true).Count() == 0)
                psm.AmbiguityInfo.AmbigType = "5";
        }

        private static bool SequenceContainsUnknownAminoAcids(string baseSequence)
        {
            char[] array = new char[4] { 'B', 'J', 'X', 'Z' };
            char[] array2 = array;
            foreach (char value in array2)
            {
                if (baseSequence.Contains(value))
                {
                    return true;
                }
            }

            return false;
        }
        private static List<(int, string)> GetPTMs(string fullSequence)
        {
            List<(int, string)> list = new List<(int, string)>();
            StringBuilder stringBuilder = new StringBuilder();
            int num = 0;
            int num2 = 0;
            foreach (char c in fullSequence)
            {
                if (c == ']')
                {
                    num2--;
                    if (num2 == 0)
                    {
                        num--;
                        list.Add((num, stringBuilder.ToString()));
                        stringBuilder.Clear();
                    }
                }
                else
                {
                    if (num2 > 0)
                    {
                        stringBuilder.Append(c);
                    }
                    else
                    {
                        num++;
                    }

                    if (c == '[')
                    {
                        num2++;
                    }
                }
            }

            return list;
        }
        private static bool PtmsContainUnknownMassShifts(List<string> ptms)
        {
            foreach (string ptm in ptms)
            {
                if (ptm.Length > 1 && double.TryParse(ptm.Substring(1), out var _))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

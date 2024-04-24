using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using Proteomics;
using Test.ChimeraPaper.ResultFiles;
using Test.RyanJulain;

namespace Test.AveragingPaper
{
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
                if (selector.Target is IEnumerable<double> enumerable)
                {
                    if (!enumerable.SequenceEqual((IEnumerable<double>)selector(y)))
                        return false;
                }
                else if (!Equals(selector(x), selector(y)))
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


        #region Custom Implementations

        // chimeras
        public static CustomComparer<PsmFromTsv> ChimeraComparer =>
            new(psm => psm.PrecursorScanNum, psm => psm.Ms2ScanNumber,
                psm => psm.FileNameWithoutExtension.Replace("-calib", "").Replace("-averaged", ""));

        public static CustomComparer<MsFraggerPsm> MsFraggerChimeraComparer =>
            new(psm => psm.OneBasedScanNumber, psm => psm.FileNameWithoutExtension);


        private static Func<MsFraggerPeptide, object>[] MsFraggerPeptideDistinctSelector =
        {
            peptide => peptide.BaseSequence,
            peptide => peptide.ProteinAccession,
            peptide => peptide.AssignedModifications.Length,
            peptide => peptide.AssignedModifications.FirstOrDefault(),
            peptide => peptide.AssignedModifications.LastOrDefault(),
            peptide => peptide.NextAminoAcid,
            peptide => peptide.PreviousAminoAcid,
        };

        public static CustomComparer<MsFraggerPeptide> MsFraggerPeptideDistinctComparer =>
            new(MsFraggerPeptideDistinctSelector);

        public static CustomComparer<MsPathFinderTResult> MsPathFinderTChimeraComparer =>
            new CustomComparer<MsPathFinderTResult>(
                prsm => prsm.OneBasedScanNumber, 
                prsm => prsm.FileNameWithoutExtension);



        // Radical Fragmentation
        private static Func<PrecursorFragmentMassSet, object>[] PrecursorFragmentSetSelector =
        {
            protein => protein.Accession,
            protein => protein.PrecursorMass,
            protein => protein.FragmentCount,
            protein => protein.FragmentMasses.FirstOrDefault(),
            protein => protein.FragmentMasses.LastOrDefault(),
            protein => protein.FragmentMasses[protein.FragmentMasses.Count / 2],
            protein => protein.FragmentMasses[protein.FragmentMasses.Count / 3],
            protein => protein.FragmentMasses[protein.FragmentMasses.Count / 4],
            protein => protein.FullSequence
        };
        
        public static CustomComparer<PrecursorFragmentMassSet> PrecursorFragmentMassComparer =>
            new(PrecursorFragmentSetSelector);

        public static CustomComparer<PrecursorFragmentMassSet> LevelOneComparer => new
        (
            protein => protein.Accession,
            protein => protein.PrecursorMass,
            protein => protein.FragmentCount,
            protein => protein.FullSequence,
            protein => protein.FragmentMasses.FirstOrDefault(),
            protein => protein.FragmentMasses.LastOrDefault(),
            protein => protein.FragmentMasses[protein.FragmentMasses.Count / 2],
            protein => protein.FragmentMasses[protein.FragmentMasses.Count / 3],
            protein => protein.FragmentMasses[protein.FragmentMasses.Count / 4]
        );

        public static CustomComparer<PrecursorFragmentMassSet> LevelTwoComparer => new
        (
            protein => protein.Accession,
            protein => protein.PrecursorMass,
            protein => protein.FragmentCount,
            protein => protein.FragmentMasses.FirstOrDefault(),
            protein => protein.FragmentMasses.LastOrDefault(),
            protein => protein.FragmentMasses[protein.FragmentMasses.Count / 2],
            protein => protein.FragmentMasses[protein.FragmentMasses.Count / 3],
            protein => protein.FragmentMasses[protein.FragmentMasses.Count / 4]
        );


       

        #endregion

    }
}

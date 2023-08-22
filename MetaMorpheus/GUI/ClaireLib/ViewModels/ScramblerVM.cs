using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Chemistry;
using Easy.Common.Extensions;
using GuiFunctions;
using MzLibUtil;
using Proteomics.AminoAcidPolymer;
using ClassExtensions = Chemistry.ClassExtensions;

namespace MetaMorpheusGUI
{
    public class ScramblerVM : BaseViewModel
    {
        private string proteinSequence;
        private double massDif;
        private double targetMass;
        private bool truncations;
        private bool splices;
        private bool mostAbundant;
        private ScramblerResults results;

        private static readonly double WaterMonoisotopicMass =
            PeriodicTable.GetElement("H").PrincipalIsotope.AtomicMass * 2 +
            PeriodicTable.GetElement("O").PrincipalIsotope.AtomicMass;



        #region User Inputs

        public string ProteinSequence
        {
            get => proteinSequence;
            set
            {
                proteinSequence = value;
                OnPropertyChanged(nameof(ProteinSequence));
                UpdateProteinInfo();
            }
        }

        public double MassDifference
        {
            get => massDif;
            set { massDif = value; OnPropertyChanged(nameof(MassDifference)); OnPropertyChanged(nameof(MinProteinLength)); }
        }

        public double TargetMass
        {
            get => targetMass;
            set
            {
                targetMass = value;
                OnPropertyChanged(nameof(TargetMass));
                OnPropertyChanged(nameof(MinProteinLength));
            }
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

        public bool CalculateMostAbundant
        {
            get => mostAbundant;
            set { mostAbundant = value; OnPropertyChanged(nameof(CalculateMostAbundant)); }
        }


        #endregion

        #region Calculated Values

        public double MonoIsotopicMass =>
            ClassExtensions.RoundedDouble(WaterMonoisotopicMass + ProteinSequence?.Sum(p => Residue.ResidueMonoisotopicMass[p]) ?? 0, 3) ?? 0;

        public double MostAbundantMass
        {
            get
            {
                if (ProteinSequence is null) return 0;
                var distribution =
                    IsotopicDistribution.GetDistribution(new Proteomics.AminoAcidPolymer.Peptide(ProteinSequence).GetChemicalFormula());
                var mostIntenseIndex = distribution.Intensities.IndexOf(distribution.Intensities.Max());
                return ClassExtensions.RoundedDouble(distribution.Masses[mostIntenseIndex], 3) ?? 0;
            }
        }

        public int MinProteinLength => (int)((TargetMass - MassDifference) / Residue.ResidueMonoisotopicMass['W']);

        #endregion


        public ScramblerResults Results
        {
            get => results;
            set
            {
                results = value;
                OnPropertyChanged(nameof(Results));
                OnPropertyChanged(nameof(Results.TruncationSequences));
                OnPropertyChanged(nameof(Results.SpliceSequences));
                OnPropertyChanged(nameof(Results.AllSequences));
                OnPropertyChanged(nameof(Results.AllUniqueSequences));
                OnPropertyChanged(nameof(Results.AllPossibleSequences));
                OnPropertyChanged(nameof(Results.AllResults));
            }
        }

        public ScramblerVM()
        {
            MassDifference = 50;
            Truncations = true;
            SpliceVariants = true;
        }

        private Stopwatch stopwatch;
        public void RunAnalysis(bool time = false)
        {
            if (time) { stopwatch = Stopwatch.StartNew(); }

            // generate all possible strings
            List<string> allPossibleStrings = new() { ProteinSequence };
            ScramblerResults results = new();

            if (Truncations)
            {
                allPossibleStrings.AddRange(GetTruncySequences());
                results.TruncationSequences = allPossibleStrings.Count - 1;
            }

            if (SpliceVariants)
            {
                List<string> splicesToAdd = new();
                foreach (var possible in allPossibleStrings)
                {
                    var temp = GetSplicedSequences(possible).ToList();
                    results.SpliceSequences += temp.Count;
                    splicesToAdd.AddRange(temp);
                }
                allPossibleStrings.AddRange(splicesToAdd);
            }

            results.AllSequences = allPossibleStrings.Count;
            var distinctStrings = allPossibleStrings.Distinct().ToList();
            results.AllUniqueSequences = distinctStrings.Count;


            // TODO: Pair down possible sequences
            // this can be done by finding the shortest sequence of the heaviest amino acid 
            // that can possible create the mass of the protein, to create a lower bound
            // do not calculate the masses for those 


            // find mass of each remaining
            List<ScramblerResults.UwuResultsEntry> allUniqueResults = new();

            Tolerance tolerance = new AbsoluteTolerance(MassDifference);
            foreach (var sequence in distinctStrings)
            {
                var monoMass = ClassExtensions.RoundedDouble(WaterMonoisotopicMass + sequence.Sum(p => Residue.ResidueMonoisotopicMass[p]), 3) ?? throw new NullReferenceException("Idk bro in mono mass");
                if (!tolerance.Within(monoMass, TargetMass))
                    continue;

                double mostAbundantMass = 0;
                var massDifference = ClassExtensions.RoundedDouble(monoMass - TargetMass, 3) ??
                              throw new NullReferenceException("Idk brother, its in mass diff tho");
                if (CalculateMostAbundant)
                {
                    var distribution =
                        IsotopicDistribution.GetDistribution(new Proteomics.AminoAcidPolymer.Peptide(sequence).GetChemicalFormula());
                    var mostIntenseIndex = distribution.Intensities.IndexOf(distribution.Intensities.Max());
                    mostAbundantMass = ClassExtensions.RoundedDouble(distribution.Masses[mostIntenseIndex], 3) ?? throw new NullReferenceException("Idk bro in most abundant mass");

                }

                allUniqueResults.Add(new ScramblerResults.UwuResultsEntry(monoMass, mostAbundantMass, sequence, massDifference));
            }

            var orderedResults = allUniqueResults.OrderBy(p => Math.Abs(p.MassDifference)).ToList();
            results.AllPossibleSequences = orderedResults.Count;
            results.AllResults.AddRange(orderedResults);
            Results = results;

            if (time)
            {
                var elapsed = stopwatch.Elapsed.Milliseconds;

            }
        }

        public void ExportResults()
        {
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var outDirectory = Path.Combine(documentsPath, "UwU");
            if (!Directory.Exists(outDirectory))
                Directory.CreateDirectory(outDirectory);
            var fileName = $"Within{MassDifference}of{TargetMass}for{ProteinSequence}.csv";
            var outPath = Path.Combine(outDirectory, fileName);

            int index = 1;
            while (File.Exists(outPath))
            {
                outPath = outPath.Replace(".csv", $"({index}).csv");
                index++;
            }

            using (var sw = new StreamWriter(File.Create(outPath)))
            {
                sw.WriteLine("Mass Difference,Monoisotopic Mass,Base Sequence,Most Abundant Mass");
                foreach (var result in Results.AllResults)
                {
                    sw.WriteLine(result.ToString());
                }
            }

            MessageBox.Show($"Results Exported to {outPath}");

        }

        #region String Generation

        public IEnumerable<string> GetTruncySequences()
        {
            List<string> list = new();
            for (int i = 0; i < ProteinSequence.Length; i++)
            {
                yield return ProteinSequence.Substring(0, ProteinSequence.Length - i);
            }
            for (int i = 1; i < ProteinSequence.Length; i++)
            {
                yield return ProteinSequence.Substring(i, ProteinSequence.Length - i);
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

                    if (start + (seqToSplice.Length - start - toRemove) < MinProteinLength)
                        break;

                    var result = seqToSplice.Substring(0, start) +
                                 seqToSplice.Substring(start + toRemove, seqToSplice.Length - start - toRemove);

                    strings.Add(result);
                }
            }

            return strings;
        }

        #endregion

        public void UpdateProteinInfo()
        {
            OnPropertyChanged(nameof(MonoIsotopicMass));
            OnPropertyChanged(nameof(MostAbundantMass));
            OnPropertyChanged(nameof(MinProteinLength));
        }
    }

    
}

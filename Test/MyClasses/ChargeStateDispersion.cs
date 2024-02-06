using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chemistry;
using EngineLayer;
using MassSpectrometry;
using MzLibUtil;
using NUnit.Framework;
using Readers;

namespace Test
{

    internal static class RunChargeStateDispersion
    {
        [Test]
        public static void ChargeDispersionRunner()
        {
            double qValueCutoff = 0.01;
            List<string> psmPaths = new();
            List<string> dataFilePaths = new();
            string outputFilePath = @"D:\Projects\Code Maintenance\ChargeStateDispersion\Testing";

            var sequences = psmPaths.SelectMany(p => PsmTsvReader.ReadTsv(p, out _).Where(psm => psm.QValue <= qValueCutoff))
                .Select(psm => psm.BaseSeq);
            var dataFiles = dataFilePaths.ToDictionary(Path.GetFileNameWithoutExtension, MsDataFileReader.GetDataFile);

 

            foreach (string sequence in sequences)
            {
                double score =
                    ChargeStateDispersion.CalculateChargeDispersion(sequence, ChargeStateDispersion.ResidueCharges);
                Console.WriteLine($"{sequence}\t{score}");
            }
        }
    }



    internal static class ChargeStateDispersion
    {

        public static Dictionary<char, int> ResidueCharges = new()
        {
            { 'D', -1 },
            { 'E', -1 },
            { 'H', 1 },
            { 'K', 1 },
            { 'R', 1 },
        };

        public static double CalculateChargeDispersion(string proteinSequence, Dictionary<char, int> residueCharges)
        {
            int sequenceLength = proteinSequence.Length;
            List<int> positivePositions = new List<int>();
            List<int> negativePositions = new List<int>();

            for (int i = 0; i < sequenceLength; i++)
            {
                char residue = proteinSequence[i];

                if (residueCharges.ContainsKey(residue))
                {
                    int charge = residueCharges[residue];

                    if (charge > 0)
                    {
                        positivePositions.Add(i);
                    }
                    else if (charge < 0)
                    {
                        negativePositions.Add(i);
                    }
                }
            }

            if (positivePositions.Count == 0 || negativePositions.Count == 0)
            {
                return 0.0; // No dispersion if either positive or negative charges are absent.
            }

            double meanPositive = positivePositions.Average();
            double meanNegative = negativePositions.Average();

            double variancePositive = positivePositions.Sum(pos => Math.Pow(pos - meanPositive, 2));
            double varianceNegative = negativePositions.Sum(pos => Math.Pow(pos - meanNegative, 2));

            double score = 1.0 - (variancePositive + varianceNegative) / ((positivePositions.Count + negativePositions.Count) - 2);

            return score;
        }

        //private static List<ChargeState> FindExistingChargeStates(double monoisotopicMass, MzSpectrum averagedScan)
        //{
        //    PpmTolerance rootFindingTolerance = new PpmTolerance(20);
        //    PpmTolerance adjacentPeakFindingTolerance = new PpmTolerance(10);
        //    List<TheoreticalIsotopologue> theoreticalDistribution = GetSpecifiedAveragineDistribution(monoisotopicMass);
        //    int maxIsotopologues = theoreticalDistribution.Count();
        //    double mostAbundantMass = theoreticalDistribution.OrderByDescending(i => i.probability).First().mass;
        //    int minCharge = 5;
        //    int maxCharge = 50;
        //    List<int> possibleChargeStates = new List<int>();
        //    for (int i = minCharge; i <= maxCharge; i++)
        //    {
        //        if (mostAbundantMass.ToMz(i) >= averagedScan.IsolationRange.Minimum && mostAbundantMass.ToMz(i) <= averagedScan.IsolationRange.Maximum)
        //        {
        //            possibleChargeStates.Add(i);
        //        }
        //    }
        //    List<ChargeState> chargeStates = new List<ChargeState>();
        //    foreach (int chargeCount in possibleChargeStates)
        //    {
        //        ChargeState cs = new ChargeState(chargeCount);
        //        double queryMz = mostAbundantMass.ToMz(chargeCount);
        //        int possibleRootPeakIndex = averagedScan.GetClosestPeakIndex(queryMz);
        //        if (rootFindingTolerance.Within(averagedScan.XArray[possibleRootPeakIndex].ToMass(chargeCount), mostAbundantMass))
        //        {
        //            cs.peaks.Add(new MsPeak(averagedScan.XArray[possibleRootPeakIndex], averagedScan.YArray[possibleRootPeakIndex]));
        //            MzPeak previousLowerPeak = cs.peaks.First();
        //            MzPeak previousUpperPeak = cs.peaks.First();
        //            double csRootmass = averagedScan.XArray[possibleRootPeakIndex].ToMass(chargeCount);
        //            int indexShiftFromRootPeak = 1;
        //            int rootPeakIndex = (int)Math.Round(mostAbundantMass - monoisotopicMass);
        //            bool findingLowerIsotopologues = true;
        //            bool findingUpperIsotopologues = true;
        //            while ((findingLowerIsotopologues && findingUpperIsotopologues) || (findingLowerIsotopologues || findingUpperIsotopologues))
        //            {
        //                if (findingLowerIsotopologues && findingUpperIsotopologues)
        //                {
        //                    //Check for lower isotopologue peak within tolerance
        //                    double lowerIsotopologueMass = csRootmass - (indexShiftFromRootPeak * MONOISOTOPIC_MASS_UNIT);
        //                    int possibleLowerIsotopologuePeakIndex = averagedScan.GetClosestPeakIndex(lowerIsotopologueMass.ToMz(chargeCount));
        //                    if (adjacentPeakFindingTolerance.Within(averagedScan.XArray[possibleLowerIsotopologuePeakIndex].ToMass(chargeCount), lowerIsotopologueMass))
        //                    {
        //                        MzPeak newLowerPeak = new MzPeak(averagedScan.XArray[possibleLowerIsotopologuePeakIndex], averagedScan.YArray[possibleLowerIsotopologuePeakIndex]);
        //                        newLowerPeak.isotopologueIndex = rootPeakIndex - indexShiftFromRootPeak;
        //                        cs.peaks.Add(newLowerPeak);
        //                    }
        //                    //check for upper isotopologue peak within tolerance
        //                    double upperIsotopologueMass = csRootmass + (indexShiftFromRootPeak * MONOISOTOPIC_MASS_UNIT);
        //                    int possibleUpperIsotopologuePeakIndex = averagedScan.GetClosestPeakIndex(upperIsotopologueMass.ToMz(chargeCount));
        //                    if (adjacentPeakFindingTolerance.Within(averagedScan.XArray[possibleUpperIsotopologuePeakIndex].ToMass(chargeCount), upperIsotopologueMass))
        //                    {
        //                        MsPeak newUpperPeak = new MsPeak(averagedScan.XArray[possibleUpperIsotopologuePeakIndex], averagedScan.YArray[possibleUpperIsotopologuePeakIndex]);
        //                        newUpperPeak.isotopologueIndex = rootPeakIndex + indexShiftFromRootPeak;
        //                        cs.peaks.Add(newUpperPeak);
        //                    }
        //                }
        //                else if (findingLowerIsotopologues && !findingUpperIsotopologues)
        //                {
        //                    double lowerIsotopologueMass = csRootmass - (indexShiftFromRootPeak * MONOISOTOPIC_MASS_UNIT);
        //                    int possibleLowerIsotopologuePeakIndex = averagedScan.GetClosestPeakIndex(lowerIsotopologueMass.ToMz(chargeCount));
        //                    if (adjacentPeakFindingTolerance.Within(averagedScan.XArray[possibleLowerIsotopologuePeakIndex].ToMass(chargeCount), lowerIsotopologueMass))
        //                    {
        //                        MsPeak newLowerPeak = new MsPeak(averagedScan.XArray[possibleLowerIsotopologuePeakIndex], averagedScan.YArray[possibleLowerIsotopologuePeakIndex]);
        //                        newLowerPeak.isotopologueIndex = rootPeakIndex - indexShiftFromRootPeak;
        //                        cs.peaks.Add(newLowerPeak);
        //                    }
        //                }
        //                else if (!findingLowerIsotopologues && findingUpperIsotopologues)
        //                {
        //                    double upperIsotopologueMass = csRootmass + (indexShiftFromRootPeak * MONOISOTOPIC_MASS_UNIT);
        //                    int possibleUpperIsotopologuePeakIndex = averagedScan.GetClosestPeakIndex(upperIsotopologueMass.ToMz(chargeCount));
        //                    if (adjacentPeakFindingTolerance.Within(averagedScan.XArray[possibleUpperIsotopologuePeakIndex].ToMass(chargeCount), upperIsotopologueMass))
        //                    {
        //                        MsPeak newUpperPeak = new MsPeak(averagedScan.XArray[possibleUpperIsotopologuePeakIndex], averagedScan.YArray[possibleUpperIsotopologuePeakIndex]);
        //                        newUpperPeak.isotopologueIndex = rootPeakIndex + indexShiftFromRootPeak;
        //                        cs.peaks.Add(newUpperPeak);
        //                    }
        //                }
        //                indexShiftFromRootPeak++;
        //                int lowerIsotopologueIndex = rootPeakIndex - indexShiftFromRootPeak;
        //                int upperIsotopologueIndex = rootPeakIndex + indexShiftFromRootPeak;
        //                if (lowerIsotopologueIndex < 0)
        //                    findingLowerIsotopologues = false;
        //                if (upperIsotopologueIndex >= theoreticalDistribution.Count)
        //                    findingUpperIsotopologues = false;
        //            }
        //            if (cs.peaks.Count >= 6 && CalculateChargeStateCosineSimilarity(theoreticalDistribution, cs.peaks, cs.chargeCount) >= 0.75)
        //            {
        //                double mostAbundantPeakIntensity = cs.peaks.OrderByDescending(p => p.intensity).ToList().First().intensity;
        //                foreach (MsPeak peak in cs.peaks)
        //                {
        //                    peak.chargeCount = chargeCount;
        //                    peak.normalized_intensity = peak.intensity / mostAbundantPeakIntensity;
        //                }
        //                cs.total_intensity = cs.peaks.Sum(p => p.intensity);
        //                cs.monoisotopicMz = monoisotopicMass.ToMz(cs.chargeCount);
        //                cs.mostAbundantMz = cs.peaks.OrderByDescending(p => p.intensity).ToList().First().mz;
        //                cs.maxMz = cs.peaks.OrderBy(p => p.mz).Last().mz;
        //                chargeStates.Add(cs);
        //            }
        //        }
        //    }
        //    foreach (ChargeState cs in chargeStates)
        //    {
        //        cs.normalizedAbundance = cs.total_intensity / chargeStates.Sum(c => c.total_intensity);
        //    }
        //    return chargeStates;
        //}
    }
}

﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EngineLayer;
using MathNet.Numerics;
using NUnit.Framework;
using Proteomics;
using Proteomics.Fragmentation;
using Proteomics.ProteolyticDigestion;

namespace Test
{
    [TestFixture]
    public static class SoftwareComparisons
    {

        [Test]
        public static void RunOnManyRuns()
        {
            string directoryPath =
                @"D:\Projects\Top Down MetaMorpheus\DavidTabbResults\PXD019247-ECOLI\Search Outputs\MM";
            DavidTabbOutputter.RunOnAllMMDirectories(directoryPath, false);
        }

        [Test]
        public static void RunOnAllTasksInOneRun()
        {
                        string directoryPath =
                @"D:\Projects\Top Down MetaMorpheus\DavidTabbResults\PXD003074-SULIS\Search Outputs\MM";
           // DavidTabbOutputter.RunOnAllSearchTasksInDirectory(directoryPath, true, "EmpericalQValue");
           // DavidTabbOutputter.RunOnAllSearchTasksInDirectory(directoryPath, false, "");
            DavidTabbOutputter.RunOnAllMMDirectories(directoryPath, false);
        }

        [Test]
        public static void RunOnAveragingData()
        {
            string directoryPath =
                @"C:\Users\Nic\OneDrive - UW-Madison\AUSTIN V CARR - AUSTIN V CARR's files\SpectralAveragingPaper\Supplemental Information\MM Output Bulk Jurkat";
            foreach (var file in Directory.GetFiles(directoryPath, "*.psmtsv"))
            {
                string output = Path.Combine(directoryPath, Path.GetFileNameWithoutExtension(file) + ".tsv");
                DavidTabbOutputter.CreateComparableOutput(file, output, false, true);
            }
        
        }
    }



    public static class DavidTabbOutputter
    {

        /// <summary>
        /// Creates a proforma sequence and mass added from a full sequence
        /// Outputs in format introduced in David Tabb Paper
        /// Deal with other mods will create a comparable output, but not necessarily in the proforma syntax
        /// </summary>
        /// <param name="inputPsmsPath"></param>
        /// <param name="outputTsvPath"></param>
        /// <param name="empiricalQ"></param>
        /// <param name="dealWithOtherMods"></param>
        public static void CreateComparableOutput(string inputPsmsPath, string outputTsvPath, bool empiricalQ,
            bool dealWithOtherMods = false)
        {
            var psms = PsmTsvReader.ReadTsv(inputPsmsPath, out List<string> warnings)
                .Where(p => p.AmbiguityLevel == "1")
                .ToList();

            if (warnings.Any())
            {
                Console.WriteLine("Warnings encountered while reading PSMs:");
                foreach (var warning in warnings)
                {
                    Console.WriteLine("\t" + warning);
                }
            }
            if (!outputTsvPath.EndsWith(".tsv"))
            {
                outputTsvPath += ".tsv";
            }

            if (empiricalQ)
                SetQValueBasedOnSampleType(psms);
            
            var eValueCurve = EValueRegressionFormulaByTailFittingForTopPsms(psms, empiricalQ);
            using (var sw = new StreamWriter(File.Create(outputTsvPath)))
            {
                foreach (var psm in psms.Where(p => p.DecoyContamTarget == "T"))
                {
                    string raw = psm.FileNameWithoutExtension.Replace("-calib", "").Replace("-averaged", "");
                    int scan = psm.Ms2ScanNumber;
                    int z = psm.PrecursorCharge;
                    string accession = psm.ProteinAccession;
                    string trunc = psm.BaseSeq;
                    var (proforma, massAdded) = GetProformaSequenceAndMassAddedFromFullSequence(psm.FullSequence, dealWithOtherMods);

                    double eValue =/* empiricalQ ? psm.QValue :*/ GetEValue((int)psm.Score, eValueCurve.intercept, eValueCurve.slope);
                    eValue = psm.QValue;
                    if (eValue > 0.01)
                        continue;

                    double negLogEValue = -Math.Log(eValue, 10);
                    sw.WriteLine($"{raw}\t{scan}\t{z}\t{accession}\t{trunc}\t{massAdded}\t{proforma}\t{negLogEValue}");
                }
            }
        }

        private static double GetEValue(int score, double intercept, double slope)
        {
            return Math.Pow(10, intercept + slope * /*(score == 0 ? Math.Log10(0.000001) :*/ Math.Log10(score));
        }

        private static (string, double) GetProformaSequenceAndMassAddedFromFullSequence(string fullSequence, bool dealWithOtherMods = false)
        {
            double massAdded = 0;
            string tempSequence = fullSequence;
            if (tempSequence.Contains("Common Biological:Methylation on K"))
            {
                tempSequence = tempSequence.Replace("Common Biological:Methylation on K", "Methyl");
                massAdded += 14;
            }

            if (tempSequence.Contains("Common Biological:Acetylation on K"))
            {
                tempSequence = tempSequence.Replace("Common Biological:Acetylation on K", "Acetyl");
                massAdded += 42;
            }
            if (tempSequence.Contains("Common Biological:Acetylation on X"))
            {
                tempSequence = tempSequence.Replace("Common Biological:Acetylation on X", "Acetyl");
                massAdded += 42;
            }

            if (tempSequence.Contains("Common Biological:Phosphorylation on S"))
            {
                tempSequence = tempSequence.Replace("Common Biological:Phosphorylation on S", "Phospho");
                massAdded += 80;
            }
            if (tempSequence.Contains("Common Biological:Phosphorylation on T"))
            {
                tempSequence = tempSequence.Replace("Common Biological:Phosphorylation on T", "Phospho");
                massAdded += 80;
            }
            if (tempSequence.Contains("Common Biological:Phosphorylation on Y"))
            {
                tempSequence = tempSequence.Replace("Common Biological:Phosphorylation on Y", "Phospho");
                massAdded += 80;
            }

            if (tempSequence.Contains("Common Variable:Oxidation on M"))
            {
                tempSequence = tempSequence.Replace("Common Variable:Oxidation on M", "Oxidation");
                massAdded += 16;
            }
            string proforma = tempSequence;

            if (dealWithOtherMods && proforma.Contains(':'))
            {
                var result = DealWithOtherModifications(proforma);
                proforma = result.proforma;
                massAdded += result.massAdded;
            }

            return (proforma, massAdded);
        }

        /// <summary>
        /// The method fits a line for log[survival] vs. log[(int)score] for the top 10% scoring spectrum matches
        /// This line can be used to compute the E-value of a spectrum match by inputing the log[(int)score]
        /// and raising 10 the the power of the computed value (10^y)
        /// For high scoring spectrum matches (~the set of spectrum matches at 1% FDR), this value should be <= 0
        /// This function should not used to calculate E-Value for anything with greater than 1% FDR
        /// I found this strategy in an asms presentation pdf from 2006
        /// https://prospector.ucsf.edu/prospector/html/misc/publications/2006_ASMS_1.pdf
        /// Protein Prospector and Ways Calculating Expectation Values
        /// Aenoch J. Lynn; Robert J. Chalkley; Peter R. Baker; Mark R.Segal; and Alma L.Burlingame
        /// </summary>
        /// <param name="allPSMs"></param>
        /// <returns></returns>
        public static (double intercept, double slope) EValueRegressionFormulaByTailFittingForTopPsms(List<PsmFromTsv> allPSMs, bool empiricalQ)
        {
            var decoys = allPSMs.Where(p => p.DecoyContamTarget == "D")
                .OrderByDescending(p => p.Score)
                .ToList();
            if (empiricalQ || decoys.Count == 0)
            {
                decoys = allPSMs
                    .Where(p => p.IsDecoy())
                    .OrderByDescending(p => p.Score)
                    .ToList();
            }

            var decoyScoreHistogram = decoys //we are fitting the tail to only decoy PSMs
                    .Select(p => (int)p.Score) //we are only interested in the integer score because the decimal portion is unrelated
                    .GroupBy(s => s).ToList(); //making a score histogram here.
            

           double[] survival = new double[decoyScoreHistogram.Select(k => k.Key).ToList().Max() + 1];

           foreach (var scoreCountPair in decoyScoreHistogram)
           {
               survival[scoreCountPair.Key] =
                   scoreCountPair
                       .Count(); //the array already has a value of 0 at each index (which is the integer Morpheus score) during creation. so we only need to populate it where we have scores
           }

           List<double> logScores = new(); //x-values
            List<double> logSurvivals = new(); //y-values

            double runningSum = 0;
            for (int i = survival.Length - 1; i > -1; i--)
            {
                runningSum += survival[i];
                survival[i] = runningSum;
            }

            double countMax = survival.Max();

            for (int i = 0; i < survival.Length; i++)
            {
                survival[i] /= countMax;
            }

            double[] logSurvival = new double[survival.Length];
            for (int i = 0; i < survival.Length; i++)
            {
                if (survival[i] > 0 && survival[i] < (0.1 * survival.Max()))
                {
                    logSurvival[i] = Math.Log10(survival[i]);
                    logScores.Add(Math.Log10(i));
                    logSurvivals.Add(Math.Log10(survival[i]));
                }
            }

            return Fit.Line(logScores.ToArray(), logSurvivals.ToArray());
        }

        public static void SetQValueBasedOnSampleType(List<PsmFromTsv> psms)
        {
            double cumulativeTarget = 0;
            double cumulativeDecoy = 0;
            foreach (var psm in psms.OrderByDescending(p => p.Score).ThenBy(b => Math.Abs(double.Parse(b.PeptideMonoMass.Split('|')[0]) - b.PrecursorMass)))
            {
                if (psm.IsDecoy())
                {
                    cumulativeDecoy++;
                }
                else
                {
                    cumulativeTarget++;
                }

                double qValue = Math.Min(1, cumulativeDecoy / cumulativeTarget );
                psm.QValue = qValue;
            }
        }

        public static bool IsDecoy(this PsmFromTsv psm)
        {
            if (double.IsNaN(psm.PEP_QValue))
            {
                if (!psm.ProteinAccession.StartsWith("M9"))
                    return true;
            }
            else if (psm.DecoyContamTarget == "D")
                return true;
            return false;
        }

        private static (string proforma, double massAdded) DealWithOtherModifications(string fullSequence)
        {
            var idToMod = GlobalVariables.AllModsKnownDictionary;
            var _allModsOneIsNterminus = new Dictionary<int, Modification>();
            int currentModStart = 0;
            int currentModificationLocation = 1;
            bool currentlyReadingMod = false;
            int bracketCount = 0;

            for (int r = 0; r < fullSequence.Length; r++)
            {
                char c = fullSequence[r];
                if (c == '[')
                {
                    currentlyReadingMod = true;
                    if (bracketCount == 0)
                    {
                        currentModStart = r + 1;
                    }
                    bracketCount++;
                }
                else if (c == ']')
                {
                    string modId = null;
                    bracketCount--;
                    if (bracketCount == 0)
                    {
                        try
                        {
                            //remove the beginning section (e.g. "Fixed", "Variable", "Uniprot")
                            string modString = fullSequence.Substring(currentModStart, r - currentModStart);
                            int splitIndex = modString.IndexOf(':');
                            if (splitIndex == -1)
                            {
                                currentlyReadingMod = false;
                                continue;
                            }
                            string modType = modString.Substring(0, splitIndex);
                            modId = modString.Substring(splitIndex + 1, modString.Length - splitIndex - 1);
                        }
                        catch (Exception e)
                        {
                            throw new MzLibUtil.MzLibException(
                                "Error while trying to parse string into peptide: " + e.Message);
                        }
                        if (!idToMod.TryGetValue(modId, out Modification mod))
                        {
                            throw new MzLibUtil.MzLibException(
                                "Could not find modification while reading string: " + fullSequence);
                        }
                        if (mod.LocationRestriction.Contains("C-terminal.") && r == fullSequence.Length - 1)
                        {
                            currentModificationLocation = PeptideWithSetModifications.GetBaseSequenceFromFullSequence(fullSequence).Length + 2;
                        }
                        _allModsOneIsNterminus.Add(currentModificationLocation, mod);
                        currentlyReadingMod = false;
                    }
                }
                else if (!currentlyReadingMod)
                {
                    currentModificationLocation++;
                }
                //else do nothing
            }

            int massAdded = 0;
            foreach (var mod in _allModsOneIsNterminus.Values)
            {
                fullSequence = fullSequence.Replace($"{mod.ModificationType}:{mod.IdWithMotif}", mod.OriginalId);
                massAdded += (int)Math.Round(mod.MonoisotopicMass.Value, 0);
            }

            return (fullSequence, 2);
        }

        public static void RunOnAllMMDirectories(string directoryPath, bool empiricalQ, string extra = "")
        {
            foreach (var runDirectory in Directory.GetDirectories(directoryPath))
            {
                RunOnAllSearchTasksInDirectory(runDirectory, empiricalQ, extra);
            }
        }

        public static void RunOnAllSearchTasksInDirectory(string directoryPath, bool empiricalQ, string extra = "")
        {
            foreach (var taskDirectory in Directory.GetDirectories(directoryPath))
            {
                if (!taskDirectory.Contains("SearchTask")) continue;
                var psmsPath = Path.Combine(taskDirectory, "AllPSMs.psmtsv");

                if (!File.Exists(psmsPath)) continue;
                string outPath = Path.Combine(taskDirectory,
                    $"MM-{directoryPath.Split("\\").Last()}{extra}.tsv");
                CreateComparableOutput(psmsPath, outPath, empiricalQ);
            }
        }

    }
}
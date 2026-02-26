using Chemistry;
using EngineLayer.SpectrumMatch.Scoring;
using MassSpectrometry;
using Omics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Transcriptomics.Digestion;

namespace EngineLayer
{
    public abstract class MetaMorpheusEngine
    {
        protected static readonly Dictionary<DissociationType, List<double>> complementaryIonConversionDictionary = new Dictionary<DissociationType, List<double>>
        {
            { DissociationType.LowCID, new List<double>(){ Constants.ProtonMass } },
            { DissociationType.HCD, new List<double>(){ Constants.ProtonMass } },
            { DissociationType.ETD,new List<double>() {2 * Constants.ProtonMass } }, //presence of zplusone (zdot) makes this two instead of one
            { DissociationType.CID,new List<double>() {Constants.ProtonMass } },
            { DissociationType.EThcD,new List<double>() {Constants.ProtonMass, 2 * Constants.ProtonMass } },
            //TODO: refactor such that complementary ions are generated specifically for their complementary pair.
            //TODO: create a method to auto-determine the conversion
        };

        public readonly CommonParameters CommonParameters;
        protected readonly List<(string FileName, CommonParameters Parameters)> FileSpecificParameters;
        protected readonly List<string> NestedIds;

        protected MetaMorpheusEngine(CommonParameters commonParameters, List<(string FileName, CommonParameters Parameters)> fileSpecificParameters, List<string> nestedIds)
        {
            CommonParameters = commonParameters;
            FileSpecificParameters = fileSpecificParameters;
            NestedIds = nestedIds;
        }

        public static event EventHandler<SingleEngineEventArgs> StartingSingleEngineHander;

        public static event EventHandler<SingleEngineFinishedEventArgs> FinishedSingleEngineHandler;

        public static event EventHandler<StringEventArgs> OutLabelStatusHandler;

        public static event EventHandler<StringEventArgs> WarnHandler;

        public static event EventHandler<ProgressEventArgs> OutProgressHandler;


        private static readonly XcorrScore _xCorrScoringFunction = new();
        private static readonly SpectralLibraryScore _spectralLibraryScoringFunction = new();
        public double CalculatePeptideScore(MsDataScan thisScan, List<MatchedFragmentIon> matchedFragmentIons, bool fragmentsCanHaveDifferentCharges = false)
        {
            if (fragmentsCanHaveDifferentCharges)
                return _spectralLibraryScoringFunction.CalculatePeptideScore(thisScan, matchedFragmentIons);

            if (thisScan.MassSpectrum.XcorrProcessed)
                return _xCorrScoringFunction.CalculatePeptideScore(thisScan, matchedFragmentIons);

            // This is defaulted to MorpheusScore, but it can be set to any IScoreFunction by the user.
            return CommonParameters.ScoringFunction.CalculatePeptideScore(thisScan, matchedFragmentIons);
        }

        public static List<MatchedFragmentIon> MatchFragmentIons(Ms2ScanWithSpecificMass scan, List<Product> theoreticalProducts, CommonParameters commonParameters, bool matchAllCharges = false)
        {
            if (matchAllCharges)
            {
                return MatchFragmentIonsOfAllCharges(scan, theoreticalProducts, commonParameters);
            }

            var matchedFragmentIons = new List<MatchedFragmentIon>();

            if (scan.TheScan.MassSpectrum.XcorrProcessed && scan.TheScan.MassSpectrum.XArray.Length != 0)
            {

                for (int i = 0; i < theoreticalProducts.Count; i++)
                {
                    var product = theoreticalProducts[i];
                    // unknown fragment mass; this only happens rarely for sequences with unknown amino acids
                    if (double.IsNaN(product.NeutralMass))
                    {
                        continue;
                    }

                    // Magic number represents mzbinning space. 
                    double theoreticalFragmentMz = Math.Round(product.NeutralMass.ToMz(1) / 1.0005079, 0) * 1.0005079;
                    var closestMzIndex = scan.TheScan.MassSpectrum.GetClosestPeakIndex(theoreticalFragmentMz);

                    if (commonParameters.ProductMassTolerance.Within(scan.TheScan.MassSpectrum.XArray[closestMzIndex], theoreticalFragmentMz))
                    {
                        matchedFragmentIons.Add(new MatchedFragmentIon(product, theoreticalFragmentMz, scan.TheScan.MassSpectrum.YArray[closestMzIndex], 1));
                    }
                }

                return matchedFragmentIons;
            }

            // if the spectrum has no peaks
            if (scan.ExperimentalFragments != null && !scan.ExperimentalFragments.Any())
            {
                return matchedFragmentIons;
            }

            // search for ions in the spectrum
            for (int i = 0; i < theoreticalProducts.Count; i++)
            {
                var product = theoreticalProducts[i];
                // unknown fragment mass; this only happens rarely for sequences with unknown amino acids
                if (double.IsNaN(product.NeutralMass))
                {
                    continue;
                }

                // get the closest peak in the spectrum to the theoretical peak
                var closestExperimentalMass = scan.GetClosestExperimentalIsotopicEnvelope(product.NeutralMass);

                // is the mass error acceptable?
                if (closestExperimentalMass != null
                    && commonParameters.ProductMassTolerance.Within(closestExperimentalMass.MonoisotopicMass, product.NeutralMass)
                    && Math.Abs(closestExperimentalMass.Charge) <= Math.Abs(scan.PrecursorCharge))//TODO apply this filter before picking the envelope
                {
                    matchedFragmentIons.Add(new MatchedFragmentIon(product, closestExperimentalMass.MonoisotopicMass.ToMz(closestExperimentalMass.Charge),
                        closestExperimentalMass.Peaks.First().intensity, closestExperimentalMass.Charge));
                }
            }
            if (commonParameters.AddCompIons)
            {
                foreach (double massShift in complementaryIonConversionDictionary[commonParameters.DissociationType])
                {
                    double protonMassShift = massShift.ToMass(1);

                    for (int i = 0; i < theoreticalProducts.Count; i++)
                    {
                        var product = theoreticalProducts[i];
                        // unknown fragment mass or diagnostic ion or precursor; skip those
                        if (double.IsNaN(product.NeutralMass) || product.ProductType == ProductType.D || product.ProductType == ProductType.M)
                        {
                            continue;
                        }

                        double compIonMass = scan.PrecursorMass + protonMassShift - product.NeutralMass;

                        // get the closest peak in the spectrum to the theoretical peak
                        IsotopicEnvelope closestExperimentalMass = scan.GetClosestExperimentalIsotopicEnvelope(compIonMass);

                        // is the mass error acceptable?
                        if (commonParameters.ProductMassTolerance.Within(closestExperimentalMass.MonoisotopicMass, compIonMass) && closestExperimentalMass.Charge <= scan.PrecursorCharge)
                        {
                            //found the peak, but we don't want to save that m/z because it's the complementary of the observed ion that we "added". Need to create a fake ion instead.
                            double mz = (scan.PrecursorMass + protonMassShift - closestExperimentalMass.MonoisotopicMass).ToMz(closestExperimentalMass.Charge);

                            matchedFragmentIons.Add(new MatchedFragmentIon(product, mz, closestExperimentalMass.TotalIntensity, closestExperimentalMass.Charge));
                        }
                    }
                }
            }

            return matchedFragmentIons;
        }
        
        //Used only when user wants to generate spectral library.
        //Normal search only looks for one match ion for one fragment, and if it accepts it then it doesn't try to look for different charge states of that same fragment. 
        //But for library generation, we need find all the matched peaks with all the different charges.
        private static List<MatchedFragmentIon> MatchFragmentIonsOfAllCharges(Ms2ScanWithSpecificMass scan, List<Product> theoreticalProducts, CommonParameters commonParameters)
        {
            var matchedFragmentIons = new List<MatchedFragmentIon>();
            var ions = new List<string>();

            // if the spectrum has no peaks
            if (scan.ExperimentalFragments != null && !scan.ExperimentalFragments.Any())
            {
                return matchedFragmentIons;
            }

            // search for ions in the spectrum
            foreach (Product product in theoreticalProducts)
            {
                // unknown fragment mass; this only happens rarely for sequences with unknown amino acids
                if (double.IsNaN(product.NeutralMass))
                {
                    continue;
                }

                //get the range we can accept 
                var minMass = commonParameters.ProductMassTolerance.GetMinimumValue(product.NeutralMass);
                var maxMass = commonParameters.ProductMassTolerance.GetMaximumValue(product.NeutralMass);
                var closestExperimentalMassList = scan.GetClosestExperimentalIsotopicEnvelopeList(minMass, maxMass);
                if (closestExperimentalMassList != null)
                {
                    foreach (var x in closestExperimentalMassList)
                    {
                        String ion = $"{product.ProductType.ToString()}{ product.FragmentNumber}^{x.Charge}-{product.NeutralLoss}";
                        if (x != null 
                            && !ions.Contains(ion) 
                            && commonParameters.ProductMassTolerance.Within(x.MonoisotopicMass, product.NeutralMass) 
                            && Math.Abs(x.Charge) <= Math.Abs(scan.PrecursorCharge))//TODO apply this filter before picking the envelope
                        {
                            Product temProduct = product;
                            matchedFragmentIons.Add(new MatchedFragmentIon(temProduct, x.MonoisotopicMass.ToMz(x.Charge),
                                x.Peaks.First().intensity, x.Charge));

                            ions.Add(ion);
                        }
                    }
                }
            }

            return matchedFragmentIons;
        }
        protected abstract MetaMorpheusEngineResults RunSpecific();

        public MetaMorpheusEngineResults Run()
        {
            DetermineAnalyteType(CommonParameters);
            StartingSingleEngine();
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            this.CommonParameters.SetCustomProductTypes();
            var myResults = RunSpecific();
            stopWatch.Stop();
            myResults.Time = stopWatch.Elapsed;
            FinishedSingleEngine(myResults);
            return myResults;
        }

        public Task<MetaMorpheusEngineResults> RunAsync() => Task.Run(Run);

        /// <summary>
        /// Changes the name of the analytes from "peptide" to "proteoform" or "oligo" if the protease is set to top-down
        /// </summary>
        /// <param name="commonParameters"></param>
        public static void DetermineAnalyteType(CommonParameters commonParameters)
        {
            // Comment made while DetemineAnalyteType happened at the task layer
            // TODO: note that this will not function well if the user is using file-specific settings, but it's assumed
            // that bottom-up and top-down data is not being searched in the same task. 

            // Update: Now that it is in the engine layer, analyte type specific operations will be okay at the engine layer, meaning seaching top-down and bottom-up with file specific params will execute the proper control flow. However, a problem still exists in PostSearchAnalysis where that analyte type will be set to whatever the main parameters are. 

            if (commonParameters == null || commonParameters.DigestionParams == null)
                return;

            GlobalVariables.AnalyteType = commonParameters.DigestionParams switch
            {
                RnaDigestionParams => AnalyteType.Oligo,
                DigestionParams { Protease: not null } when commonParameters.DigestionParams.DigestionAgent.Name == "top-down"
                    => AnalyteType.Proteoform,
                _ => AnalyteType.Peptide
            };
        }

        #region Event Helpers

        public string GetId()
        {
            return string.Join(",", NestedIds);
        }

        protected void Warn(string v)
        {
            WarnHandler?.Invoke(this, new StringEventArgs(v, NestedIds));
        }

        protected void Status(string v)
        {
            OutLabelStatusHandler?.Invoke(this, new StringEventArgs(v, NestedIds));
        }

        protected void ReportProgress(ProgressEventArgs v)
        {
            OutProgressHandler?.Invoke(this, v);
        }

        private void StartingSingleEngine()
        {
            StartingSingleEngineHander?.Invoke(this, new SingleEngineEventArgs(this));
        }

        private void FinishedSingleEngine(MetaMorpheusEngineResults myResults)
        {
            FinishedSingleEngineHandler?.Invoke(this, new SingleEngineFinishedEventArgs(myResults));
        }

        #endregion
    }
}

using Chemistry;
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

        public static double CalculatePeptideScore(MsDataScan thisScan, List<MatchedFragmentIon> matchedFragmentIons)
        {
            double score = 0;

            if (thisScan.MassSpectrum.XcorrProcessed)
            {
                // XCorr
                foreach (var fragment in matchedFragmentIons
                    .GroupBy(p => (p.NeutralTheoreticalProduct.ProductType, p.NeutralTheoreticalProduct.FragmentNumber))
                    .Select(g => g.MaxBy(p => p.Intensity)))
                {
                    switch (fragment.NeutralTheoreticalProduct.ProductType)
                    {
                        case ProductType.aDegree:
                        case ProductType.aStar:
                        case ProductType.bWaterLoss:
                        case ProductType.bAmmoniaLoss:
                        case ProductType.yWaterLoss:
                        case ProductType.yAmmoniaLoss:
                            score += 0.01 * fragment.Intensity;
                            break;
                        case ProductType.D: //count nothing for diagnostic ions.
                            break;
                        default:
                            score += 1 * fragment.Intensity;
                            break;
                    }
                }
            }
            else
            {
                // Morpheus score
                foreach (var fragment in matchedFragmentIons
                             .GroupBy(p => (p.NeutralTheoreticalProduct.ProductType, p.NeutralTheoreticalProduct.FragmentNumber))
                             .Select(g => g.MaxBy(p => p.Intensity)))
                { 
                    switch (fragment.NeutralTheoreticalProduct.ProductType)
                    {
                        case ProductType.D:
                            break;
                        default:
                            score += 1 + fragment.Intensity / thisScan.TotalIonCurrent;
                            break;
                    }
                    
                }
            }

            return score;
        }

        #region Matching Fragment Ions

        public static List<MatchedFragmentIon> MatchFragmentIons(Ms2ScanWithSpecificMass scan, List<Product> theoreticalProducts, CommonParameters commonParameters, bool matchAllCharges = false, bool includeExperimentalEnvelope = false, bool isLowRes = false)
        {
            // if this is a child scan and it's an ion trap 2D scan, we want to use the wider tolerance for matching
            var productMassTolerance = isLowRes? commonParameters.ProductMassTolerance_LowRes : commonParameters.ProductMassTolerance;

            // Consider how to add complementary ions to xcorr analysis
            if (scan.TheScan.MassSpectrum.XcorrProcessed && scan.TheScan.MassSpectrum.XArray.Length != 0)
                return MatchFragmentIonsWithXcorr(scan, theoreticalProducts, commonParameters, matchAllCharges, includeExperimentalEnvelope);

            // if the spectrum has no peaks
            if (scan.ExperimentalFragments != null && !scan.ExperimentalFragments.Any())
                return [];

            var matchedFragmentIons = new List<MatchedFragmentIon>();

            // search for ions in the spectrum
            for (int i = 0; i < theoreticalProducts.Count; i++)
            {
                var product = theoreticalProducts[i];
                // unknown fragment mass; this only happens rarely for sequences with unknown amino acids
                if (double.IsNaN(product.NeutralMass))
                {
                    continue;
                }

                if (!matchAllCharges)
                {
                    var closestExperimentalMass = scan.GetClosestExperimentalIsotopicEnvelope(product.NeutralMass);
                    if (closestExperimentalMass != null
                        && productMassTolerance.Within(closestExperimentalMass.MonoisotopicMass, product.NeutralMass)
                        && Math.Abs(closestExperimentalMass.Charge) <= Math.Abs(scan.PrecursorCharge))
                    {
                        matchedFragmentIons.Add(CreateIon(product, closestExperimentalMass, includeExperimentalEnvelope));
                    }
                    continue;
                }

                foreach (var envelope in scan.GetExperimentalIsotopicEnvelopesInMassRange(
                    product.NeutralMass, productMassTolerance, true, scan.PrecursorCharge))
                {
                    matchedFragmentIons.Add(CreateIon(product, envelope, includeExperimentalEnvelope));
                }
            }

            if (commonParameters.AddCompIons)
            {
                foreach (double massShift in Util.ComplementaryIonConversion.complementaryIonConversionDictionary[commonParameters.DissociationType])
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

                        var complementInfo = Util.ComplementaryIonConversion.GetComplementInfo(product.ProductType, product.Terminus);
                        if (complementInfo == null)
                        {
                            continue;
                        }

                        double compIonMass = scan.PrecursorMass + protonMassShift - product.NeutralMass;
                        if (!matchAllCharges)
                        {
                            IsotopicEnvelope closestExperimentalMass = scan.GetClosestExperimentalIsotopicEnvelope(compIonMass);

                            if (closestExperimentalMass != null
                                && productMassTolerance.Within(closestExperimentalMass.MonoisotopicMass, compIonMass)
                                && Math.Abs(closestExperimentalMass.Charge) <= Math.Abs(scan.PrecursorCharge))
                            {
                                var compProduct = new Product(complementInfo.Value.complementType, complementInfo.Value.complementTerminus,
                                    compIonMass, product.FragmentNumber, product.ResiduePosition, 0);
                                double mz = closestExperimentalMass.MonoisotopicMass.ToMz(closestExperimentalMass.Charge);
                                matchedFragmentIons.Add(CreateIon(compProduct, closestExperimentalMass, includeExperimentalEnvelope, mz));
                            }
                            continue;
                        }

                        foreach (var envelope in scan.GetExperimentalIsotopicEnvelopesInMassRange(
                            compIonMass, productMassTolerance, true, scan.PrecursorCharge))
                        {
                            var compProduct = new Product(complementInfo.Value.complementType, complementInfo.Value.complementTerminus,
                                compIonMass, product.FragmentNumber, product.ResiduePosition, 0);
                            double mz = envelope.MonoisotopicMass.ToMz(envelope.Charge);
                            matchedFragmentIons.Add(CreateIon(compProduct, envelope, includeExperimentalEnvelope, mz));
                        }
                    }
                }
            }

            return matchedFragmentIons;
        }

        public static List<MatchedFragmentIon> MatchFragmentIonsWithXcorr(Ms2ScanWithSpecificMass scan, List<Product> theoreticalProducts, CommonParameters commonParameters, bool matchAllCharges = false, bool includeExperimentalEnvelope = false)
        {
            var productMassTolerance = commonParameters.ProductMassTolerance;
            var matchedFragmentIons = new List<MatchedFragmentIon>();

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


                if (productMassTolerance.Within(scan.TheScan.MassSpectrum.XArray[closestMzIndex], theoreticalFragmentMz))
                {
                    matchedFragmentIons.Add(new MatchedFragmentIon(product, theoreticalFragmentMz, scan.TheScan.MassSpectrum.YArray[closestMzIndex], 1));
                }
            }

            return matchedFragmentIons;
        }

        public static MatchedFragmentIon CreateIon(Product product, IsotopicEnvelope envelope, bool includeExperimentalEnvelope = false, double? mz = null)
        {
            mz ??= envelope.MonoisotopicMass.ToMz(envelope.Charge);
            if (includeExperimentalEnvelope)
            {
                return new MatchedFragmentIonWithEnvelope(product, mz.Value, envelope.Peaks.First().intensity, envelope.Charge)
                {
                    Envelope = envelope
                };
            }

            return new MatchedFragmentIon(product, mz.Value, envelope.Peaks.First().intensity, envelope.Charge);
        }


        #endregion

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
        /// Determines and sets the analyte type based on CommonParameters digestion settings.
        /// This method is called automatically by MetaMorpheusEngine.Run() to handle:
        /// - RNA mode (RnaDigestionParams → Oligo)
        /// - Top-down mode (protease == "top-down" → Proteoform)  
        /// - Bottom-up/default mode (→ Peptide)
        /// 
        /// IMPORTANT: This recalculates the analyte type at runtime and may differ from the GUI mode
        /// set by GuiGlobalParamsViewModel.IsRnaMode. This is intentional to support:
        /// - File-specific parameters with different modes
        /// - Mixed mode workflows
        /// 
        /// For GUI initialization, rely on GuiGlobalParamsViewModel.IsRnaMode which sets 
        /// GlobalVariables.AnalyteType. Do NOT call this method during GUI task window initialization.
        /// </summary>
        /// <param name="commonParameters"></param>
        public static void DetermineAnalyteType(CommonParameters commonParameters)
        {
            // Comment made while DetermineAnalyteType happened at the task layer
            // TODO: note that this will not function well if the user is using file-specific settings, but it's assumed
            // that bottom-up and top-down data is not being searched in the same task. 

            // Update: Now that it is in the engine layer, analyte type specific operations will be okay at the engine layer, meaning searching top-down and bottom-up with file specific params will execute the proper control flow. However, a problem still exists in PostSearchAnalysis where that analyte type will be set to whatever the main parameters are. 

            if (commonParameters == null || commonParameters.DigestionParams == null)
                return;

            GlobalVariables.AnalyteType = commonParameters.DetermineAnalyteType();
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

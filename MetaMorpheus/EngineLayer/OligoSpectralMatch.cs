using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Chemistry;
using Easy.Common.Extensions;
using MassSpectrometry;
using MathNet.Numerics;
using Proteomics.Fragmentation;
using ThermoFisher.CommonCore.Data.Business;
using Transcriptomics;

namespace EngineLayer
{
    public class OligoSpectralMatch : SpectralMatch
    {
        public MsDataScan MsDataScan { get; set; }

        public string FilePath { get; protected set; }
        public int ScanNumber { get; protected set; }
        public double RetentionTime { get; protected set; }
        public int PrecursorScanNumber { get; protected set; }
        public int PrecursorCharge { get; protected set; }
        public double PrecursorMz { get; protected set; }
        public double PrecursorMonoMass { get; protected set; }
        public double Score { get; protected set; }
        public int MsnOrder { get; private set; }
        public List<MatchedFragmentIon> MatchedFragmentIons { get; protected set; }
        public int SidEnergy { get; protected set; }
        

        public OligoSpectralMatch(MsDataScan scan, NucleicAcid oligo, string baseSequence,
            List<MatchedFragmentIon> matchedFragmentIons, string filePath)
        {
            MsDataScan = scan;
            BaseSequence = baseSequence;
            MatchedFragmentIons = matchedFragmentIons;
            FilePath = filePath;
            ScanNumber = MsDataScan.OneBasedScanNumber;
            PrecursorScanNumber = MsDataScan.OneBasedPrecursorScanNumber ?? 0;
            PrecursorCharge = MsDataScan.SelectedIonChargeStateGuess ?? 0 ;
            PrecursorMz = MsDataScan.SelectedIonMZ ?? 0;
            PrecursorMonoMass = oligo.MonoisotopicMass;
            MsnOrder = scan.MsnOrder;
            SidEnergy = GetSidEnergy(scan.ScanFilter);
            Score = MetaMorpheusEngine.CalculatePeptideScore(MsDataScan, MatchedFragmentIons).Round(2);
        }

        public OligoSpectralMatch(string tsvLine, Dictionary<string, int> parsedHeader)
        {
            var spl = tsvLine.Split(delimiter);
            FilePath = spl[parsedHeader[PsmTsvHeader.FileName]];
            ScanNumber = int.Parse(spl[parsedHeader[PsmTsvHeader.Ms2ScanNumber]]);
            RetentionTime = double.Parse(spl[parsedHeader[PsmTsvHeader.Ms2ScanRetentionTime]]);
            if (int.TryParse(spl[parsedHeader[PsmTsvHeader.PrecursorScanNum]].Trim(), out int result))
            {
                PrecursorScanNumber = result;
            }
            else
            {
                PrecursorScanNumber = 0;
            }
            PrecursorCharge = (int)double.Parse(spl[parsedHeader[PsmTsvHeader.PrecursorCharge]].Trim(), CultureInfo.InvariantCulture);
            PrecursorMz = double.Parse(spl[parsedHeader[PsmTsvHeader.PrecursorMz]].Trim(), CultureInfo.InvariantCulture);
            PrecursorMonoMass = double.Parse(spl[parsedHeader[PsmTsvHeader.PrecursorMass]].Trim(), CultureInfo.InvariantCulture);
            BaseSequence = spl[parsedHeader[PsmTsvHeader.BaseSequence]].Trim();
            MsnOrder = int.Parse(spl[parsedHeader[PsmTsvHeader.MsnOrder]].Trim());
            Score = double.Parse(spl[parsedHeader[PsmTsvHeader.Score]].Trim(), CultureInfo.InvariantCulture);

            MatchedFragmentIons = ReadFragmentIonsFromString(spl[parsedHeader[PsmTsvHeader.MatchedIonMzRatios]],
                spl[parsedHeader[PsmTsvHeader.MatchedIonIntensities]], BaseSequence,
                spl[parsedHeader[PsmTsvHeader.MatchedIonMassDiffDa]]);
            SidEnergy = int.Parse(spl[parsedHeader[PsmTsvHeader.SidEnergy]]);

        }

        private string delimiter = "\t";
        public string ToTsvString()
        {
            StringBuilder tsvStringBuilder = new();
            StringBuilder seriesStringBuilder = new StringBuilder();
            StringBuilder mzStringBuilder = new StringBuilder();
            StringBuilder fragmentDaErrorStringBuilder = new StringBuilder();
            StringBuilder fragmentPpmErrorStringBuilder = new StringBuilder();
            StringBuilder fragmentIntensityStringBuilder = new StringBuilder();
            List<StringBuilder> stringBuilders = new List<StringBuilder> { seriesStringBuilder, mzStringBuilder, fragmentDaErrorStringBuilder, fragmentPpmErrorStringBuilder, fragmentIntensityStringBuilder };

            tsvStringBuilder.Append(FilePath + this.delimiter);
            tsvStringBuilder.Append(ScanNumber + this.delimiter);
            tsvStringBuilder.Append(RetentionTime + this.delimiter);
            tsvStringBuilder.Append(PrecursorScanNumber + this.delimiter);
            tsvStringBuilder.Append(PrecursorCharge + this.delimiter);
            tsvStringBuilder.Append(PrecursorMz + this.delimiter);
            tsvStringBuilder.Append(PrecursorMonoMass + this.delimiter);
            tsvStringBuilder.Append(Score + this.delimiter);
            tsvStringBuilder.Append(BaseSequence + this.delimiter);
            tsvStringBuilder.Append(MsnOrder + this.delimiter);

            // using ", " instead of "," improves human readability
            const string delimiter = ", ";

            var matchedIonsGroupedByProductType = MatchedFragmentIons.GroupBy(x => new { x.NeutralTheoreticalProduct.ProductType, x.NeutralTheoreticalProduct.SecondaryProductType }).ToList();

            foreach (var productType in matchedIonsGroupedByProductType)
            {
                var products = productType.OrderBy(p => p.NeutralTheoreticalProduct.FragmentNumber)
                    .ToList();

                stringBuilders.ForEach(p => p.Append("["));

                for (int i = 0; i < products.Count; i++)
                {
                    MatchedFragmentIon ion = products[i];
                    string ionLabel;

                    double massError = ion.Mz.ToMass(ion.Charge) - ion.NeutralTheoreticalProduct.NeutralMass;
                    double ppmMassError = massError / ion.NeutralTheoreticalProduct.NeutralMass * 1e6;

                    ionLabel = ion.Annotation;

                    // append ion label
                    seriesStringBuilder.Append(ionLabel);

                    // append experimental m/z
                    mzStringBuilder.Append(ionLabel + ":" + ion.Mz.ToString("F5"));

                    // append absolute mass error
                    fragmentDaErrorStringBuilder.Append(ionLabel + ":" + massError.ToString("F5"));

                    // append ppm mass error
                    fragmentPpmErrorStringBuilder.Append(ionLabel + ":" + ppmMassError.ToString("F2"));

                    // append fragment ion intensity
                    fragmentIntensityStringBuilder.Append(ionLabel + ":" + ion.Intensity.ToString("F0"));

                    // append delimiter ", "
                    if (i < products.Count - 1)
                    {
                        stringBuilders.ForEach(p => p.Append(delimiter));
                    }
                }

                // append product type delimiter
                stringBuilders.ForEach(p => p.Append("];"));
            }

            tsvStringBuilder.Append(MatchedFragmentIons.Count + this.delimiter);
            tsvStringBuilder.Append(seriesStringBuilder.ToString().TrimEnd(';') + this.delimiter);
            tsvStringBuilder.Append(mzStringBuilder.ToString().TrimEnd(';') + this.delimiter);
            tsvStringBuilder.Append(fragmentDaErrorStringBuilder.ToString().TrimEnd(';') + this.delimiter);
            tsvStringBuilder.Append(fragmentPpmErrorStringBuilder.ToString().TrimEnd(';') + this.delimiter);
            tsvStringBuilder.Append(fragmentIntensityStringBuilder.ToString().TrimEnd(';') + this.delimiter);
            tsvStringBuilder.Append(SidEnergy + this.delimiter);

            return tsvStringBuilder.ToString();
        }

        public static string TsvHeader
        {
            get
            {
                List<string> strings = new()
                {
                    PsmTsvHeader.FileName,
                    PsmTsvHeader.Ms2ScanNumber,
                    PsmTsvHeader.Ms2ScanRetentionTime,
                    PsmTsvHeader.PrecursorScanNum,
                    PsmTsvHeader.PrecursorCharge,
                    PsmTsvHeader.PrecursorMz,
                    PsmTsvHeader.PrecursorMass,
                    PsmTsvHeader.Score,
                    PsmTsvHeader.BaseSequence,
                    PsmTsvHeader.MsnOrder,
                    PsmTsvHeader.MatchedIonCounts,
                    PsmTsvHeader.MatchedIonSeries,
                    PsmTsvHeader.MatchedIonMzRatios,
                    PsmTsvHeader.MatchedIonMassDiffDa,
                    PsmTsvHeader.MatchedIonMassDiffPpm,
                    PsmTsvHeader.MatchedIonIntensities,
                    PsmTsvHeader.SidEnergy,

                };
                return string.Join('\t', strings);
            }
        }


        /// <summary>
        /// Removes enclosing brackets and
        /// replaces delimimiters between ion series with comma
        /// then splits on comma
        /// </summary>
        /// <param name="input"> String containing ion series from .psmtsv </param>
        /// <returns> List of strings, with each entry containing one ion and associated property </returns>
        private static List<string> CleanMatchedIonString(string input)
        {
            List<string> ionProperty = input.Substring(1, input.Length - 2)
                .Replace("];[", ", ")
                .Split(", ")
                .ToList();
            ionProperty.RemoveAll(p => p.Contains("\"") || p.Equals(""));
            return ionProperty;
        }
        private static readonly Regex IonParser = new Regex(@"([a-zA-Z]+)(\d+)");
        private static List<MatchedFragmentIon> ReadFragmentIonsFromString(string matchedMzString, string matchedIntensityString, string peptideBaseSequence, string matchedMassErrorDaString = null)
        {
            List<MatchedFragmentIon> matchedIons = new List<MatchedFragmentIon>();

            if (matchedMzString.Length > 2) //check if there's an ion
            {
                List<string> peakMzs = CleanMatchedIonString(matchedMzString);
                List<string> peakIntensities = CleanMatchedIonString(matchedIntensityString);
                List<string> peakMassErrorDa = null;

                if (matchedMassErrorDaString.IsNotNullOrEmpty())
                {
                    peakMassErrorDa = CleanMatchedIonString(matchedMassErrorDaString);
                }

                for (int index = 0; index < peakMzs.Count; index++)
                {
                    string peak = peakMzs[index];
                    string[] split = peak.Split(new char[] { '+', ':' }); //TODO: needs update for negative charges that doesn't break internal fragment ions or neutral losses

                    // if there is a mismatch between the number of peaks and number of intensities from the psmtsv, the intensity will be set to 1
                    double intensity = peakMzs.Count == peakIntensities.Count ? //TODO: needs update for negative charges that doesn't break internal fragment ions or neutral losses
                        double.Parse(peakIntensities[index].Split(new char[] { '+', ':', ']' })[2], CultureInfo.InvariantCulture) :
                        1.0;

                    int fragmentNumber = 0;
                    int secondaryFragmentNumber = 0;
                    ProductType productType;
                    ProductType? secondaryProductType = null;
                    FragmentationTerminus terminus = FragmentationTerminus.None; //default for internal fragments
                    int aminoAcidPosition;
                    double neutralLoss = 0;

                    //get theoretical fragment
                    string ionTypeAndNumber = split[0];

                    //if an internal fragment
                    if (ionTypeAndNumber.Contains("["))
                    {
                        // if there is no mismatch between intensity and peak counts from the psmtsv
                        if (!intensity.Equals(1.0))
                        {
                            intensity = double.Parse(peakIntensities[index].Split(new char[] { '+', ':', ']' })[3],
                                CultureInfo.InvariantCulture);
                        }
                        string[] internalSplit = split[0].Split('[');
                        string[] productSplit = internalSplit[0].Split("I");
                        string[] positionSplit = internalSplit[1].Replace("]", "").Split('-');
                        productType = (ProductType)Enum.Parse(typeof(ProductType), productSplit[0]);
                        secondaryProductType = (ProductType)Enum.Parse(typeof(ProductType), productSplit[1]);
                        fragmentNumber = int.Parse(positionSplit[0]);
                        secondaryFragmentNumber = int.Parse(positionSplit[1]);
                        aminoAcidPosition = secondaryFragmentNumber - fragmentNumber;
                    }
                    else //terminal fragment
                    {
                        Match result = IonParser.Match(ionTypeAndNumber);
                        productType = (ProductType)Enum.Parse(typeof(ProductType), result.Groups[1].Value);
                        fragmentNumber = int.Parse(result.Groups[2].Value);
                        
                        // check for neutral loss  
                        if (ionTypeAndNumber.Contains("("))
                        {
                            string temp = ionTypeAndNumber.Replace("(", "");
                            temp = temp.Replace(")", "");
                            var split2 = temp.Split('-');
                            neutralLoss = double.Parse(split2[1], CultureInfo.InvariantCulture);
                        }

                        //get terminus
                        if (TerminusSpecificProductTypes.ProductTypeToFragmentationTerminus.ContainsKey(productType))
                        {
                            terminus = TerminusSpecificProductTypes.ProductTypeToFragmentationTerminus[productType];
                        }

                        //get amino acid position
                        aminoAcidPosition = terminus == FragmentationTerminus.C ?
                            peptideBaseSequence.Split('|')[0].Length - fragmentNumber :
                            fragmentNumber;
                    }

                    //get mass error in Daltons
                    double errorDa = 0;
                    if (matchedMassErrorDaString.IsNotNullOrEmpty() && peakMassErrorDa[index].IsNotNullOrEmpty())
                    {
                        string peakError = peakMassErrorDa[index];
                        string[] errorSplit = peakError.Split(new char[] { '+', ':', ']' });
                        errorDa = double.Parse(errorSplit[2], CultureInfo.InvariantCulture);
                    }

                    //get charge and mz
                    int z = int.Parse(split[1]);
                    double mz = double.Parse(split[2], CultureInfo.InvariantCulture);
                    double neutralExperimentalMass = mz.ToMass(z); //read in m/z converted to mass
                    double neutralTheoreticalMass = neutralExperimentalMass - errorDa; //theoretical mass is measured mass - measured error

                    //The product created here is the theoretical product, with the mass back-calculated from the measured mass and measured error
                    Product theoreticalProduct = new Product(productType,
                      terminus,
                      neutralTheoreticalMass,
                      fragmentNumber,
                      aminoAcidPosition,
                      neutralLoss,
                      secondaryProductType,
                      secondaryFragmentNumber);

                    matchedIons.Add(new MatchedFragmentIon(ref theoreticalProduct, mz, intensity, z));
                }
            }
            return matchedIons;
        }

        public static void Export(List<OligoSpectralMatch> matches, string outpath)
        {
            using (var sw = new StreamWriter(File.Create(outpath)))
            {
                sw.WriteLine(OligoSpectralMatch.TsvHeader);
                foreach (var match in matches)
                {
                    sw.WriteLine(match.ToTsvString());
                }
            }
        }

        public static List<OligoSpectralMatch> Import(string filePath, out List<string> warnings)
        {
            List<OligoSpectralMatch> osms = new List<OligoSpectralMatch>();
            warnings = new List<string>();

            StreamReader reader = null;
            try
            {
                reader = new StreamReader(filePath);
            }
            catch (Exception e)
            {
                throw new MetaMorpheusException("Could not read file: " + e.Message);
            }

            int lineCount = 0;

            string line;
            Dictionary<string, int> parsedHeader = null;

            while (reader.Peek() > 0)
            {
                lineCount++;

                line = reader.ReadLine();

                if (lineCount == 1)
                {
                    parsedHeader = PsmTsvReader.ParseHeader(line);
                    continue;
                }

                //try
                //{
                    osms.Add(new OligoSpectralMatch(line, parsedHeader));
                //}
                //catch (Exception e)
                //{
                //    warnings.Add("Could not read line: " + lineCount);
                //}
            }

            reader.Close();

            if ((lineCount - 1) != osms.Count)
            {
                warnings.Add("Warning: " + ((lineCount - 1) - osms.Count) + " PSMs were not read.");
            }

            return osms;
        }

        private int GetSidEnergy(string scanFilter)
        {
            var sidIndex = scanFilter.IndexOf("sid=");
            if (sidIndex == -1) return 0;


            var fullIndex = scanFilter.IndexOf("Full ms");

            var sub = scanFilter.Substring(sidIndex + 4, fullIndex - sidIndex);
            var resultString = Regex.Match(sub, @"\d+").Value;
            return int.Parse(resultString);
        }

        public override string ToString()
        {
            return $"{ScanNumber}:{MatchedFragmentIons.Count}:{MsnOrder}:{SidEnergy}";
        }

        /// <summary>
        /// Determines fragment coverage for the OSM
        /// Assigns fragment coverage indicies for the OSM and the oligo based on the Residue position in Matched Ion Fragments
        /// </summary>
        public void GetSequenceCoverage()
        {
            // Not ambiguous and has matched ions
            if (string.IsNullOrEmpty(BaseSequence) || !MatchedFragmentIons.Any())
                return;

            // pull 5' and 3' terminal fragments and amino acid numbers
            var fivePrimeFragmentAAPositions = MatchedFragmentIons.Where(ion =>
                    ion.NeutralTheoreticalProduct.Terminus == FragmentationTerminus.FivePrime)
                .Select(ion => ion.NeutralTheoreticalProduct.AminoAcidPosition)
                .Distinct()
                .ToList();
            var threePrimeFragmentAAPositions = MatchedFragmentIons.Where(ion =>
                    ion.NeutralTheoreticalProduct.Terminus == FragmentationTerminus.ThreePrime)
                .Select(ion => ion.NeutralTheoreticalProduct.AminoAcidPosition)
                .Distinct()
                .ToList();

            GetCoverage(fivePrimeFragmentAAPositions, threePrimeFragmentAAPositions);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chemistry;
using MassSpectrometry;
using Proteomics.Fragmentation;

namespace EngineLayer
{
    public class OligoSpectralMatch
    {
        public MsDataScan MsDataScan { get; set; }
        public string BaseSequence { get; private set; }
        public List<MatchedFragmentIon> MatchedFragmentIons { get; protected set; }
        public int ScanNumber => MsDataScan.OneBasedScanNumber;
        public string FilePath { get; protected set; }


        public OligoSpectralMatch(MsDataScan scan, string baseSequence,
            List<MatchedFragmentIon> matchedFragmentIons, string filePath)
        {
            MsDataScan = scan;
            BaseSequence = baseSequence;
            MatchedFragmentIons = matchedFragmentIons;
            FilePath = filePath;
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
            tsvStringBuilder.Append(MsDataScan.OneBasedScanNumber + this.delimiter);
            tsvStringBuilder.Append(BaseSequence + this.delimiter);

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


            return tsvStringBuilder.ToString();
        }

        public static string TsvHeader =>
            "FileName\tScan Number\tBase Sequences\tMatched Ions\tMatchedIonSeries\tMatchedIonMzRatios\tMatchedIonMassDiffDa\tMatchedIonMassDiffPpm\tMatchedIonIntensities";

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
    }
}

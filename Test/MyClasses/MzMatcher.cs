using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chemistry;
using Easy.Common.Extensions;
using EngineLayer;
using IO.ThermoRawFileReader;
using MassSpectrometry;
using MzLibUtil;
using Proteomics.ProteolyticDigestion;

namespace Test
{
    public class MzMatcher
    {
        private static int minChargeState = 1;
        private static int maxChargeState = 50;
        private static PpmTolerance tolerance = new(50);
        private static double relativeIntensityCutoff = 0.2;


        public string Accession { get; set; }
        public List<MsDataScan> Ms1Scans { get; set; }
        public Dictionary<int, double> ChargeStates { get; set; }
        public Dictionary<int, double> ScoredChargeStates { get; set; }


        public MzMatcher(string accession, List<MsDataScan> scans, List<PsmFromTsv> psms)
        {
            Accession = accession;
            Ms1Scans = scans.Where(p => p.MsnOrder == 1).ToList();

            var representativePsm = psms.Where(p => p.ProteinAccession == Accession && p.AmbiguityLevel == "1")
                .MaxBy(p => p.PsmCount);
            ChargeStates = GetMzValues(representativePsm);
        }

        private Dictionary<int, double> GetMzValues(PsmFromTsv psm)
        {
            var pep = new PeptideWithSetModifications(psm.FullSequence, GlobalVariables.AllModsKnownDictionary);
            Dictionary<int, double> mzs = new();
            for (int i = minChargeState; i < maxChargeState; i++)
            {
                mzs.Add(i, pep.MostAbundantMonoisotopicMass.ToMz(i));
            }
            return mzs;
        }

        public void ScoreScans()
        {
            Dictionary<int, double> chargeStateScores = ChargeStates.ToDictionary
                (p => p.Key, p => 0.0);
            int scanCount = Ms1Scans.Count;

            foreach (var spectra in Ms1Scans.Select(p => p.MassSpectrum))
            {
                var yCutoff = spectra.YArray.Max() * relativeIntensityCutoff;
                foreach (var chargeState in ChargeStates)
                {
                    // relative intensity
                    // TODO: change relative intensity to noise level
                    var peaksWithinTolerance = spectra.XArray.Where(p =>
                        tolerance.Within(p, chargeState.Value)).ToList();

                    var peaksWithinToleranceAboveCutoff =
                        peaksWithinTolerance.Where(p => spectra.YArray[spectra.XArray.IndexOf(p)] >= yCutoff).ToList();

                    if (peaksWithinToleranceAboveCutoff.Any())
                        chargeStateScores[chargeState.Key] += 1;
                }
            }

            // normalize counts to number of spectra
            for (int i = minChargeState; i < maxChargeState; i++)
            {
                chargeStateScores[i] /= scanCount;
            }

            ScoredChargeStates = chargeStateScores;
        }

    }
}

using System.Collections.Generic;
using UsefulProteomicsDatabases;
using EngineLayer;
using Omics.Modifications;
using Proteomics;

namespace TaskLayer
{
    public class SearchParameters : SearchParameterParent
    {
        public SearchParameters()
        {
            // default search task parameters
            DoParsimony = true;
            NoOneHitWonders = false;
            ModPeptidesAreDifferent = false;
            DoLabelFreeQuantification = true;
            DoSpectralRecovery = false;
            QuantifyPpmTol = 5;
            SearchTarget = true;
            DoHistogramAnalysis = false;
            HistogramBinTolInDaltons = 0.003;
            WritePrunedDatabase = false;
            KeepAllUniprotMods = true;
            MaxFragmentSize = 30000.0;
            MinAllowedInternalFragmentLength = 0;
            WriteMzId = true;
            WritePepXml = false;
            IncludeModMotifInMzid = false;

            ModsToWriteSelection = new Dictionary<string, int>
            {
                //Key is modification type.

                //Value is integer 0, 1, 2 and 3 interpreted as:
                //   0:   Do not Write
                //   1:   Write if in DB and Observed
                //   2:   Write if in DB
                //   3:   Write if Observed

                {"N-linked glycosylation", 3},
                {"O-linked glycosylation", 3},
                {"Other glycosylation", 3},
                {"Common Biological", 3},
                {"Less Common", 3},
                {"Metal", 3},
                {"2+ nucleotide substitution", 3},
                {"1 nucleotide substitution", 3},
                {"UniProt", 2},
            };

            
            LocalFdrCategories = new List<FdrCategory> { FdrCategory.FullySpecific };
            TCAmbiguity = TargetContaminantAmbiguity.RemoveContaminant;
        }

        
        public bool ModPeptidesAreDifferent { get; set; }
        public bool NoOneHitWonders { get; set; }
        public bool MatchBetweenRuns { get; set; }
        public bool Normalize { get; set; }
        public double QuantifyPpmTol { get; set; }
        public bool SearchTarget { get; set; }
        public bool WritePrunedDatabase { get; set; }
        public bool KeepAllUniprotMods { get; set; }
        public bool DoLabelFreeQuantification { get; set; }
        public bool DoMultiplexQuantification { get; set; }
        public string MultiplexModId { get; set; }
        public bool DoSpectralRecovery { get; set; }
        public SearchType SearchType { get; set; }
        public List<FdrCategory> LocalFdrCategories { get; set; }
        public double MaxFragmentSize { get; set; }
        public int MinAllowedInternalFragmentLength { get; set; } //0 means "no internal fragments"

        public double MaximumMassThatFragmentIonScoreIsDoubled { get; set; }
        public bool WriteMzId { get; set; }
        public bool WritePepXml { get; set; }

        public bool WriteSpectralLibrary { get; set; }
        public bool UpdateSpectralLibrary { get; set; }
        public List<SilacLabel> SilacLabels { get; set; }
        public SilacLabel StartTurnoverLabel { get; set; } //used for SILAC turnover experiments
        public SilacLabel EndTurnoverLabel { get; set; } //used for SILAC turnover experiments
        public TargetContaminantAmbiguity TCAmbiguity { get; set; }
        public bool IncludeModMotifInMzid { get; set; }
    }
}
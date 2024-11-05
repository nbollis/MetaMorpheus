using System.Collections.Generic;
using UsefulProteomicsDatabases;

namespace TaskLayer;

public abstract class SearchParameterParent
{
    protected SearchParameterParent()
    {
        DisposeOfFileWhenDone = true;
        MassDiffAcceptorType = MassDiffAcceptorType.OneMM;
        DecoyType = DecoyType.Reverse;
        CustomMdac = null;

        // Quant
        DoLabelFreeQuantification = true;
        QuantifyPpmTol = 5;

        // Post Search Analysis
        DoLocalizationAnalysis = true;
        DoHistogramAnalysis = false;
        HistogramBinTolInDaltons = 0.003;

        // Parsimony
        DoParsimony = true;
        NoOneHitWonders = false;
        ModPeptidesAreDifferent = false;

        // Output
        WriteHighQValueSpectralMatches = true;
        WriteDecoys = true;
        WriteContaminants = true;
        WriteAmbiguous = true;
        WriteIndividualFiles = true;
        CompressIndividualFiles = false;
    }

    public bool DisposeOfFileWhenDone { get; set; }

    public DecoyType DecoyType { get; set; }
    public MassDiffAcceptorType MassDiffAcceptorType { get; set; }
    public string CustomMdac { get; set; }

    #region Parsimony
    public bool DoParsimony { get; set; }
    public bool ModPeptidesAreDifferent { get; set; }
    public bool NoOneHitWonders { get; set; }

    #endregion


    #region Quantification

    public bool DoLabelFreeQuantification { get; set; }
    public bool MatchBetweenRuns { get; set; }
    public bool Normalize { get; set; }
    public double QuantifyPpmTol { get; set; }

    #endregion

    #region Output

    public bool DoLocalizationAnalysis { get; set; }
    public double HistogramBinTolInDaltons { get; set; }
    public bool DoHistogramAnalysis { get; set; }
    public bool CompressIndividualFiles { get; set; }
    public bool WriteHighQValueSpectralMatches { get; set; }
    public bool WriteDecoys { get; set; }
    public bool WriteContaminants { get; set; }
    public bool WriteAmbiguous { get; set; }
    public bool WriteIndividualFiles { get; set; }
    public Dictionary<string, int> ModsToWriteSelection { get; set; }

    #endregion
}
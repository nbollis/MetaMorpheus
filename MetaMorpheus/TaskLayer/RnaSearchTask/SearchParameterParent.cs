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

        DoLocalizationAnalysis = true;
        WriteHighQValueSpectralMatches = true;
        WriteDecoys = true;
        WriteContaminants = true;
        WriteAmbiguous = true;
        WriteIndividualFiles = true;
        DoHistogramAnalysis = false;
        CompressIndividualFiles = false;
        HistogramBinTolInDaltons = 0.003;
    }

    public bool DisposeOfFileWhenDone { get; set; }

    public DecoyType DecoyType { get; set; }
    public MassDiffAcceptorType MassDiffAcceptorType { get; set; }
    public string CustomMdac { get; set; }
    public bool DoParsimony { get; set; }

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
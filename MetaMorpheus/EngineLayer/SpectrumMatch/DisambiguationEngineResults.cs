using System.Text;

namespace EngineLayer.SpectrumMatch;

public class DisambiguationEngineResults : MetaMorpheusEngineResults
{
    public int TotalRemoved => RemovedByQValueNotch + RemovedByParentModificationPreference;
    //public int RemovedByPEP { get; set; }
    public int RemovedByQValueNotch { get; set; }
    public int RemovedByParentModificationPreference { get; set; }
    //public int RemovedByInternalIonCount { get; set; }

    public DisambiguationEngineResults(DisambiguationEngine s) : base(s)
    {
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine(base.ToString());
        //sb.AppendLine($"Ambiguous {GlobalVariables.AnalyteType.GetUniqueFormLabel()}s removed by PEP: {RemovedByPEP}");
        sb.AppendLine($"Ambiguous {GlobalVariables.AnalyteType.GetUniqueFormLabel()}s removed QValueNotch: {RemovedByQValueNotch}");
        sb.AppendLine($"Ambiguous {GlobalVariables.AnalyteType.GetUniqueFormLabel()}s removed by parent modification preference: {RemovedByParentModificationPreference}");
        //sb.AppendLine($"Ambiguous {GlobalVariables.AnalyteType.GetUniqueFormLabel()}s removed Internal Ion Count: {RemovedByInternalIonCount}");
        sb.AppendLine($"Ambiguous {GlobalVariables.AnalyteType.GetUniqueFormLabel()}s removed in total: {RemovedByQValueNotch}");
        return sb.ToString();
    }
}

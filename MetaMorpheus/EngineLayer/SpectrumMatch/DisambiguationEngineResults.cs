using System;
using System.Text;

namespace EngineLayer.SpectrumMatch;

public class DisambiguationEngineResults : MetaMorpheusEngineResults
{
    private DisambiguationEngine.DisambiguationMethod[] AppliedMethods { get; init; }

    public int TotalRemoved => RemovedByQValueNotch + RemovedByParentModificationPreference;
    //public int RemovedByPEP { get; set; }
    public int RemovedByQValueNotch { get; set; }
    public int RemovedByParentModificationPreference { get; set; }
    //public int RemovedByInternalIonCount { get; set; }

    public DisambiguationEngineResults(DisambiguationEngine s, DisambiguationEngine.DisambiguationMethod[] appliedMethods) : base(s)
    {
        AppliedMethods = appliedMethods;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine(base.ToString());

        foreach (var method in AppliedMethods)
        {
            switch (method)
            {
                case DisambiguationEngine.DisambiguationMethod.QValueNotch:
                    sb.AppendLine($"Ambiguous {GlobalVariables.AnalyteType.GetUniqueFormLabel()}s removed QValueNotch: {RemovedByQValueNotch}");
                    break;

                case DisambiguationEngine.DisambiguationMethod.ParentModificationPreference: 
                    sb.AppendLine($"Ambiguous {GlobalVariables.AnalyteType.GetUniqueFormLabel()}s removed by parent modification preference: {RemovedByParentModificationPreference}");
                    break;

                //case DisambiguationEngine.DisambiguationMethod.PEP:
                //    sb.AppendLine($"Ambiguous {GlobalVariables.AnalyteType.GetUniqueFormLabel()}s removed by PEP: {RemovedByPEP}");
                //    break;

                //case DisambiguationEngine.DisambiguationMethod.InternalFragmentIons:
                //    sb.AppendLine($"Ambiguous {GlobalVariables.AnalyteType.GetUniqueFormLabel()}s removed Internal Ion Count: {RemovedByInternalIonCount}");
                //    break;

                default:
                    throw new ArgumentException($"Disambiguation Method {method} writing not yet supported");

            }

        }

        sb.AppendLine($"Ambiguous {GlobalVariables.AnalyteType.GetUniqueFormLabel()}s removed in total: {TotalRemoved}");
        return sb.ToString();
    }
}

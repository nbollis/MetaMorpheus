using Omics;
using Omics.Fragmentation;
using Proteomics.ProteolyticDigestion;
using System.Collections.Generic;
using System.Text;

namespace EngineLayer.Indexing
{
    public class IndexingResults : MetaMorpheusEngineResults
    {
        public IndexingResults(List<IBioPolymerWithSetMods> digestionProductIndex, List<int>[] fragmentIndex, List<int>[] precursorIndex, IndexingEngine indexParams) : base(indexParams)
        {
            DigestionProductIndex = digestionProductIndex;
            FragmentIndex = fragmentIndex;
            PrecursorIndex = precursorIndex;
        }

        public List<int>[] FragmentIndex { get; private set; }
        public List<int>[] PrecursorIndex { get; private set; }
        public List<IBioPolymerWithSetMods> DigestionProductIndex { get; private set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(base.ToString());
            sb.AppendLine("\t\tfragmentIndexDict.Count: " + FragmentIndex.Length);
            if (PrecursorIndex != null)
            {
                sb.AppendLine("\t\tprecursorIndexDict.Count: " + PrecursorIndex.Length);
            }
            sb.AppendLine("\t\tpeptideIndex.Count: " + DigestionProductIndex.Count);
            return sb.ToString();
        }
    }
}
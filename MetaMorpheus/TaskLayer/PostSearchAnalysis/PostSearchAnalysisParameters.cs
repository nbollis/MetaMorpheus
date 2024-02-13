using System.Collections.Generic;
using FlashLFQ;
using Omics.Modifications;
using Proteomics;

namespace TaskLayer
{
    public class PostSearchAnalysisParameters : PostSearchAnalysisParametersParent
    {
        public new SearchParameters SearchParameters
        {
            get => (SearchParameters)base.SearchParameters;
            set => base.SearchParameters = value;
        }
        public new List<Protein> BioPolymerList { get; set; }
        public Modification MultiplexModification { get; set; }
        public FlashLfqResults FlashLfqResults { get; set; }
    }
}
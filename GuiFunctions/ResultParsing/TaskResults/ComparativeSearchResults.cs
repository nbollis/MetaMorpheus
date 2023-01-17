using EngineLayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GuiFunctions
{
    public struct ComparativeSearchResults
    {
        public List<PsmFromTsv> DistinctFilteredPsms { get; init; }
        public List<PsmFromTsv> DistinctFilteredProteins { get; init; }
        public List<PsmFromTsv> DistinctFilteredProteoforms { get; init; }

        public ComparativeSearchResults(IEnumerable<PsmFromTsv> psms, IEnumerable<PsmFromTsv> proteins,
            IEnumerable<PsmFromTsv> proteoforms)
        {
            DistinctFilteredPsms = psms.ToList();
            DistinctFilteredProteins = proteins.ToList();
            DistinctFilteredProteoforms = proteoforms.ToList();
        }
    }
}

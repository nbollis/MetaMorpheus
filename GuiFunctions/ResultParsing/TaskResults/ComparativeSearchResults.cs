using EngineLayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Easy.Common.Extensions;
using MzLibUtil;
using Proteomics;
using iText.Forms.Xfdf;

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

    public readonly struct SankeyLine
    {
        public string Source { get; init; }
        public string Target { get; init; }
        public double Value { get; init; }

        public SankeyLine(string source, string target, double value)
        {
            Source = source;
            Target = target;
            Value = value;
        }

        public override string ToString()
        {
            return $"{Source} [{Value}] {Target}";
        }
    }

    public readonly struct TdBuMatchComparison : ITsv
    {
        public string Name { get; init; }
        public int TdCount { get; init; }
        public int BuCount { get; init; }
        public int TdUnAmbiguousCount { get; init; }
        public int BuUnAmbiguousCount { get; init; }
        public int TdHitsInBottomUp { get; init; }
        public int BuHitsInTopDown { get; init; }
        public int MatchModAndLocation { get; init; }
        public int MatchLocationOnly { get; init; }
        public int MatchModOnly { get; init; }
        public int MatchNothing { get; init; }

        public TdBuMatchComparison(string name, int tdCount, int buCount, int tdUnambigCount, int buUnambigCount,int tdHitsinBu, int buHitsInTopDown, int modLoc, int loc, int mod, int none)
        {
            Name = name;
            TdCount = tdCount;
            BuCount = buCount;
            TdUnAmbiguousCount = tdUnambigCount;
            BuUnAmbiguousCount = buUnambigCount;
            TdHitsInBottomUp = tdHitsinBu;
            BuHitsInTopDown = buHitsInTopDown;
            MatchModAndLocation = modLoc;
            MatchLocationOnly = loc;
            MatchModOnly = mod;
            MatchNothing = none;
        }

        public string TabSeparatedHeader
        {
            get
            {
                var sb = new StringBuilder();
                sb.Append("Name\t");
                sb.Append("Top-Down\t");
                sb.Append("Bottom-Up\t");
                sb.Append("UnAmbiguous Td\t");
                sb.Append("UnAmbiguous Bu\t");
                sb.Append("Td Hits in Bu\t");
                sb.Append("Bu Hits in Td\t");
                sb.Append("Matched Mod and Location\t");
                sb.Append("Matched Location\t");
                sb.Append("Matched Mod\t");
                sb.Append("Match Nothing\t");

                var tsvString = sb.ToString().TrimEnd('\t');
                return tsvString;
            }
        }

        public string ToTsvString()
        {
            var sb = new StringBuilder();
            sb.Append($"{Name}\t");
            sb.Append($"{TdCount}\t");
            sb.Append($"{BuCount}\t");
            sb.Append($"{TdUnAmbiguousCount}\t");
            sb.Append($"{BuUnAmbiguousCount}\t");
            sb.Append($"{TdHitsInBottomUp}\t");
            sb.Append($"{BuHitsInTopDown}\t");
            sb.Append($"{MatchModAndLocation}\t");
            sb.Append($"{MatchLocationOnly}\t");
            sb.Append($"{MatchModOnly}\t");
            sb.Append($"{MatchNothing}\t");

            var tsvString = sb.ToString().TrimEnd('\t');
            return tsvString;
        }


    }
}

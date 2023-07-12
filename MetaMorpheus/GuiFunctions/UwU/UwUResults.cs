using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GuiFunctions
{
    public class UwUResults
    {
        public int TruncationSequences { get; set; }
        public int SpliceSequences { get; set; }
        public int AllSequences { get; set; }
        public int AllUniqueSequences { get; set; }
        public int AllPossibleSequences { get; set; }

        public record UwuResultsEntry(double MonoMass, double AverageMass, string Sequence, double MassDifference)
        {
            public override string ToString()
            {
                return $"{MassDifference},{MonoMass},{Sequence},{AverageMass}";
            }
        }

        public List<UwuResultsEntry> AllResults { get; }

        public UwUResults()
        {
            AllResults = new();
        }
    }
}

using System.Collections.Generic;

namespace MetaMorpheusGUI
{
    public class ScramblerResults
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

        public ScramblerResults()
        {
            AllResults = new();
        }
    }
}

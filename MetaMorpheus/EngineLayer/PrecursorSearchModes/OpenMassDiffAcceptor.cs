using MzLibUtil;
using System;
using System.Collections.Generic;

namespace EngineLayer
{
    public class OpenSearchMode() : MassDiffAcceptor("OpenSearch")
    {
        private static readonly AllowedIntervalWithNotch DefaultInterval = new(double.NegativeInfinity, double.PositiveInfinity, 0);

        public override int Accepts(double scanPrecursorMass, double peptideMass)
        {
            return 0;
        }

        public override IEnumerable<AllowedIntervalWithNotch> GetAllowedPrecursorMassIntervalsFromTheoreticalMass(double peptideMonoisotopicMass)
        {
            yield return DefaultInterval;
        }

        public override IEnumerable<AllowedIntervalWithNotch> GetAllowedPrecursorMassIntervalsFromObservedMass(double peptideMonoisotopicMass)
        {
            yield return DefaultInterval;
        }

        public override string ToProseString()
        {
            return ("unbounded");
        }

        public override string ToString()
        {
            return FileNameAddition + " OpenSearch";
        }
    }
}
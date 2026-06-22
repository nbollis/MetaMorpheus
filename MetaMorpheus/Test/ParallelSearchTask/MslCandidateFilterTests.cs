using Chemistry;
using NUnit.Framework;
using Priors = EngineLayer.ParallelSearch.MslPeptideReader.CandidatePriors;
using Reader = EngineLayer.ParallelSearch.MslPeptideReader;

namespace Test.ParallelSearchTask;

/// <summary>
/// Tests the .msl candidate pre-filter — the learned mass + RT gate that decides which library entries are
/// worth fragmenting/scoring — plus the merged-index "db|accession" parse and the lower-bound helper.
/// </summary>
[TestFixture]
public class MslCandidateFilterTests
{
    private const int Charge = 2;
    private const double Mz = 500.0;

    // Build priors whose scan list has exactly one mass-matching scan (the middle one) at a chosen RT.
    private static Priors PriorsWithMatch(double matchingScanRt, double tolPpm = 10, double rtWindowMin = 5,
        double rtSlope = 1, double rtIntercept = 0)
    {
        double neutral = Mz.ToMass(Charge);
        var scanMasses = new[] { neutral - 100.0, neutral, neutral + 100.0 }; // only the middle one is within tol
        var scanRts = new[] { 0.0, matchingScanRt, 0.0 };
        return new Priors(scanMasses, scanRts, tolPpm, rtSlope, rtIntercept, rtWindowMin);
    }

    [Test]
    public void IsCandidate_MassMatch_UnpredictedRt_KeepsOnMassAlone()
    {
        var priors = PriorsWithMatch(matchingScanRt: 20.0);
        // irt == 0 -> RT filter disabled; mass matches -> candidate.
        bool cand = Reader.IsCandidate(Mz, Charge, 0f, priors, out bool massMatched);
        Assert.That(cand, Is.True);
        Assert.That(massMatched, Is.True);
    }

    [Test]
    public void IsCandidate_NoMassMatch_NotCandidate()
    {
        var priors = PriorsWithMatch(matchingScanRt: 20.0);
        // m/z + 25 -> neutral + 50, which lands in the 100-Da gap between scan masses -> no mass match.
        bool cand = Reader.IsCandidate(Mz + 25.0, Charge, 0f, priors, out bool massMatched);
        Assert.That(cand, Is.False);
        Assert.That(massMatched, Is.False);
    }

    [Test]
    public void IsCandidate_MassMatch_RtInWindow_IsCandidate()
    {
        var priors = PriorsWithMatch(matchingScanRt: 20.0); // slope 1, intercept 0 -> predRt == iRT
        bool cand = Reader.IsCandidate(Mz, Charge, 20.0f, priors, out bool massMatched); // predRt 20 vs scan RT 20
        Assert.That(cand, Is.True);
        Assert.That(massMatched, Is.True);
    }

    [Test]
    public void IsCandidate_MassMatch_RtOutOfWindow_NotCandidate_ButMassMatched()
    {
        var priors = PriorsWithMatch(matchingScanRt: 20.0, rtWindowMin: 5);
        // predRt 50 vs the only mass-matching scan's RT 20 -> |50-20|=30 > 5 -> RT fails.
        bool cand = Reader.IsCandidate(Mz, Charge, 50.0f, priors, out bool massMatched);
        Assert.That(cand, Is.False);
        Assert.That(massMatched, Is.True, "mass matched even though RT did not");
    }

    [Test]
    public void IsCandidate_MassTolerance_BoundaryBehaviour()
    {
        double neutral = Mz.ToMass(Charge);
        // The effective window is (ppm * mass) + a 0.01 Da margin. A scan 0.03 Da away is outside a 10 ppm
        // window (~0.02 Da total) but inside a 40 ppm one (~0.05 Da total).
        double offset = 0.03;
        var priorsNarrow = new Priors(new[] { neutral + offset }, new[] { 0.0 }, precursorTolPpm: 10, rtSlope: 1, rtIntercept: 0, rtWindowMin: 5);
        Assert.That(Reader.IsCandidate(Mz, Charge, 0f, priorsNarrow, out _), Is.False);

        var priorsWide = new Priors(new[] { neutral + offset }, new[] { 0.0 }, precursorTolPpm: 40, rtSlope: 1, rtIntercept: 0, rtWindowMin: 5);
        Assert.That(Reader.IsCandidate(Mz, Charge, 0f, priorsWide, out _), Is.True);
    }

    [Test]
    public void LowerBound_ReturnsFirstIndexAtOrAboveValue()
    {
        var sorted = new[] { 1.0, 3.0, 5.0, 9.0 };
        Assert.Multiple(() =>
        {
            Assert.That(Reader.LowerBound(sorted, 0.0), Is.EqualTo(0));   // below all
            Assert.That(Reader.LowerBound(sorted, 3.0), Is.EqualTo(1));   // exact match
            Assert.That(Reader.LowerBound(sorted, 4.0), Is.EqualTo(2));   // between -> next-higher index
            Assert.That(Reader.LowerBound(sorted, 9.0), Is.EqualTo(3));   // exact last
            Assert.That(Reader.LowerBound(sorted, 10.0), Is.EqualTo(4));  // above all -> length
        });
    }

    [Test]
    public void ParseDbTagAndAccession_SplitsOnFirstBar()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Reader.ParseDbTagAndAccession("UP000464024|P0DTC9"), Is.EqualTo(("UP000464024", "P0DTC9")));
            // Only the FIRST bar splits; later bars stay in the accession.
            Assert.That(Reader.ParseDbTagAndAccession("db|acc|extra"), Is.EqualTo(("db", "acc|extra")));
            // No bar -> UNKNOWN db, whole string is the accession.
            Assert.That(Reader.ParseDbTagAndAccession("loneAccession"), Is.EqualTo(("UNKNOWN", "loneAccession")));
            // Empty accession after the bar -> "<db>_UNKNOWN".
            Assert.That(Reader.ParseDbTagAndAccession("db|"), Is.EqualTo(("db", "db_UNKNOWN")));
            // Null/empty input.
            Assert.That(Reader.ParseDbTagAndAccession(null), Is.EqualTo(("UNKNOWN", "UNKNOWN_UNKNOWN")));
        });
    }
}

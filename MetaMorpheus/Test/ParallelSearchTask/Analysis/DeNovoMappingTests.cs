using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Proteomics.ProteolyticDigestion;
using TaskLayer.ParallelSearch.Analysis;
using TaskLayer.ParallelSearch.Analysis.Collectors;
using UsefulProteomicsDatabases;

namespace Test.ParallelSearchTask.Analysis;

[TestFixture]
public class DeNovoMappingTests
{
    private string _folder = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(TestContext.CurrentContext.TestDirectory, "DeNovoMappingTests", TestContext.CurrentContext.Test.Name);
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, true);
        Directory.CreateDirectory(_folder);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, true);
    }

    private string WriteFasta(string name, params (string Accession, string Sequence)[] proteins)
    {
        string path = Path.Combine(_folder, name + ".fasta");
        File.WriteAllLines(path, proteins.SelectMany(p => new[] { $">sp|{p.Accession}|{p.Accession}_TEST Test protein OS=Test OX=1", p.Sequence }));
        return path;
    }

    /// <summary>A minimal Casanovo mzTab; each prediction has a retention time, so no spectra file is read.</summary>
    private string WriteMzTab(params (string Sequence, double Score)[] predictions)
    {
        string path = Path.Combine(_folder, "predictions.mztab");
        var lines = new List<string>
        {
            "MTD\tmzTab-version\t1.0.0",
            "MTD\tmzTab-mode\tSummary",
            "MTD\tmzTab-type\tIdentification",
            "MTD\tdescription\tCasanovo identification file predictions",
            "MTD\tsoftware[1]\t[MS, MS:1003281, Casanovo, 5.1.2]",
            "MTD\tpsm_search_engine_score[1]\t[MS, MS:1001143, search engine specific score for PSMs, ]",
            "MTD\tms_run[1]-location\tfile:///predictions.mgf",
            "PSH\tsequence\tPSM_ID\taccession\tunique\tdatabase\tdatabase_version\tsearch_engine\tsearch_engine_score[1]\tmodifications"
                + "\tretention_time\tcharge\texp_mass_to_charge\tcalc_mass_to_charge\tspectra_ref\tpre\tpost\tstart\tend\topt_ms_run[1]_aa_scores",
        };
        int id = 0;
        foreach (var (sequence, score) in predictions)
        {
            id++;
            string aaScores = string.Join(",", sequence.Select(_ => "0.90000"));
            lines.Add($"PSM\t{sequence}\t{id}\tnull\tnull\tnull\tnull\t[MS, MS:1003281, Casanovo, 5.1.2]\t{score}\tnull"
                + $"\t{60.0 * id}\t2\t500.0\t500.0\tms_run[1]:index={id - 1}\tnull\tnull\tnull\tnull\t{aaScores}");
        }
        File.WriteAllLines(path, lines);
        return path;
    }

    [Test]
    public void WriteMappingSummary_FromMzTab_IsReadByTheCollector()
    {
        // trypsin cuts ELVISLIVESK | AAAGGGTTTR; the prediction with Ls must map because I and L are treated as one
        string present = WriteFasta("Present", ("P00001", "ELVISLIVESKAAAGGGTTTR"));
        string absent = WriteFasta("Absent", ("P00002", "WWWWWWWHHHHHK"));
        string mzTab = WriteMzTab(("ELVLSLLVESK", 0.9), ("AAAGGGTTTR", 0.8), ("QQQQQQQQK", 0.7));

        var (summaryPath, _) = DeNovoMapping.WriteMappingSummary([present, absent], [mzTab], [],
            new DigestionParams(), DecoyType.Reverse, _folder);

        var collector = new DeNovoMappingCollector(summaryPath);
        var presentContext = new TransientDatabaseContext { DatabaseName = "Present" };
        var absentContext = new TransientDatabaseContext { DatabaseName = "Absent" };
        Assert.That(summaryPath, Is.EqualTo(Path.Combine(_folder, DeNovoMapping.SummaryFileName)));
        Assert.That(collector.CanCollectData(presentContext) && collector.CanCollectData(absentContext), Is.True);

        var metrics = new TransientDatabaseMetrics("Present") { Results = collector.CollectData(presentContext) };
        metrics.PopulatePropertiesFromResults();
        var miss = collector.CollectData(absentContext);
        Assert.Multiple(() =>
        {
            Assert.That(metrics.TargetPredictions, Is.EqualTo(2));
            Assert.That(metrics.UniquePeptidesMapped, Is.EqualTo(2));
            Assert.That(metrics.UniqueProteinsMapped, Is.EqualTo(1));
            Assert.That(metrics.TotalPredictions, Is.EqualTo(metrics.TargetPredictions + metrics.DecoyPredictions));
            Assert.That(metrics.TargetPredictionScores.OrderBy(s => s), Is.EqualTo(new[] { 0.8, 0.9 }).Within(1e-9));

            Assert.That(miss[DeNovoMappingCollector.TotalPredictions], Is.EqualTo(0));
            Assert.That(miss[DeNovoMappingCollector.UniquePeptidesMapped], Is.EqualTo(0));
        });
    }

    [Test]
    public void WriteMappingSummary_WithAHost_TestsOnlyThePredictionsTheHostDoesNotExplain()
    {
        // AAAGGGTTTR is in the database and in the host, so its prediction is host-ambiguous; ELVLSLLVESK is the database's own
        string present = WriteFasta("Present", ("P00001", "ELVISLIVESKAAAGGGTTTR"));
        string mzTab = WriteMzTab(("ELVLSLLVESK", 0.9), ("AAAGGGTTTR", 0.8));
        var host = new Proteomics.Protein("GGGGGKAAAGGGTTTRWWWWWWWK", "H00001", organism: "Homo sapiens");

        var (summaryPath, _) = DeNovoMapping.WriteMappingSummary([present], [mzTab], [],
            new DigestionParams(), DecoyType.Reverse, _folder, [host]);

        var collector = new DeNovoMappingCollector(summaryPath);
        var metrics = new TransientDatabaseMetrics("Present")
        {
            Results = collector.CollectData(new TransientDatabaseContext { DatabaseName = "Present" })
        };
        metrics.PopulatePropertiesFromResults();
        Assert.Multiple(() =>
        {
            Assert.That(metrics.DeNovoHasHost, Is.True);
            Assert.That(metrics.DeNovoHostProteinCount, Is.EqualTo(1));
            Assert.That(metrics.TargetPredictions, Is.EqualTo(2), "the totals still count every hit");
            Assert.That(metrics.UnambiguousTargetPredictions, Is.EqualTo(1));
            Assert.That(metrics.AmbiguousTargetPredictions, Is.EqualTo(1));
            Assert.That(metrics.UniqueUnambiguousPeptidesMapped, Is.EqualTo(1));
            Assert.That(metrics.DeNovoTestedTargets, Is.EqualTo(1));
            Assert.That(metrics.DeNovoTestedUniquePeptides, Is.EqualTo(1));
            Assert.That(metrics.DeNovoTestedPredictionScores, Is.EqualTo(new[] { 0.9 }).Within(1e-9));
            Assert.That(metrics.DeNovoTestedMeanPredictionScore, Is.EqualTo(0.9).Within(1e-9));
        });
    }

    [Test]
    public void WriteMappingSummary_WithoutAHost_TestsEveryPrediction()
    {
        string present = WriteFasta("Present", ("P00001", "ELVISLIVESKAAAGGGTTTR"));
        string mzTab = WriteMzTab(("ELVLSLLVESK", 0.9), ("AAAGGGTTTR", 0.8));

        var (summaryPath, _) = DeNovoMapping.WriteMappingSummary([present], [mzTab], [],
            new DigestionParams(), DecoyType.Reverse, _folder);

        var metrics = new TransientDatabaseMetrics("Present")
        {
            Results = new DeNovoMappingCollector(summaryPath).CollectData(new TransientDatabaseContext { DatabaseName = "Present" })
        };
        metrics.PopulatePropertiesFromResults();
        Assert.Multiple(() =>
        {
            Assert.That(metrics.DeNovoHasHost, Is.False);
            Assert.That(metrics.DeNovoTestedTargets, Is.EqualTo(2));
            Assert.That(metrics.DeNovoTestedUniquePeptides, Is.EqualTo(2));
            Assert.That(metrics.DeNovoTestedPredictionScores.OrderBy(s => s), Is.EqualTo(new[] { 0.8, 0.9 }).Within(1e-9));
        });
    }

    [Test]
    public void CollectorWithAMapper_MapsTheDatabasesProteins_LikeTheSummaryFile()
    {
        string present = WriteFasta("Present", ("P00001", "ELVISLIVESKAAAGGGTTTR"));
        string mzTab = WriteMzTab(("ELVLSLLVESK", 0.9), ("AAAGGGTTTR", 0.8), ("QQQQQQQQK", 0.7));
        var host = new Proteomics.Protein("GGGGGKAAAGGGTTTRWWWWWWWK", "H00001", organism: "Homo sapiens");
        var mapper = DeNovoMapping.CreateMapper([mzTab], [], new DigestionParams(), DecoyType.Reverse, [host]);

        // the file path: read from disk with decoys added, written to a summary, read back
        var (summaryPath, _) = DeNovoMapping.WriteMappingSummary(mapper, [present], _folder);
        var fromFile = new DeNovoMappingCollector(summaryPath).CollectData(new TransientDatabaseContext { DatabaseName = "Present" });

        // the search path: the same proteins, decoys included, as the search holds them
        var proteins = ProteinDbLoader.LoadProteinFasta(present, true, DecoyType.Reverse, false, out _).Cast<Omics.IBioPolymer>().ToList();
        var collector = new DeNovoMappingCollector(() => mapper);
        var context = new TransientDatabaseContext { DatabaseName = "Present", TransientProteins = proteins };
        Assert.That(collector.CanCollectData(context), Is.True);
        var fromProteins = collector.CollectData(context);

        Assert.Multiple(() =>
        {
            Assert.That(fromProteins.Keys, Is.EquivalentTo(fromFile.Keys));
            foreach (var (key, value) in fromFile)
                Assert.That(fromProteins[key], Is.EqualTo(value).Within(1e-4), key); // the summary file rounds to 4 decimals
            Assert.That(collector.MappedResults.Select(r => r.DatabaseIdentifier), Is.EqualTo(new[] { "Present" }));
            Assert.That(collector.CanCollectData(new TransientDatabaseContext { DatabaseName = "Empty", TransientProteins = [] }), Is.False);
        });
    }

    [Test]
    public void DeNovoTests_IncludeTheHostAmbiguityTest()
    {
        var tests = new TaskLayer.ParallelSearch.Statistics.TestSuiteBuilder().AddDeNovoTests().Build();

        Assert.That(tests.Select(t => t.MetricName), Does.Contain("DeNovo-HostAmbiguity"));
    }

    [Test]
    public void WriteMappingSummary_SkipsAMissingDatabaseFileWithAWarning()
    {
        string mzTab = WriteMzTab(("AAAGGGTTTR", 0.8));

        var (summaryPath, warnings) = DeNovoMapping.WriteMappingSummary([Path.Combine(_folder, "Gone.fasta")], [mzTab], [],
            new DigestionParams(), DecoyType.Reverse, _folder);

        Assert.That(warnings, Has.Some.Contains("Gone.fasta"));
        Assert.That(new DeNovoMappingCollector(summaryPath).CanCollectData(new TransientDatabaseContext { DatabaseName = "Gone" }), Is.False);
    }

    [Test]
    public void SummaryRowWithoutHostColumns_ReadsAsNoHost()
    {
        // a ManySearchSummary written before host ambiguity existed has no DeNovo_HostProteinCount
        var metrics = new TransientDatabaseMetrics("Db") { TotalPredictions = 2, TargetPredictions = 2, UniquePeptidesMapped = 2 };
        metrics.PopulateResultsFromProperties();
        metrics.Results.Remove(DeNovoMappingCollector.HostProteinCount);
        metrics.PopulatePropertiesFromResults();

        Assert.That(metrics.DeNovoHasHost, Is.False);
        Assert.That(metrics.DeNovoTestedTargets, Is.EqualTo(2));
    }

    [Test]
    public void DeNovoColumns_SurviveTheResultsAndPropertiesRoundTrip()
    {
        var metrics = new TransientDatabaseMetrics("Db")
        {
            TotalPredictions = 5, TargetPredictions = 4, DecoyPredictions = 1, UniquePeptidesMapped = 3, UniqueProteinsMapped = 2,
            DeNovoMeanRtError = 0.5, MeanPredictionScore = 0.7,
            DeNovoRetentionTimeErrors = [0.1, -0.2], PredictionScores = [0.9, 0.5], TargetPredictionScores = [0.9], DecoyPredictionScores = [0.5],
            DeNovoHostProteinCount = 7, UnambiguousTargetPredictions = 3, AmbiguousTargetPredictions = 1, UnambiguousDecoyPredictions = 1,
            UniqueUnambiguousPeptidesMapped = 2, UniqueUnambiguousProteinsMapped = 1,
            DeNovoUnambiguousRetentionTimeErrors = [0.1], UnambiguousTargetPredictionScores = [0.9], UnambiguousDecoyPredictionScores = [0.5],
        };

        metrics.PopulateResultsFromProperties();
        var copy = new TransientDatabaseMetrics("Db") { Results = new Dictionary<string, object>(metrics.Results) };
        copy.PopulatePropertiesFromResults();

        Assert.Multiple(() =>
        {
            Assert.That(copy.TotalPredictions, Is.EqualTo(5));
            Assert.That(copy.TargetPredictions, Is.EqualTo(4));
            Assert.That(copy.DecoyPredictions, Is.EqualTo(1));
            Assert.That(copy.UniquePeptidesMapped, Is.EqualTo(3));
            Assert.That(copy.UniqueProteinsMapped, Is.EqualTo(2));
            Assert.That(copy.DeNovoMeanRtError, Is.EqualTo(0.5));
            Assert.That(copy.MeanPredictionScore, Is.EqualTo(0.7));
            Assert.That(copy.DeNovoRetentionTimeErrors, Is.EqualTo(new[] { 0.1, -0.2 }));
            Assert.That(copy.PredictionScores, Is.EqualTo(new[] { 0.9, 0.5 }));
            Assert.That(copy.TargetPredictionScores, Is.EqualTo(new[] { 0.9 }));
            Assert.That(copy.DecoyPredictionScores, Is.EqualTo(new[] { 0.5 }));
            Assert.That(copy.DeNovoHostProteinCount, Is.EqualTo(7));
            Assert.That(copy.UnambiguousTargetPredictions, Is.EqualTo(3));
            Assert.That(copy.AmbiguousTargetPredictions, Is.EqualTo(1));
            Assert.That(copy.UnambiguousDecoyPredictions, Is.EqualTo(1));
            Assert.That(copy.UniqueUnambiguousPeptidesMapped, Is.EqualTo(2));
            Assert.That(copy.UniqueUnambiguousProteinsMapped, Is.EqualTo(1));
            Assert.That(copy.DeNovoUnambiguousRetentionTimeErrors, Is.EqualTo(new[] { 0.1 }));
            Assert.That(copy.UnambiguousTargetPredictionScores, Is.EqualTo(new[] { 0.9 }));
            Assert.That(copy.UnambiguousDecoyPredictionScores, Is.EqualTo(new[] { 0.5 }));
        });
    }
}

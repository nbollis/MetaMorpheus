using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Proteomics.ProteolyticDigestion;
using TaskLayer.ParallelSearch.Analysis;
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
    public void MapInto_FillsTheDeNovoColumnsOfEachDatabaseInTheSummary()
    {
        // trypsin cuts ELVISLIVESK | AAAGGGTTTR; the prediction with Ls must map because I and L are treated as one
        string present = WriteFasta("Present", ("P00001", "ELVISLIVESKAAAGGGTTTR"));
        string absent = WriteFasta("Absent", ("P00002", "WWWWWWWHHHHHK"));
        string notInSummary = WriteFasta("NotInSummary", ("P00003", "ELVISLIVESKAAAGGGTTTR"));
        string mzTab = WriteMzTab(("ELVLSLLVESK", 0.9), ("AAAGGGTTTR", 0.8), ("QQQQQQQQK", 0.7));

        var metrics = new Dictionary<string, TransientDatabaseMetrics>
        {
            ["Present"] = new("Present"),
            ["Absent"] = new("Absent"),
        };

        var (mapped, _) = DeNovoMapping.MapInto(metrics, [present, absent, notInSummary], [mzTab], [],
            new DigestionParams(), DecoyType.Reverse);

        var hit = metrics["Present"];
        var miss = metrics["Absent"];
        Assert.Multiple(() =>
        {
            Assert.That(mapped, Is.EqualTo(2), "only databases with a summary row are mapped");
            Assert.That(hit.TargetPredictions, Is.EqualTo(2));
            Assert.That(hit.UniquePeptidesMapped, Is.EqualTo(2));
            Assert.That(hit.UniqueProteinsMapped, Is.EqualTo(1));
            Assert.That(hit.TotalPredictions, Is.EqualTo(hit.TargetPredictions + hit.DecoyPredictions));
            Assert.That(hit.TargetPredictionScores.OrderBy(s => s), Is.EqualTo(new[] { 0.8, 0.9 }).Within(1e-9));
            Assert.That(hit.Results[DeNovoMapping.TargetPredictions], Is.EqualTo(2), "the results dictionary is filled too");

            Assert.That(miss.TotalPredictions, Is.EqualTo(0));
            Assert.That(miss.UniquePeptidesMapped, Is.EqualTo(0));
        });
    }

    [Test]
    public void MapInto_SkipsAMissingDatabaseFileWithAWarning()
    {
        string mzTab = WriteMzTab(("AAAGGGTTTR", 0.8));
        var metrics = new Dictionary<string, TransientDatabaseMetrics> { ["Gone"] = new("Gone") };

        var (mapped, warnings) = DeNovoMapping.MapInto(metrics, [Path.Combine(_folder, "Gone.fasta")], [mzTab], [],
            new DigestionParams(), DecoyType.Reverse);

        Assert.That(mapped, Is.EqualTo(0));
        Assert.That(warnings, Has.Some.Contains("Gone.fasta"));
    }

    [Test]
    public void DeNovoColumns_SurviveTheResultsAndPropertiesRoundTrip()
    {
        var metrics = new TransientDatabaseMetrics("Db")
        {
            TotalPredictions = 5, TargetPredictions = 4, DecoyPredictions = 1, UniquePeptidesMapped = 3, UniqueProteinsMapped = 2,
            DeNovoMeanRtError = 0.5, MeanPredictionScore = 0.7,
            DeNovoRetentionTimeErrors = [0.1, -0.2], PredictionScores = [0.9, 0.5], TargetPredictionScores = [0.9], DecoyPredictionScores = [0.5],
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
        });
    }
}

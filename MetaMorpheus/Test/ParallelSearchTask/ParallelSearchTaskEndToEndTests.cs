using System.Collections.Generic;
using System.IO;
using System.Linq;
using EngineLayer.DatabaseLoading;
using Nett;
using NUnit.Framework;
using TaskLayer;
using TaskLayer.ParallelSearch;
using TaskLayer.ParallelSearch.Twins;
using UsefulProteomicsDatabases;

namespace Test.ParallelSearchTask;

[TestFixture]
public class ParallelSearchTaskEndToEndTests
{
    private static readonly string TestDataFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData");
    private static readonly string SpectraPath = Path.Combine(TestDataFolder, "TaGe_SA_A549_3_snip.mzML");
    private static readonly string BaseDatabasePath = Path.Combine(TestDataFolder, "TaGe_SA_A549_3_snip.fasta");
    private static readonly string[] TransientDatabasePaths =
    [
        Path.Combine(TestDataFolder, "hela_snip_for_unitTest.fasta"),
        Path.Combine(TestDataFolder, "smalldb.fasta")
    ];

    private static TaskLayer.ParallelSearch.ParallelSearchTask CreateTask(bool overwrite)
    {
        var task = new TaskLayer.ParallelSearch.ParallelSearchTask(
            TransientDatabasePaths.Select(p => new DbForTask(p, false)).ToList());
        task.ParallelSearchParameters.OverwriteTransientSearchOutputs = overwrite;
        task.ParallelSearchParameters.MaxSearchesInParallel = 2;
        return task;
    }

    private static Dictionary<string, TaskLayer.ParallelSearch.Analysis.TransientDatabaseMetrics> ReadSummary(string outputFolder)
    {
        var cache = new ParallelSearchResultCache(
            Path.Combine(outputFolder, TransientDatabaseResultsManager.SummaryResultsFileName));
        cache.InitializeCache();
        return cache.AllResultsDictionary;
    }

    [Test]
    public void RunTask_WritesOneSummaryRowPerTransientDatabase_AndReusesThemOnRerun()
    {
        string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "ParallelSearchEndToEnd");
        if (Directory.Exists(outputFolder))
            Directory.Delete(outputFolder, true);
        Directory.CreateDirectory(outputFolder);

        try
        {
            var baseDatabases = new List<DbForTask> { new(BaseDatabasePath, false) };
            var spectra = new List<string> { SpectraPath };
            string[] expectedNames = TransientDatabasePaths.Select(Path.GetFileNameWithoutExtension).ToArray();

            CreateTask(overwrite: true).RunTask(outputFolder, baseDatabases, spectra, "parallel");

            string summaryPath = Path.Combine(outputFolder, TransientDatabaseResultsManager.SummaryResultsFileName);
            Assert.That(File.Exists(summaryPath), Is.True);

            string[] summaryLines = File.ReadAllLines(summaryPath);
            string[] header = summaryLines[0].Split(',');
            var firstRun = ReadSummary(outputFolder);

            Assert.Multiple(() =>
            {
                // header plus one row per transient database
                Assert.That(summaryLines.Length, Is.EqualTo(expectedNames.Length + 1));
                Assert.That(header, Does.Contain("DatabaseName"));
                Assert.That(header, Does.Contain("TransientProteinCount"));
                Assert.That(header, Does.Contain("TargetPsmsFromTransientDbAtQValueThreshold"));
                Assert.That(firstRun.Keys, Is.EquivalentTo(expectedNames));
                Assert.That(firstRun.Values.All(m => m.TransientProteinCount > 0), Is.True);
                Assert.That(firstRun.Values.All(m => m.TotalProteins > m.TransientProteinCount), Is.True);
                Assert.That(File.Exists(Path.Combine(outputFolder, "BasePSMs.psmtsv")), Is.True);
                foreach (string name in expectedNames)
                {
                    Assert.That(File.Exists(Path.Combine(outputFolder, name, $"{name}_AllPSMs.psmtsv")), Is.True);
                    Assert.That(File.Exists(Path.Combine(outputFolder, name, "results.txt")), Is.True);
                }
            });

            // Rerun without overwriting: every database is already in the summary, so nothing is searched again
            string perDatabaseFile = Path.Combine(outputFolder, expectedNames[0], $"{expectedNames[0]}_AllPSMs.psmtsv");
            var writeTimeBeforeRerun = File.GetLastWriteTimeUtc(perDatabaseFile);

            CreateTask(overwrite: false).RunTask(outputFolder, baseDatabases, spectra, "parallel");

            var secondRun = ReadSummary(outputFolder);
            Assert.Multiple(() =>
            {
                Assert.That(File.GetLastWriteTimeUtc(perDatabaseFile), Is.EqualTo(writeTimeBeforeRerun));
                Assert.That(File.ReadAllLines(summaryPath).Length, Is.EqualTo(expectedNames.Length + 1));
                Assert.That(secondRun.Keys, Is.EquivalentTo(expectedNames));
                foreach (string name in expectedNames)
                {
                    Assert.That(secondRun[name].TransientProteinCount, Is.EqualTo(firstRun[name].TransientProteinCount));
                    Assert.That(secondRun[name].TargetPsmsFromTransientDbAtQValueThreshold,
                        Is.EqualTo(firstRun[name].TargetPsmsFromTransientDbAtQValueThreshold));
                }
            });
        }
        finally
        {
            if (Directory.Exists(outputFolder))
                Directory.Delete(outputFolder, true);
        }
    }

    /// <summary>A minimal Casanovo mzTab; each prediction has a retention time, so no spectra file is read.</summary>
    private static string WriteMzTab(string folder, params string[] sequences)
    {
        string path = Path.Combine(folder, "predictions.mztab");
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
        for (int i = 0; i < sequences.Length; i++)
        {
            string aaScores = string.Join(",", sequences[i].Select(_ => "0.90000"));
            lines.Add($"PSM\t{sequences[i]}\t{i + 1}\tnull\tnull\tnull\tnull\t[MS, MS:1003281, Casanovo, 5.1.2]\t0.9\tnull"
                + $"\t{60.0 * (i + 1)}\t2\t500.0\t500.0\tms_run[1]:index={i}\tnull\tnull\tnull\tnull\t{aaScores}");
        }
        File.WriteAllLines(path, lines);
        return path;
    }

    [Test]
    public void RunTask_WithDeNovoMzTabs_CountsHostSharedPredictionsAsAmbiguous_WhetherSearchedOrCached()
    {
        // AAAEYEK is only in smalldb (yeast); AALTTLK is in the HeLa transient database and in the human base database,
        // so the host explains it too. The mapper is built at the end on the all-cached path and during the searches otherwise.
        string outputFolder = FreshOutputFolder("ParallelSearchDeNovoHost");
        string mzTabFolder = FreshOutputFolder("ParallelSearchDeNovoHost_mzTab");
        try
        {
            string mzTab = WriteMzTab(mzTabFolder, "AAAEYEK", "AALTTLK");
            var baseDatabases = new List<DbForTask> { new(BaseDatabasePath, false) };
            var spectra = new List<string> { SpectraPath };
            string small = Path.GetFileNameWithoutExtension(TransientDatabasePaths[1]);
            string hela = Path.GetFileNameWithoutExtension(TransientDatabasePaths[0]);

            void AssertHostCounts(string when)
            {
                var summary = ReadSummary(outputFolder);
                Assert.Multiple(() =>
                {
                    Assert.That(File.Exists(Path.Combine(outputFolder, TaskLayer.ParallelSearch.Analysis.DeNovoMapping.SummaryFileName)), Is.True, when);
                    Assert.That(summary[small].DeNovoHostProteinCount, Is.GreaterThan(0), when);
                    Assert.That(summary[small].UnambiguousTargetPredictions, Is.EqualTo(1), when);
                    Assert.That(summary[small].AmbiguousTargetPredictions, Is.EqualTo(0), when);
                    Assert.That(summary[hela].UnambiguousTargetPredictions, Is.EqualTo(0), when);
                    Assert.That(summary[hela].AmbiguousTargetPredictions, Is.EqualTo(1), when);
                    Assert.That(summary[hela].DeNovoTestedTargets, Is.EqualTo(0), when);
                });
            }

            var statuses = new List<string>();
            void OnStatus(object? sender, EngineLayer.StringEventArgs e) { lock (statuses) statuses.Add(e.S); }
            void RunWithDeNovo(bool overwrite)
            {
                statuses.Clear();
                MetaMorpheusTask.OutLabelStatusHandler += OnStatus;
                try
                {
                    var task = CreateTask(overwrite);
                    task.ParallelSearchParameters.DeNovoMzTabPaths = [mzTab];
                    task.RunTask(outputFolder, baseDatabases, spectra, "parallel");
                }
                finally
                {
                    MetaMorpheusTask.OutLabelStatusHandler -= OnStatus;
                }
            }
            bool MappedAfterTheSearches() => statuses.Any(s => s.Contains("not mapped during the searches"));

            // searched without de novo, then rerun with it: every database is cached, so they are mapped from their files at the end
            CreateTask(overwrite: true).RunTask(outputFolder, baseDatabases, spectra, "parallel");
            RunWithDeNovo(overwrite: false);
            AssertHostCounts("all databases cached");
            Assert.That(MappedAfterTheSearches(), Is.True, "cached rows without de novo are mapped at the end");

            // searched again with de novo: the mapper is built in the background and each database is mapped during its search
            RunWithDeNovo(overwrite: true);
            AssertHostCounts("searched");
            int background = statuses.FindIndex(s => s.StartsWith("Preparing de novo predictions in the background"));
            int searching = statuses.FindIndex(s => s.StartsWith("Starting search of"));
            Assert.Multiple(() =>
            {
                Assert.That(background, Is.GreaterThanOrEqualTo(0).And.LessThan(searching), "the mapper must start before the transient searches");
                Assert.That(MappedAfterTheSearches(), Is.False, "every searched database is mapped during its search");
            });
        }
        finally
        {
            if (Directory.Exists(outputFolder))
                Directory.Delete(outputFolder, true);
            if (Directory.Exists(mzTabFolder))
                Directory.Delete(mzTabFolder, true);
        }
    }

    private static string FreshOutputFolder(string name)
    {
        string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, name);
        if (Directory.Exists(outputFolder))
            Directory.Delete(outputFolder, true);
        Directory.CreateDirectory(outputFolder);
        return outputFolder;
    }

    [Test]
    public void RunTask_WithEveryOutputEnabled_WritesAllAndTransientFilesAndCompressesThem()
    {
        // The spectra come from the A549 database, so searching it as a transient database
        // gives transient identifications and transient protein groups.
        string outputFolder = FreshOutputFolder("ParallelSearchAllOutputs");
        try
        {
            string a549 = Path.GetFileNameWithoutExtension(BaseDatabasePath);
            TaskLayer.ParallelSearch.ParallelSearchTask CreateCompressingTask()
            {
                var task = new TaskLayer.ParallelSearch.ParallelSearchTask(
                [
                    new DbForTask(BaseDatabasePath, false),
                    new DbForTask(TransientDatabasePaths[1], false)
                ]);
                task.ParallelSearchParameters.WriteTransientResultsOnly = false;
                task.ParallelSearchParameters.WriteSpectralLibrary = true;
                task.ParallelSearchParameters.WriteTransientSpectralLibrary = true;
                task.ParallelSearchParameters.CompressTransientSearchOutputs = true;
                return task;
            }

            var task = CreateCompressingTask();
            task.RunTask(outputFolder, [new DbForTask(TransientDatabasePaths[0], false)], [SpectraPath], "parallel");

            // rerun with the summary gone: every database is searched again into a folder that still holds the
            // first run's .gz files, and compressing the fresh outputs must not delete them
            File.Delete(Path.Combine(outputFolder, TransientDatabaseResultsManager.SummaryResultsFileName));
            CreateCompressingTask().RunTask(outputFolder, [new DbForTask(TransientDatabasePaths[0], false)], [SpectraPath], "parallel");

            var summary = ReadSummary(outputFolder);
            string databaseFolder = Path.Combine(outputFolder, a549);
            string[] writtenFiles = Directory.GetFiles(databaseFolder).Select(Path.GetFileName).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(summary.Keys, Has.Count.EqualTo(2));
                Assert.That(summary[a549].TargetPsmsFromTransientDbAtQValueThreshold, Is.GreaterThan(0));
                Assert.That(summary[a549].TargetProteinGroupsFromTransientDb, Is.GreaterThan(0));
                Assert.That(task.TotalMs2Scans, Is.GreaterThan(0));

                // every output is compressed in place
                Assert.That(writtenFiles, Has.Member($"{a549}_AllPSMs.psmtsv.gz"));
                Assert.That(writtenFiles, Has.Member("AllPSMs.psmtsv.gz"));
                Assert.That(writtenFiles, Has.Member($"{a549}_AllPeptides.psmtsv.gz"));
                Assert.That(writtenFiles, Has.Member("AllPeptides.psmtsv.gz"));
                Assert.That(writtenFiles, Has.Member($"{a549}_AllProteinGroups.tsv.gz"));
                Assert.That(writtenFiles, Has.Member("AllProteinGroups.tsv.gz"));
                // the transient library is an .msl, which is not gzipped because the combined library is built from it
                Assert.That(writtenFiles, Has.Member($"{a549}_SpectralLibrary.msl"));
                Assert.That(File.Exists(Path.Combine(outputFolder, "TransientSpectralLibrary.msl")), Is.True);
                using var library = Readers.SpectralLibrary.MslLibrary.Load(Path.Combine(outputFolder, "TransientSpectralLibrary.msl"));
                var libraryEntries = library.GetAllEntries().ToList();
                Assert.That(libraryEntries, Is.Not.Empty);
                Assert.That(libraryEntries.All(e => e.GeneName == a549 && e.ProteinAccession.StartsWith(a549 + "|")), Is.True);
                Assert.That(writtenFiles, Has.Member($"AllPeptidesAnd_{a549}_SpectralLibrary.msp.gz"));
                Assert.That(writtenFiles, Has.Member("results.txt.gz"));
            });
        }
        finally
        {
            if (Directory.Exists(outputFolder))
                Directory.Delete(outputFolder, true);
        }
    }

    [Test]
    public void RunTask_CompressedTablesHoldTheTextAnUncompressedRunWrites()
    {
        // The result tables are written straight into their .gz files; they must hold what an uncompressed run writes, and a
        // stale uncompressed table left in the folder must not be compressed over the fresh one
        string outputFolder = FreshOutputFolder("ParallelSearchCompressedTables");
        try
        {
            string a549 = Path.GetFileNameWithoutExtension(BaseDatabasePath);
            TaskLayer.ParallelSearch.ParallelSearchTask CreateTask(bool compress)
            {
                var task = new TaskLayer.ParallelSearch.ParallelSearchTask(
                [
                    new DbForTask(BaseDatabasePath, false),
                    new DbForTask(TransientDatabasePaths[1], false)
                ]);
                task.ParallelSearchParameters.WriteTransientResultsOnly = false;
                task.ParallelSearchParameters.CompressTransientSearchOutputs = compress;
                return task;
            }

            string databaseFolder = Path.Combine(outputFolder, a549);
            string[] tables =
            [
                $"{a549}_AllPSMs.psmtsv", "AllPSMs.psmtsv", $"{a549}_AllPeptides.psmtsv", "AllPeptides.psmtsv",
                $"{a549}_AllProteinGroups.tsv", "AllProteinGroups.tsv"
            ];

            CreateTask(compress: false).RunTask(outputFolder, [new DbForTask(TransientDatabasePaths[0], false)], [SpectraPath], "parallel");
            var uncompressedText = tables.ToDictionary(t => t, t => File.ReadAllText(Path.Combine(databaseFolder, t)));
            foreach (string table in tables)
            {
                File.WriteAllText(Path.Combine(databaseFolder, table), "stale\n");
            }

            File.Delete(Path.Combine(outputFolder, TransientDatabaseResultsManager.SummaryResultsFileName));
            CreateTask(compress: true).RunTask(outputFolder, [new DbForTask(TransientDatabasePaths[0], false)], [SpectraPath], "parallel");

            Assert.Multiple(() =>
            {
                foreach (string table in tables)
                {
                    Assert.That(File.Exists(Path.Combine(databaseFolder, table)), Is.False, table);
                    using var compressed = new System.IO.Compression.GZipStream(File.OpenRead(Path.Combine(databaseFolder, table + ".gz")),
                        System.IO.Compression.CompressionMode.Decompress);
                    using var reader = new StreamReader(compressed);
                    Assert.That(reader.ReadToEnd(), Is.EqualTo(uncompressedText[table]), table);
                }
            });
        }
        finally
        {
            if (Directory.Exists(outputFolder))
                Directory.Delete(outputFolder, true);
        }
    }

    [Test]
    public void RunTask_ResumeSearchesOnlyNewDatabases_AndOverwriteRedoesExistingOnes()
    {
        string outputFolder = FreshOutputFolder("ParallelSearchResume");
        try
        {
            var baseDatabases = new List<DbForTask> { new(BaseDatabasePath, false) };
            var spectra = new List<string> { SpectraPath };
            string first = Path.GetFileNameWithoutExtension(TransientDatabasePaths[0]);
            string second = Path.GetFileNameWithoutExtension(TransientDatabasePaths[1]);
            string firstPsmFile = Path.Combine(outputFolder, first, $"{first}_AllPSMs.psmtsv");

            // 1: only the first database
            var onlyFirst = new TaskLayer.ParallelSearch.ParallelSearchTask([new DbForTask(TransientDatabasePaths[0], false)]);
            onlyFirst.RunTask(outputFolder, baseDatabases, spectra, "parallel");
            var firstWriteTime = File.GetLastWriteTimeUtc(firstPsmFile);
            Assert.That(ReadSummary(outputFolder).Keys, Is.EquivalentTo(new[] { first }));

            // 2: both databases, keeping what exists: the first is skipped and the second is searched
            CreateTask(overwrite: false).RunTask(outputFolder, baseDatabases, spectra, "parallel");
            Assert.Multiple(() =>
            {
                Assert.That(ReadSummary(outputFolder).Keys, Is.EquivalentTo(new[] { first, second }));
                Assert.That(File.GetLastWriteTimeUtc(firstPsmFile), Is.EqualTo(firstWriteTime));
                Assert.That(File.Exists(Path.Combine(outputFolder, second, "results.txt")), Is.True);
            });

            // 3: the first database again, overwriting: its folder is rebuilt and the second row is kept
            string marker = Path.Combine(outputFolder, first, "left-over.txt");
            File.WriteAllText(marker, "stale");
            var overwriteFirst = new TaskLayer.ParallelSearch.ParallelSearchTask([new DbForTask(TransientDatabasePaths[0], false)]);
            overwriteFirst.ParallelSearchParameters.OverwriteTransientSearchOutputs = true;
            overwriteFirst.RunTask(outputFolder, baseDatabases, spectra, "parallel");

            string[] summaryLines = File.ReadAllLines(
                Path.Combine(outputFolder, TransientDatabaseResultsManager.SummaryResultsFileName));
            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(marker), Is.False);
                Assert.That(File.Exists(firstPsmFile), Is.True);
                Assert.That(summaryLines.Length, Is.EqualTo(3));
                Assert.That(ReadSummary(outputFolder).Keys, Is.EquivalentTo(new[] { first, second }));
            });
        }
        finally
        {
            if (Directory.Exists(outputFolder))
                Directory.Delete(outputFolder, true);
        }
    }

    /// <summary>
    /// The PSM and peptide files follow a normal search's write settings: with decoys and
    /// high q-value matches turned off, neither appears in the baseline or per-database files.
    /// </summary>
    [Test]
    public void RunTask_PsmAndPeptideFilesHonourTheWriteSettings()
    {
        string outputFolder = FreshOutputFolder("ParallelSearchWriteSettings");
        try
        {
            string a549 = Path.GetFileNameWithoutExtension(BaseDatabasePath);
            var task = new TaskLayer.ParallelSearch.ParallelSearchTask([new DbForTask(BaseDatabasePath, false)]);
            task.ParallelSearchParameters.WriteTransientResultsOnly = false;
            task.ParallelSearchParameters.WriteDecoys = false;
            task.ParallelSearchParameters.WriteHighQValuePsms = false;

            task.RunTask(outputFolder, [new DbForTask(TransientDatabasePaths[0], false)], [SpectraPath], "parallel");

            string[] files =
            [
                Path.Combine(outputFolder, "BasePSMs.psmtsv"),
                Path.Combine(outputFolder, "BasePeptides.psmtsv"),
                Path.Combine(outputFolder, a549, $"{a549}_AllPSMs.psmtsv"),
                Path.Combine(outputFolder, a549, "AllPSMs.psmtsv"),
                Path.Combine(outputFolder, a549, $"{a549}_AllPeptides.psmtsv"),
                Path.Combine(outputFolder, a549, "AllPeptides.psmtsv"),
            ];

            Assert.Multiple(() =>
            {
                foreach (string file in files)
                {
                    string[] lines = File.ReadAllLines(file);
                    string[] header = lines[0].Split('\t');
                    int decoyColumn = System.Array.IndexOf(header, "Decoy/Contaminant/Target");
                    int qValueColumn = System.Array.IndexOf(header, "QValue");
                    var rows = lines.Skip(1).Select(l => l.Split('\t')).ToList();

                    Assert.That(rows, Is.Not.Empty, file);
                    Assert.That(rows.Where(r => r[decoyColumn].Contains('D')), Is.Empty, $"decoys written to {file}");
                    Assert.That(rows.Select(r => double.Parse(r[qValueColumn], System.Globalization.CultureInfo.InvariantCulture)),
                        Has.All.LessThanOrEqualTo(0.01), $"high q-value matches written to {file}");
                }
            });
        }
        finally
        {
            if (Directory.Exists(outputFolder))
                Directory.Delete(outputFolder, true);
        }
    }

    [Test]
    public void RunTask_ReportsRetentionTimeErrorsInMinutes()
    {
        // The A549 spectra searched against A549 as both base and transient database: the base search gives the
        // anchors for the per-file Chronologer calibration, the transient search gives identifications to score.
        string outputFolder = FreshOutputFolder("ParallelSearchRetentionTime");
        try
        {
            string a549 = Path.GetFileNameWithoutExtension(BaseDatabasePath);
            var task = new TaskLayer.ParallelSearch.ParallelSearchTask([new DbForTask(BaseDatabasePath, false)]);

            task.RunTask(outputFolder, [new DbForTask(BaseDatabasePath, false)], [SpectraPath], "parallel");

            string summaryPath = Path.Combine(outputFolder, TransientDatabaseResultsManager.SummaryResultsFileName);
            string[] header = File.ReadAllLines(summaryPath)[0].Split(',');
            var row = ReadSummary(outputFolder)[a549];
            TestContext.Out.WriteLine($"PSM errors: n={row.Psm_AllRtErrors.Length} mean |error|={row.Psm_MeanAbsoluteRtError} " +
                $"min={row.Psm_AllRtErrors.DefaultIfEmpty(double.NaN).Min()} max={row.Psm_AllRtErrors.DefaultIfEmpty(double.NaN).Max()}");
            TestContext.Out.WriteLine($"peptide errors: n={row.Peptide_AllRtErrors.Length} mean |error|={row.Peptide_MeanAbsoluteRtError}");

            Assert.Multiple(() =>
            {
                Assert.That(header, Does.Contain("Psm_MeanAbsoluteRtError"));
                Assert.That(row.Psm_AllRtErrors, Is.Not.Empty);
                Assert.That(row.Peptide_AllRtErrors, Is.Not.Empty);
            });
        }
        finally
        {
            if (Directory.Exists(outputFolder))
                Directory.Delete(outputFolder, true);
        }
    }

    /// <summary>
    /// Parsimony trims each match's proteins in place, so after it a transient database's PSM and peptide files hold only
    /// matches that still have a protein from that database.
    /// </summary>
    [Test]
    public void RunTask_TransientFilesHoldOnlyMatchesWithATransientProteinAfterParsimony()
    {
        string outputFolder = FreshOutputFolder("ParallelSearchTransientAfterParsimony");
        try
        {
            // each transient protein is the first half of a baseline protein under its own accession, so it has a subset
            // of that protein's peptides and loses to it in parsimony
            string transientName = "truncated";
            string transientPath = Path.Combine(outputFolder, $"{transientName}.fasta");
            var baseProteins = ProteinDbLoader.LoadProteinFasta(BaseDatabasePath, true, DecoyType.None, false, out _);
            File.WriteAllLines(transientPath, baseProteins.SelectMany(p => new[]
            {
                $">sp|T{p.Accession}|T{p.Accession}_HUMAN truncated OS=Homo sapiens",
                p.BaseSequence[..(p.BaseSequence.Length / 2)]
            }));
            var transientAccessions = baseProteins.Select(p => $"T{p.Accession}").ToHashSet();
            var task = new TaskLayer.ParallelSearch.ParallelSearchTask([new DbForTask(transientPath, false)]);

            task.RunTask(outputFolder, [new DbForTask(BaseDatabasePath, false)], [SpectraPath], "parallel");

            Assert.Multiple(() =>
            {
                foreach (string file in new[] { $"{transientName}_AllPSMs.psmtsv", $"{transientName}_AllPeptides.psmtsv" })
                {
                    string[] lines = File.ReadAllLines(Path.Combine(outputFolder, transientName, file));
                    int accessionColumn = System.Array.IndexOf(lines[0].Split('\t'), "Accession");
                    var rows = lines.Skip(1).Select(l => l.Split('\t')[accessionColumn]).ToList();

                    Assert.That(rows, Is.Not.Empty, file);
                    Assert.That(rows.Where(accessions => !accessions.Split('|')
                            .Any(a => transientAccessions.Contains(a.Replace("DECOY_", "")))),
                        Is.Empty, $"matches without a transient protein written to {file}");
                }
            });
        }
        finally
        {
            if (Directory.Exists(outputFolder))
                Directory.Delete(outputFolder, true);
        }
    }

    [Test]
    public void RunTask_WhenTheBaseSearchFindsNothing_StopsWithAMessage()
    {
        string outputFolder = FreshOutputFolder("ParallelSearchEmptyBaseline");
        try
        {
            // a base database whose only protein matches none of the spectra
            string baseDatabase = Path.Combine(outputFolder, "nomatch.fasta");
            File.WriteAllText(baseDatabase, ">sp|NOMATCH|NOMATCH_HUMAN No match OS=Homo sapiens\nWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW\n");
            string runFolder = Path.Combine(outputFolder, "Output");
            Directory.CreateDirectory(runFolder);
            var task = new TaskLayer.ParallelSearch.ParallelSearchTask([new DbForTask(TransientDatabasePaths[0], false)]);

            var exception = Assert.Throws<EngineLayer.MetaMorpheusException>(() =>
                task.RunTask(runFolder, [new DbForTask(baseDatabase, false)], [SpectraPath], "parallel"))!;

            Assert.That(exception.Message, Does.Contain("base database search found no matches"));
        }
        finally
        {
            if (Directory.Exists(outputFolder))
                Directory.Delete(outputFolder, true);
        }
    }

    [Test]
    public void Toml_RoundTripsTaskTypeAndTransientDatabases()
    {
        string tomlPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "ParallelSearchRoundTrip.toml");
        try
        {
            var task = CreateTask(overwrite: false);
            task.ParallelSearchParameters.DeNovoMzTabPaths = [@"D:\predictions\a.mztab", @"D:\predictions\b.mztab"];
            task.ParallelSearchParameters.DeNovoSpectraFilePaths = [@"D:\spectra\a.mgf"];
            task.ParallelSearchParameters.DeNovoMappingDataFilePath = @"D:\predictions\MappingSummary.tsv";
            Toml.WriteFile(task, tomlPath, MetaMorpheusTask.tomlConfig);

            // the command line picks the task class from this value
            string taskType = Toml.ReadFile(tomlPath, MetaMorpheusTask.tomlConfig).Get<string>("TaskType");
            var readBack = Toml.ReadFile<TaskLayer.ParallelSearch.ParallelSearchTask>(tomlPath, MetaMorpheusTask.tomlConfig);

            Assert.Multiple(() =>
            {
                Assert.That(taskType, Is.EqualTo(nameof(MyTask.ParallelSearch)));
                Assert.That(readBack.TaskType, Is.EqualTo(MyTask.ParallelSearch));
                Assert.That(readBack.ParallelSearchParameters.TransientDatabases.Select(db => db.FilePath),
                    Is.EqualTo(TransientDatabasePaths));
                Assert.That(readBack.ParallelSearchParameters.OverwriteTransientSearchOutputs, Is.False);
                Assert.That(readBack.ParallelSearchParameters.MaxSearchesInParallel, Is.EqualTo(2));
                Assert.That(readBack.SearchParameters, Is.SameAs(readBack.ParallelSearchParameters));
                Assert.That(readBack.ParallelSearchParameters.DeNovoMzTabPaths,
                    Is.EqualTo(new[] { @"D:\predictions\a.mztab", @"D:\predictions\b.mztab" }));
                Assert.That(readBack.ParallelSearchParameters.DeNovoSpectraFilePaths, Is.EqualTo(new[] { @"D:\spectra\a.mgf" }));
                Assert.That(readBack.ParallelSearchParameters.DeNovoMappingDataFilePath, Is.EqualTo(@"D:\predictions\MappingSummary.tsv"));
                Assert.That(readBack.ParallelSearchParameters.UseDeNovoMapping, Is.True);
            });
        }
        finally
        {
            if (File.Exists(tomlPath))
                File.Delete(tomlPath);
        }
    }

    [Test]
    public void Toml_WithoutDeNovoSettings_LeavesDeNovoOff()
    {
        string tomlPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "ParallelSearchNoDeNovo.toml");
        try
        {
            // a task saved with the defaults writes the empty lists as blank strings and leaves the unset path out
            Toml.WriteFile(CreateTask(overwrite: false), tomlPath, MetaMorpheusTask.tomlConfig);
            var saved = Toml.ReadFile<TaskLayer.ParallelSearch.ParallelSearchTask>(tomlPath, MetaMorpheusTask.tomlConfig);

            // the same file with the summary path present but left blank, as a hand-edited file might have it
            string text = File.ReadAllText(tomlPath);
            Assert.That(text, Does.Contain("[ParallelSearchParameters]"));
            File.WriteAllText(tomlPath, text.Replace("[ParallelSearchParameters]",
                "[ParallelSearchParameters]\nDeNovoMappingDataFilePath = \"\""));
            var blank = Toml.ReadFile<TaskLayer.ParallelSearch.ParallelSearchTask>(tomlPath, MetaMorpheusTask.tomlConfig);

            Assert.Multiple(() =>
            {
                Assert.That(text, Does.Contain("DeNovoMzTabPaths = \"\""));
                Assert.That(text, Does.Not.Contain("DeNovoMappingDataFilePath"), "an unset path is not written");
                Assert.That(saved.ParallelSearchParameters.DeNovoMappingDataFilePath, Is.Null);
                Assert.That(saved.ParallelSearchParameters.DeNovoMzTabPaths, Is.Empty);
                Assert.That(saved.ParallelSearchParameters.DeNovoSpectraFilePaths, Is.Empty);
                Assert.That(saved.ParallelSearchParameters.UseDeNovoMapping, Is.False);
                Assert.That(blank.ParallelSearchParameters.UseDeNovoMapping, Is.False, "a blank path means no de novo");
            });
        }
        finally
        {
            if (File.Exists(tomlPath))
                File.Delete(tomlPath);
        }
    }

    [Test]
    public void RunTask_WithTwinEntrapment_SearchesATwinPerDatabase_BesideIt_AndALaterSearchReusesIt()
    {
        string outputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "ParallelSearchTwinEntrapment");
        string secondOutputFolder = outputFolder + "_second";
        string twinRoot = Path.Combine(TestDataFolder, TwinEntrapment.FolderName);
        foreach (string folder in new[] { outputFolder, secondOutputFolder, twinRoot })
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
        Directory.CreateDirectory(outputFolder);
        Directory.CreateDirectory(secondOutputFolder);

        try
        {
            var baseDatabases = new List<DbForTask> { new(BaseDatabasePath, false) };
            var spectra = new List<string> { SpectraPath };
            string[] targetNames = TransientDatabasePaths.Select(Path.GetFileNameWithoutExtension).ToArray()!;

            var task = CreateTask(overwrite: true);
            task.ParallelSearchParameters.UseTwinEntrapment = true;
            task.RunTask(outputFolder, baseDatabases, spectra, "parallel");

            var summary = ReadSummary(outputFolder);
            string key = TwinEntrapment.DescribeSettings(task.CommonParameters.DigestionParams, baseDatabases).Key;
            string[] twinPaths = TransientDatabasePaths
                .Select(p => Path.Combine(TwinEntrapment.GetTwinFolderBesideTarget(p, key), TwinEntrapment.GetTwinFileName(p)))
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(summary.Keys, Is.EquivalentTo(targetNames.Concat(targetNames.Select(TwinEntrapment.GetTwinName))));
                foreach (string name in targetNames)
                {
                    string twinName = TwinEntrapment.GetTwinName(name);
                    Assert.That(summary[name].TwinOf, Is.Empty);
                    Assert.That(summary[twinName].TwinOf, Is.EqualTo(name));
                    Assert.That(summary[twinName].TransientProteinCount, Is.GreaterThan(0));
                }
                Assert.That(twinPaths.All(File.Exists), Is.True, "twins are written beside their target databases");
                Assert.That(File.Exists(Path.Combine(twinRoot, key, TwinEntrapment.SettingsFileName)), Is.True);
                Assert.That(Directory.Exists(Path.Combine(outputFolder, TwinEntrapment.FolderName)), Is.False);
            });

            // a later search with another output folder reuses the twin files as they are
            var writeTimes = twinPaths.Select(File.GetLastWriteTimeUtc).ToArray();
            var rerun = CreateTask(overwrite: true);
            rerun.ParallelSearchParameters.UseTwinEntrapment = true;
            rerun.RunTask(secondOutputFolder, baseDatabases, spectra, "parallel");
            Assert.That(twinPaths.Select(File.GetLastWriteTimeUtc), Is.EqualTo(writeTimes));
            Assert.That(ReadSummary(secondOutputFolder).Keys, Is.EquivalentTo(summary.Keys));
        }
        finally
        {
            foreach (string folder in new[] { outputFolder, secondOutputFolder, twinRoot })
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }
    }
}

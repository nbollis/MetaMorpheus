using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EngineLayer;
using pepXML.Generated;
using Plotly.NET;
using Readers;
using Test.AveragingPaper;

namespace Test.ChimeraPaper.ResultFiles;

public class CellLineResults : IEnumerable<BulkResult>
{
    public string DirectoryPath { get; set; }
    public bool Override { get; set; } = false;
    public string SearchResultsDirectoryPath { get; set; }
    public string CellLine { get; set; }
    public List<BulkResult> Results { get; set; }

    public CellLineResults(string directoryPath)
    {
        DirectoryPath = directoryPath;
        SearchResultsDirectoryPath = Path.Combine(DirectoryPath, "SearchResults"); /*directoryPath*/;
        CellLine = Path.GetFileName(DirectoryPath);
        Results = new List<BulkResult>();
        foreach (var directory in Directory.GetDirectories(SearchResultsDirectoryPath).Where(p => !p.Contains("maxquant")))
        {
            if (Directory.GetFiles(directory, "meta.bin", SearchOption.AllDirectories).Any()
                && !Directory.GetFiles(directory, "combined_peptide.tsv").Any())
                continue; // fragger currently running
            if (Directory.GetFiles(directory, "*.psmtsv", SearchOption.AllDirectories).Any())
            {
                if (directory.Contains("Fragger") && Directory.GetDirectories(directory).Length > 2)
                {
                    var directories = Directory.GetDirectories(directory);
                    Results.Add(new MetaMorpheusResult(directories.First(p => p.Contains("NoChimera"))));
                    Results.Add(new MetaMorpheusResult(directories.First(p => p.Contains("WithChimera"))));
                }
                else
                    Results.Add(new MetaMorpheusResult(directory));
            }
            else
                Results.Add(new MsFraggerResult(directory));
        }
    }

    public CellLineResults(string directorypath, List<BulkResult> results)
    {
        DirectoryPath = directorypath;
        SearchResultsDirectoryPath = Path.Combine(DirectoryPath);
        CellLine = Path.GetFileName(DirectoryPath);
        Results = results;
    }

    private string _chimeraCountingPath => Path.Combine(DirectoryPath, $"{CellLine}_PSM_{FileIdentifiers.ChimeraCountingFile}");
    private ChimeraCountingFile _chimeraCountingFile;
    public ChimeraCountingFile ChimeraCountingFile => _chimeraCountingFile ??= CountChimericPsms();

    public ChimeraCountingFile CountChimericPsms()
    {
        if (!Override && File.Exists(_chimeraCountingPath))
        {
            var result = new ChimeraCountingFile(_chimeraCountingPath);
            if (result.Results.DistinctBy(p => p.Software).Count() == Results.Count)
                return result;
        }

        List<ChimeraCountingResult> results = new List<ChimeraCountingResult>();
        foreach (var result in Results)
        {
            results.AddRange(result.ChimeraPsmFile.Results);
        }

        var chimeraCountingFile = new ChimeraCountingFile(_chimeraCountingPath) { Results = results };
        chimeraCountingFile.WriteResults(_chimeraCountingPath);
        return chimeraCountingFile;
    }

    private string _chimeraPeptidePath => Path.Combine(DirectoryPath, $"{CellLine}_Peptide_{FileIdentifiers.ChimeraCountingFile}");
    private ChimeraCountingFile _chimeraPeptideFile;
    public ChimeraCountingFile ChimeraPeptideFile => _chimeraPeptideFile ??= CountChimericPeptides();
    public ChimeraCountingFile CountChimericPeptides()
    {
        if (!Override && File.Exists(_chimeraPeptidePath))
        {
            var result = new ChimeraCountingFile(_chimeraPeptidePath);
            if (result.Results.DistinctBy(p => p.Software).Count() == Results.Count)
                return result;
        }

        List<ChimeraCountingResult> results = new List<ChimeraCountingResult>();
        foreach (var bulkResult in Results.Where(p => p is MetaMorpheusResult))
        {
            var result = (MetaMorpheusResult)bulkResult;
            results.AddRange(result.ChimeraPeptideFile.Results);
        }

        var chimeraPeptideFile = new ChimeraCountingFile(_chimeraPeptidePath) { Results = results };
        chimeraPeptideFile.WriteResults(_chimeraPeptidePath);
        return chimeraPeptideFile;
    }

    private string _bulkResultCountComparisonPath => Path.Combine(DirectoryPath, $"{CellLine}_{FileIdentifiers.BottomUpResultComparison}");
    private BulkResultCountComparisonFile _bulkResultCountComparisonFile;
    public BulkResultCountComparisonFile BulkResultCountComparisonFile => _bulkResultCountComparisonFile ??= GetBulkResultCountComparisonFile();
    public BulkResultCountComparisonFile GetBulkResultCountComparisonFile()
    {
        if (!Override && File.Exists(_bulkResultCountComparisonPath))
        {
            var result = new BulkResultCountComparisonFile(_bulkResultCountComparisonPath);
            if (result.Results.DistinctBy(p => p.Condition).Count() == Results.Count)
                return result;
        }

        List<BulkResultCountComparison> results = new List<BulkResultCountComparison>();
        foreach (var result in Results)
        {
            results.AddRange(result.BulkResultCountComparisonFile.Results);
        }

        var bulkResultCountComparisonFile = new BulkResultCountComparisonFile(_bulkResultCountComparisonPath) { Results = results };
        bulkResultCountComparisonFile.WriteResults(_bulkResultCountComparisonPath);
        return bulkResultCountComparisonFile;
    }

    private string _individualFilePath => Path.Combine(DirectoryPath, $"{CellLine}_{FileIdentifiers.IndividualFileComparison}");
    private BulkResultCountComparisonFile _individualFileComparison;
    public BulkResultCountComparisonFile IndividualFileComparisonFile => _individualFileComparison ??= IndividualFileComparison();
    public BulkResultCountComparisonFile IndividualFileComparison()
    {
        if (!Override && File.Exists(_individualFilePath))
        {
            var result = new BulkResultCountComparisonFile(_individualFilePath);
            if (result.Results.DistinctBy(p => p.Condition).Count() == Results.Count)
                return result;
        }

        List<BulkResultCountComparison> results = new List<BulkResultCountComparison>();
        foreach (var result in Results.Where(p => p.IndividualFileComparisonFile != null))
        {
            results.AddRange(result.IndividualFileComparisonFile.Results);
        }

        var individualFileComparison = new BulkResultCountComparisonFile(_individualFilePath) { Results = results };
        individualFileComparison.WriteResults(_individualFilePath);
        return individualFileComparison;
    }

    private string _baseSeqIndividualFilePath => Path.Combine(DirectoryPath, $"{CellLine}_BaseSeq_{FileIdentifiers.IndividualFileComparison}");
    private BulkResultCountComparisonFile _baseSeqIndividualFileComparison;
    public BulkResultCountComparisonFile BaseSeqIndividualFileComparisonFile => _baseSeqIndividualFileComparison ??= IndividualFileComparisonBaseSeq();

    public BulkResultCountComparisonFile IndividualFileComparisonBaseSeq()
    {
        if (!Override && File.Exists(_baseSeqIndividualFilePath))
        {
            var result = new BulkResultCountComparisonFile(_baseSeqIndividualFilePath);
            if (result.Results.DistinctBy(p => p.Condition).Count() == Results.Count)
                return result;
        }

        List<BulkResultCountComparison> results = new List<BulkResultCountComparison>();
        foreach (var result in Results)
        {
            if (result.BaseSeqIndividualFileComparisonFile != null)
                results.AddRange(result.BaseSeqIndividualFileComparisonFile.Results);
        }

        var individualFileComparison = new BulkResultCountComparisonFile(_baseSeqIndividualFilePath) { Results = results };
        individualFileComparison.WriteResults(_baseSeqIndividualFilePath);
        return individualFileComparison;
    }



   



    public void FileComparisonDifferentTypes(string outPath)
    {
        var sw = new StreamWriter(outPath);
        sw.WriteLine("DatasetName,FileName,Condition,Peptides,Base Sequence,Full Sequence,1% Peptides, 1% Base Sequence, 1% Full Sequence, 1% No Chimeras");
        foreach (var result in Results)
        {
            int mmPeptides,
                mmPeptidesBaseSeq,
                mmPeptidesFullSeq,
                fraggerPeptides,
                fraggerPeptidesBaseSeq,
                fraggerPeptidesFullSeq,
                fraggerPeptidesOnePercent,
                fraggerPeptidesOnePercentBaseSeq,
                fraggerPeptidesOnePercentFullSeq;
            string file;

            if (result is MsFraggerResult frag)
            {
                foreach (var individualFile in frag.IndividualFileResults)
                {
                    file = Path.GetFileNameWithoutExtension(individualFile.PsmFile.First().FileNameWithoutExtension);
                    var peptides = individualFile.PeptideFile;
                    fraggerPeptides = peptides.Count();
                    fraggerPeptidesBaseSeq = peptides.DistinctBy(p => p.BaseSequence).Count();

                    fraggerPeptidesFullSeq = peptides.GroupBy(p => p,
                        CustomComparer<MsFraggerPeptide>.MsFraggerPeptideDistinctComparer).Count();

                    var onePercentPeptides = peptides.Where(p => p.Probability >= 0.99).ToList();
                    fraggerPeptidesOnePercent = onePercentPeptides.Count();
                    fraggerPeptidesOnePercentBaseSeq = onePercentPeptides.DistinctBy(p => p.BaseSequence).Count();
                    fraggerPeptidesOnePercentFullSeq = onePercentPeptides.GroupBy(p => p,
                        CustomComparer<MsFraggerPeptide>.MsFraggerPeptideDistinctComparer).Count();

                    sw.WriteLine(
                        $"MsFragger,{file},{frag.Condition},{fraggerPeptides},{fraggerPeptidesBaseSeq},{fraggerPeptidesFullSeq},{fraggerPeptidesOnePercent},{fraggerPeptidesOnePercentBaseSeq},{fraggerPeptidesOnePercentFullSeq}");
                }
            }
            else if (result is MetaMorpheusResult mm)
            {
                var indFileDir =
                    Directory.GetDirectories(mm.DirectoryPath, "Individual File Results", SearchOption.AllDirectories);
                if (indFileDir.Length == 0)
                    continue;

                var indFileDirectory = indFileDir.First();
                foreach (var peptideFile in Directory.GetFiles(indFileDirectory, "*Peptides.psmtsv"))
                {
                    var peptides = PsmTsvReader.ReadTsv(peptideFile, out _)
                        .Where(p => p.DecoyContamTarget == "T" && p.PEP_QValue <= 0.01);
                    file = peptides.First().FileNameWithoutExtension.Split('-')[0];
                    mmPeptides = peptides.Count();
                    mmPeptidesBaseSeq = peptides.DistinctBy(p => p.BaseSeq).Count();
                    mmPeptidesFullSeq = peptides.GroupBy(p => p.FullSequence).Count();
                    var mmNoChimeraCount = peptides.DistinctBy(p => p, CustomComparer<PsmFromTsv>.ChimeraComparer).Count();
                    sw.WriteLine(
                        $"MetaMorpheus,{file},{mm.Condition},{mmPeptides},{mmPeptidesBaseSeq},{mmPeptidesFullSeq},{mmPeptides},{mmPeptidesBaseSeq},{mmPeptidesFullSeq},{mmNoChimeraCount}");
                }
            }
        }
        sw.Dispose();
    }

    public IEnumerator<BulkResult> GetEnumerator()
    {
        return Results.GetEnumerator();
    }

    public override string ToString() => CellLine;
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
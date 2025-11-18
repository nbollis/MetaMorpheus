#nullable enable
using System.IO;
using System.Threading.Tasks;

namespace TaskLayer;

public class TransientDatabaseSearchResults : ITransientDbResults
{
    public string DatabaseName { get; set; } = string.Empty;
    public int TotalProteins { get; set; }
    public int TransientProteinCount { get; set; }

    public int TargetPsmsAtQValueThreshold { get; set; }
    public int TargetPsmsFromTransientDb { get; set; }
    public int TargetPsmsFromTransientDbAtQValueThreshold { get; set; }

    public int TargetPeptidesAtQValueThreshold { get; set; }
    public int TargetPeptidesFromTransientDb { get; set; }
    public int TargetPeptidesFromTransientDbAtQValueThreshold { get; set; }

    public int TargetProteinGroupsAtQValueThreshold { get; set; }
    public int TargetProteinGroupsFromTransientDb { get; set; }
    public int TargetProteinGroupsFromTransientDbAtQValueThreshold { get; set; }

    public double NormalizedTransientPsmCount => TransientProteinCount > 0 ? (double)TargetPsmsFromTransientDbAtQValueThreshold / TransientProteinCount : 0;
    public double NormalizedTransientPeptideCount => TransientProteinCount > 0 ? (double)TargetPeptidesFromTransientDbAtQValueThreshold / TransientProteinCount : 0;
    public double NormalizedTransientProteinGroupCount => TransientProteinCount > 0 ? (double)TargetProteinGroupsFromTransientDbAtQValueThreshold / TransientProteinCount : 0;

    /// <summary>
    /// Writes the database results to a text file
    /// </summary>
    public async Task WriteToTextFileAsync(string filePath, double qValueThreshold, bool doParsimony)
    {
        await using StreamWriter file = new StreamWriter(filePath);
        await file.WriteLineAsync($"Database: {DatabaseName}");
        await file.WriteLineAsync($"Total proteins in combined database: {TotalProteins}");
        await file.WriteLineAsync($"Total proteins from transient database: {TransientProteinCount}");
        await file.WriteLineAsync();
        await file.WriteLineAsync($"Target PSMs at {qValueThreshold * 100}% FDR: {TargetPsmsAtQValueThreshold}");
        await file.WriteLineAsync($"Target PSMs from transient database: {TargetPsmsFromTransientDb}");
        await file.WriteLineAsync($"Target PSMs from transient database at {qValueThreshold * 100}% FDR: {TargetPsmsFromTransientDbAtQValueThreshold}");
        await file.WriteLineAsync();
        await file.WriteLineAsync($"Target peptides at {qValueThreshold * 100}% FDR: {TargetPeptidesAtQValueThreshold}");
        await file.WriteLineAsync($"Target peptides from transient database: {TargetPeptidesFromTransientDb}");
        await file.WriteLineAsync($"Target peptides from transient database at {qValueThreshold * 100}% FDR: {TargetPeptidesFromTransientDbAtQValueThreshold}");

        if (doParsimony)
        {
            await file.WriteLineAsync();
            await file.WriteLineAsync($"Target protein groups at {qValueThreshold * 100}% FDR: {TargetProteinGroupsAtQValueThreshold}");
            await file.WriteLineAsync($"Target protein groups with transient database proteins: {TargetProteinGroupsFromTransientDb}");
            await file.WriteLineAsync($"Target protein groups with transient database proteins at {qValueThreshold * 100}% FDR: {TargetProteinGroupsFromTransientDbAtQValueThreshold}");
        }
    }
}
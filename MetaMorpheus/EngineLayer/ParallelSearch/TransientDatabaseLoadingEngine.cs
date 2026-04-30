using System.Collections.Generic;
using System.Linq;
using EngineLayer.DatabaseLoading;
using Omics;
using UsefulProteomicsDatabases;

namespace EngineLayer.ParallelSearch;
public class TransientDatabaseLoadingEngine(CommonParameters commonParameters, List<(string FileName, CommonParameters Parameters)> fileSpecificParameters, List<string> nestedIds, List<DbForTask> dbFilenameList, string taskId, DecoyType decoyType, bool generateTargets = true, List<string> localizableMods = null, TargetContaminantAmbiguity tcAmbiguity = TargetContaminantAmbiguity.RemoveContaminant, bool writeTargetDecoyFasta = false, string outputFolder = null) : DatabaseLoadingEngine(commonParameters, fileSpecificParameters, nestedIds, dbFilenameList, taskId, decoyType, generateTargets, localizableMods, tcAmbiguity, writeTargetDecoyFasta, outputFolder)
{
    protected override MetaMorpheusEngineResults RunSpecific()
    {
        var results = base.RunSpecific() as DatabaseLoadingEngineResults;
        var proteins = results?.BioPolymers.Select(prot => new TransientBioPolymer(prot) as IBioPolymer).ToList();
        return new DatabaseLoadingEngineResults(this, DbForTask, proteins, results.BioPolymersRenamedBySanitization, results.BioPolymersRemovedBySanitization, results.DecoysScrambledDueToHomology);
    }
}

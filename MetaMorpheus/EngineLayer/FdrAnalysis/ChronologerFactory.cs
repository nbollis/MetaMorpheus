using Chromatography.RetentionTimePrediction.Chronologer;

namespace EngineLayer.FdrAnalysis
{
    /// <summary>
    /// Creates Chronologer predictors one at a time. Every MetaMorpheus caller that builds a Chronologer goes through here.
    /// </summary>
    /// <remarks>
    /// mzLib's Chronologer constructor writes its embedded weights to one fixed file in the temp folder
    /// (Chronologer_20220601193755_TorchSharp.dat), overwriting it each time, and then reads it back. Two predictors built at
    /// once in one process collide on that file and one of them throws "being used by another process". In a parallel search
    /// that happened when PEP built its predictor while the de novo mapper built its own on a background task: the mapper
    /// failed, and with it every database's de novo metrics. Each predictor keeps its own model lock, so predictions from
    /// different predictors still run side by side; only construction is serialized.
    /// Processes that share a temp folder can still collide; that needs a fix in mzLib.
    /// </remarks>
    public static class ChronologerFactory
    {
        private static readonly object ConstructionLock = new();

        /// <summary>A new Chronologer predictor, built while no other one is being built in this process.</summary>
        public static ChronologerRetentionTimePredictor Create()
        {
            lock (ConstructionLock)
            {
                return new ChronologerRetentionTimePredictor();
            }
        }
    }
}

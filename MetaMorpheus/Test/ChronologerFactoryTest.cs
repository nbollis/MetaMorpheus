using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Chromatography.RetentionTimePrediction.Chronologer;
using EngineLayer.FdrAnalysis;
using NUnit.Framework;
using Omics.Modifications;
using Proteomics.ProteolyticDigestion;

namespace Test
{
    [TestFixture]
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal class ChronologerFactoryTest
    {
        /// <summary>
        /// Chronologer's constructor rewrites one fixed weights file in the temp folder. Built at once from several threads
        /// (PEP, the retention time collector and the de novo mapper do this in a parallel search), predictors collided on it.
        /// Through the factory every one is built and predicts the same value.
        /// </summary>
        [Test]
        public void Create_FromManyThreadsAtOnce_BuildsEveryPredictor()
        {
            var peptide = new PeptideWithSetModifications("PEPTIDEK", new Dictionary<string, Modification>());
            var predictions = new double?[8];

            Parallel.For(0, predictions.Length, new ParallelOptions { MaxDegreeOfParallelism = predictions.Length }, i =>
            {
                using ChronologerRetentionTimePredictor predictor = ChronologerFactory.Create();
                predictions[i] = predictor.PredictRetentionTimeEquivalent(peptide, out _);
            });

            Assert.That(predictions, Has.All.Not.Null);
            Assert.That(predictions.Distinct().Count(), Is.EqualTo(1));
        }
    }
}

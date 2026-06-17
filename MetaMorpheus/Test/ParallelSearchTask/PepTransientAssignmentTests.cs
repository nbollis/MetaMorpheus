using System.Collections.Generic;
using EngineLayer;
using EngineLayer.ParallelSearch;
using NUnit.Framework;
using Test.ParallelSearchTask.Utility;

namespace Test.ParallelSearchTask;

[TestFixture]
public class PepTransientAssignmentTests
{
    private static List<(string, CommonParameters)> Fsp(CommonParameters cp) => new() { ("file.raw", cp) };

    [Test]
    public void AssignPepFromTrainedModel_NoModel_IsNoOp()
    {
        var cp = ParallelSearchTestContextFactory.CreateCommonParameters();
        var basePsms = new List<SpectralMatch>
        {
            ParallelSearchTestContextFactory.CreateSpectralMatch(cp, isDecoy: false, score: 20, psmQValue: 0.001, peptideQValue: 0.001),
        };
        var engine = new TransientPepAnalysisEngine(basePsms, "standard", Fsp(cp), TestContext.CurrentContext.TestDirectory);
        Assert.That(engine.HasTrainedModel, Is.False);

        var transient = ParallelSearchTestContextFactory.CreateSpectralMatch(cp, isDecoy: false, score: 20, psmQValue: 0.5, peptideQValue: 0.5, scanNumber: 2);
        transient.PeptideFdrInfo.PEP_QValue = 1.23;

        engine.AssignPepFromTrainedModel(new List<SpectralMatch> { transient }, peptideLevel: true);

        Assert.That(transient.PeptideFdrInfo.PEP_QValue, Is.EqualTo(1.23), "no trained model -> no-op");
    }

    [Test]
    public void TrainAndAssign_CreatesMissingFdrInfo_AndAssignsPepQValue()
    {
        var cp = ParallelSearchTestContextFactory.CreateCommonParameters();
        var basePsms = new List<SpectralMatch>();
        for (int i = 0; i < 12; i++)
            basePsms.Add(ParallelSearchTestContextFactory.CreateSpectralMatch(cp, isDecoy: false, score: 20 + i, psmQValue: 0.001, peptideQValue: 0.001, scanNumber: i + 1));
        for (int i = 0; i < 12; i++)
            basePsms.Add(ParallelSearchTestContextFactory.CreateSpectralMatch(cp, isDecoy: true, score: 5 + i, psmQValue: 0.5, peptideQValue: 0.5, scanNumber: 100 + i));

        var engine = new TransientPepAnalysisEngine(basePsms, "standard", Fsp(cp), TestContext.CurrentContext.TestDirectory);
        bool trained = engine.TrainSingleModelAndAssignBasePep();
        Assume.That(trained, Is.True);
        Assert.That(engine.HasTrainedModel, Is.True);

        var transient = ParallelSearchTestContextFactory.CreateSpectralMatch(cp, isDecoy: false, score: 25, psmQValue: 0.5, peptideQValue: 0.5, scanNumber: 500);
        transient.PeptideFdrInfo = null;
        transient.PsmFdrInfo = null;

        engine.AssignPepFromTrainedModel(new List<SpectralMatch> { transient }, peptideLevel: true);

        Assert.That(transient.PeptideFdrInfo, Is.Not.Null, "missing PeptideFdrInfo must be created");
        Assert.That(transient.PsmFdrInfo, Is.Not.Null, "missing PsmFdrInfo must be created");
        Assert.That(transient.PeptideFdrInfo.PEP_QValue, Is.Not.EqualTo(2.0), "PEP_QValue must be assigned from the background curve");
    }
}

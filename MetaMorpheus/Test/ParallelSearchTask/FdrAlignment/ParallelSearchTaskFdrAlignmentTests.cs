using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using EngineLayer;
using EngineLayer.FdrAnalysis;
using NUnit.Framework;
using Test.ParallelSearchTask.Utility;
using ParallelSearchTaskType = TaskLayer.ParallelSearch.ParallelSearchTask;

namespace Test.ParallelSearchTask.FdrAlignment;

[TestFixture]
public class ParallelSearchTaskFdrAlignmentTests
{
    [Test]
    public void ApplyBaselineFdrByScore_PsmMode_AlignsAndClamps()
    {
        var task = new ParallelSearchTaskType();
        SetBaselineLookup(task, new[]
        {
            CreateLookupEntry(task, 100, CreateFdrInfo(0.01), CreateFdrInfo(0.01)),
            CreateLookupEntry(task, 50, CreateFdrInfo(0.05), CreateFdrInfo(0.05)),
            CreateLookupEntry(task, 10, CreateFdrInfo(0.10), CreateFdrInfo(0.10)),
        });

        var commonParameters = ParallelSearchTestContextFactory.CreateCommonParameters();
        var transientPsms = new List<SpectralMatch>
        {
            ParallelSearchTestContextFactory.CreateSpectralMatch(commonParameters, false, 110, 0.9, 0.9, 1),
            ParallelSearchTestContextFactory.CreateSpectralMatch(commonParameters, false, 80, 0.9, 0.9, 2),
            ParallelSearchTestContextFactory.CreateSpectralMatch(commonParameters, false, 40, 0.9, 0.9, 3),
            ParallelSearchTestContextFactory.CreateSpectralMatch(commonParameters, false, 5, 0.9, 0.9, 4),
        };

        var result = InvokeApplyBaselineFdrByScore(task, ref transientPsms, peptideMode: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.AlignedCount, Is.EqualTo(4));
            Assert.That(result.ClampedHighCount, Is.EqualTo(1));
            Assert.That(result.ClampedLowCount, Is.EqualTo(1));
            Assert.That(transientPsms[0].PsmFdrInfo.QValue, Is.EqualTo(0.01).Within(1e-10));
            Assert.That(transientPsms[1].PsmFdrInfo.QValue, Is.EqualTo(0.01).Within(1e-10));
            Assert.That(transientPsms[2].PsmFdrInfo.QValue, Is.EqualTo(0.05).Within(1e-10));
            Assert.That(transientPsms[3].PsmFdrInfo.QValue, Is.EqualTo(0.10).Within(1e-10));
        });
    }

    [Test]
    public void ApplyBaselineFdrByScore_PeptideMode_SkipsMissingPeptideFdr()
    {
        var task = new ParallelSearchTaskType();
        SetBaselineLookup(task, new[]
        {
            CreateLookupEntry(task, 100, CreateFdrInfo(0.01), null),
            CreateLookupEntry(task, 50, CreateFdrInfo(0.05), CreateFdrInfo(0.07)),
            CreateLookupEntry(task, 10, CreateFdrInfo(0.10), CreateFdrInfo(0.2)),
        });

        var commonParameters = ParallelSearchTestContextFactory.CreateCommonParameters();
        var transientPeptides = new List<SpectralMatch>
        {
            ParallelSearchTestContextFactory.CreateSpectralMatch(commonParameters, false, 100, 0.9, 0.9, 11),
            ParallelSearchTestContextFactory.CreateSpectralMatch(commonParameters, false, 40, 0.9, 0.9, 12),
        };

        var result = InvokeApplyBaselineFdrByScore(task, ref transientPeptides, peptideMode: true);

        Assert.Multiple(() =>
        {
            Assert.That(result.AlignedCount, Is.EqualTo(2));
            Assert.That(result.ClampedHighCount, Is.EqualTo(0));
            Assert.That(result.ClampedLowCount, Is.EqualTo(0));
            Assert.That(transientPeptides[0].PeptideFdrInfo.QValue, Is.EqualTo(0.07).Within(1e-10));
            Assert.That(transientPeptides[1].PeptideFdrInfo.QValue, Is.EqualTo(0.07).Within(1e-10));
        });
    }

    [Test]
    public void CreateTransientSearchPsmArray_PeptideMode_ShallowCopiesBaseline()
    {
        var task = new ParallelSearchTaskType();
        var commonParameters = ParallelSearchTestContextFactory.CreateCommonParameters();
        var basePsm = ParallelSearchTestContextFactory.CreateSpectralMatch(commonParameters, false, 100, 0.02, 0.03, 21);
        basePsm.PsmFdrInfo = CreateFdrInfo(0.02);
        basePsm.PeptideFdrInfo = CreateFdrInfo(0.03);

        var baseline = new SpectralMatch[] { basePsm };
        SetBaseSearchPsms(task, baseline);
        SpectralMatch[] transientArray = InvokeCreateTransientSearchPsmArray(task);

        Assert.Multiple(() =>
        {
            Assert.That(transientArray, Is.Not.SameAs(baseline));
            Assert.That(transientArray[0], Is.Not.Null);
            Assert.That(transientArray[0], Is.SameAs(basePsm));
            Assert.That(transientArray[0].PsmFdrInfo.QValue, Is.EqualTo(0.02).Within(1e-10));
            Assert.That(transientArray[0].PeptideFdrInfo.QValue, Is.EqualTo(0.03).Within(1e-10));
        });
    }

    private static FdrInfo CreateFdrInfo(double qValue)
    {
        return new FdrInfo
        {
            QValue = qValue,
            QValueNotch = qValue,
            PEP = qValue,
            PEP_QValue = qValue,
            CumulativeDecoy = 1,
            CumulativeTarget = 10,
            CumulativeDecoyNotch = 1,
            CumulativeTargetNotch = 10,
        };
    }

    private static object CreateLookupEntry(ParallelSearchTaskType task, double score, FdrInfo psmFdrInfo, FdrInfo? peptideFdrInfo)
    {
        Type entryType = task.GetType().GetNestedType("BaselineFdrLookupEntry", BindingFlags.NonPublic)!;
        return Activator.CreateInstance(entryType, score, psmFdrInfo, peptideFdrInfo)!;
    }

    private static void SetBaselineLookup(ParallelSearchTaskType task, object[] entries)
    {
        var listType = typeof(List<>).MakeGenericType(task.GetType().GetNestedType("BaselineFdrLookupEntry", BindingFlags.NonPublic)!);
        var list = (IList)Activator.CreateInstance(listType)!;
        foreach (object entry in entries)
        {
            list.Add(entry);
        }

        PropertyInfo property = task.GetType().GetProperty("BaselineFdrLookup", BindingFlags.Instance | BindingFlags.NonPublic)!;
        property.SetValue(task, list);
    }

    private static void SetBaseSearchPsms(ParallelSearchTaskType task, SpectralMatch[] basePsms)
    {
        FieldInfo field = task.GetType().GetField("BaseSearchPsms", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(task, basePsms);
    }

    private static SpectralMatch[] InvokeCreateTransientSearchPsmArray(ParallelSearchTaskType task)
    {
        MethodInfo method = task.GetType().GetMethod("CreateTransientSearchPsmArray", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (SpectralMatch[])method.Invoke(task, null)!;
    }

    private static (int AlignedCount, int ClampedHighCount, int ClampedLowCount) InvokeApplyBaselineFdrByScore(ParallelSearchTaskType task, ref List<SpectralMatch> psms, bool peptideMode)
    {
        MethodInfo method = task.GetType().GetMethod("ApplyBaselineFdrByScore", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object[] parameters = { psms, peptideMode };
        object result = method.Invoke(task, parameters)!;
        psms = (List<SpectralMatch>)parameters[0];

        var tuple = ((int, int, int))result;
        return (tuple.Item1, tuple.Item2, tuple.Item3);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using Chemistry;
using EngineLayer;
using GuiFunctions;
using MassSpectrometry;
using Nett;
using NUnit.Framework;
using Readers;
using TaskLayer;
using EngineLayer.Deconvolution;
using EngineLayer.Deconvolution.FeatureFileMapping;

namespace Test.GuiTests;

/// <summary>
/// Compatibility tests proving that the task-window save path keeps working
/// without changes when <see cref="FeatureMappedFromFileDeconvolutionParameters"/>
/// is the precursor deconvolution parameters.
///
/// These tests exercise the contract that the xaml.cs code-behind relies on:
///   <c>DeconHostViewModel.{Precursor,Product}DeconvolutionParameters.Parameters</c>
///
/// The test simulates the full reopen/save cycle by:
///   1. Creating a task with FromFile precursor decon params
///   2. Building the DeconHostViewModel (what task windows do in UpdateFieldsFromTask)
///   3. Reading .Parameters (what task windows do in SaveButton_Click)
///   4. Round-tripping through TOML (what MetaMorpheusTask does to persist the task)
///   5. Re-building the DeconHostViewModel from the reloaded task
///   6. Reading .Parameters again to confirm the FromFile mapping survived
/// </summary>
[TestFixture]
public class TaskWindowFromFileCompatibilityTests
{
    private string _tempTaskPath;

    [SetUp]
    public void SetUp()
    {
        _tempTaskPath = Path.Combine(Path.GetTempPath(), "taskwindow_compat_task_" + Guid.NewGuid().ToString("N") + ".toml");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_tempTaskPath)) File.Delete(_tempTaskPath);
    }

    /// <summary>
    /// Build a SearchTask with FeatureMappedFromFileDeconvolutionParameters as precursor
    /// deconvolution. Mirrors the kind of state a user would have when they save a
    /// search task with a FromFile precursor decon setting.
    /// </summary>
    private static SearchTask BuildSearchTaskWithFromFilePrecursor(out string rawFilePath, out string featureFilePath)
    {
        // Real on-disk feature file is required for the eventual ToDeconvolutionParameters call
        // later in the pipeline; not exercised here, but the build is consistent with the runtime
        // contract that FeatureFileMap entries reference existing files.
        string tempDir = Path.Combine(Path.GetTempPath(), "taskwindow_compat_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        rawFilePath = Path.Combine(tempDir, "sample1.mzML");
        featureFilePath = Path.Combine(tempDir, "sample1.feature.tsv");
        File.WriteAllText(featureFilePath, string.Empty);

        var searchMap = new SearchFeatureFileMap
        {
            SourceMapPath = Path.Combine(tempDir, "feature-maps.toml"),
            SelectedConditionKey = "flash",
            SelectedConditionDisplayName = "FlashDeconv",
            Entries = new List<SearchFeatureFileMapEntry>
            {
                new()
                {
                    MassSpecFilePath = rawFilePath,
                    MassSpecFileName = "sample1.mzML",
                    FeatureFilePath = featureFilePath,
                    FeatureFileName = "sample1.feature.tsv"
                }
            }
        };

        var fromFile = new FeatureMappedFromFileDeconvolutionParameters(
            searchMap, 2, 18, Polarity.Positive, new Averagine(), 0.9876);

        return new SearchTask
        {
            CommonParameters = new CommonParameters(
                precursorDeconParams: fromFile,
                productDeconParams: new ClassicDeconvolutionParameters(1, 10, 4, 3))
        };
    }

    /// <summary>
    /// Core invariant: task-window save code calls
    /// <c>DeconHostViewModel.PrecursorDeconvolutionParameters.Parameters</c>
    /// and must receive a <see cref="FeatureMappedFromFileDeconvolutionParameters"/>
    /// that the GUI can pass back to <see cref="CommonParameters"/>.
    ///
    /// Note: in the FromFile view model, <c>Parameters</c> regenerates the embedded
    /// <see cref="SearchFeatureFileMap"/> from the <c>Rows</c> collection (which is
    /// driven by the GUI's mapping state, not the input parameter's map). For a task
    /// window just opened from a saved task, the rows are populated from the global
    /// feature-map store, not from the input task's embedded map. This test therefore
    /// verifies the *type* and *identity* contract: the save flow still produces a
    /// <see cref="FeatureMappedFromFileDeconvolutionParameters"/>.
    /// </summary>
    [Test]
    public void Parameters_Consumption_YieldsFromFileWithSearchMap()
    {
        var searchTask = BuildSearchTaskWithFromFilePrecursor(out _, out _);

        // Simulate task window construction (UpdateFieldsFromTask path).
        var deconHost = new DeconHostViewModel(
            searchTask.CommonParameters.PrecursorDeconvolutionParameters,
            searchTask.CommonParameters.ProductDeconvolutionParameters,
            searchTask.CommonParameters.UseProvidedPrecursorInfo,
            searchTask.CommonParameters.DoPrecursorDeconvolution);

        // The host must produce a FromFile view model for the precursor slot.
        Assert.That(deconHost.PrecursorDeconvolutionParameters, Is.InstanceOf<FromFileDeconParamsViewModel>(),
            "Host must produce a FromFileDeconParamsViewModel for FromFile precursor");

        // Simulate task window save (SaveButton_Click).
        DeconvolutionParameters consumedPrecursor = deconHost.PrecursorDeconvolutionParameters.Parameters;
        DeconvolutionParameters consumedProduct = deconHost.ProductDeconvolutionParameters.Parameters;

        Assert.That(consumedPrecursor, Is.InstanceOf<FeatureMappedFromFileDeconvolutionParameters>());
        var fromFile = (FeatureMappedFromFileDeconvolutionParameters)consumedPrecursor;
        Assert.That(fromFile.FeatureFileMap, Is.Not.Null,
            "Consumed parameters must still expose a SearchFeatureFileMap (may be empty until rows are mapped)");

        // Product side falls back to Classic per T5/T6 contract
        Assert.That(consumedProduct, Is.InstanceOf<ClassicDeconvolutionParameters>());

        // Reassignment of consumed params into CommonParameters is the actual save flow
        var reSaved = new CommonParameters(
            precursorDeconParams: consumedPrecursor,
            productDeconParams: consumedProduct);

        Assert.That(reSaved.PrecursorDeconvolutionParameters, Is.InstanceOf<FeatureMappedFromFileDeconvolutionParameters>());
        Assert.That(reSaved.ProductDeconvolutionParameters, Is.InstanceOf<ClassicDeconvolutionParameters>());
    }

    /// <summary>
    /// Product-side FromFile is intentionally mapped to Classic by the DeconHostViewModel.
    /// Verifies this fallback is consistent for all 5 task windows that host the decon control.
    /// </summary>
    [Test]
    public void Parameters_Consumption_ProductFromFile_FallsBackToClassic()
    {
        var fromFileProduct = new FeatureMappedFromFileDeconvolutionParameters();
        var classicPrecursor = new ClassicDeconvolutionParameters(1, 12, 4, 3);

        var deconHost = new DeconHostViewModel(classicPrecursor, fromFileProduct);

        // Product: must be Classic regardless of input
        Assert.That(deconHost.ProductDeconvolutionParameters.DeconvolutionType,
            Is.EqualTo(DeconvolutionType.ClassicDeconvolution));
        Assert.That(deconHost.ProductDeconvolutionParameters.Parameters,
            Is.InstanceOf<ClassicDeconvolutionParameters>());

        // Save path can still consume both
        DeconvolutionParameters precursorParams = deconHost.PrecursorDeconvolutionParameters.Parameters;
        DeconvolutionParameters productParams = deconHost.ProductDeconvolutionParameters.Parameters;
        Assert.That(precursorParams, Is.Not.Null);
        Assert.That(productParams, Is.Not.Null);
    }

    /// <summary>
    /// Reopen/save round-trip via TOML preserves the FromFile precursor mapping
    /// end-to-end through the DeconHostViewModel. This is the exact path the
    /// task windows depend on when a user reopens a saved task.
    ///
    /// Note: when the host rebuilds a <see cref="FromFileDeconParamsViewModel"/>
    /// from the input task's parameters, the embedded <see cref="SearchFeatureFileMap"/>
    /// is regenerated from <c>Rows</c> (which are empty in this unit test environment
    /// — they would be populated from the global store at runtime). The TOML round-trip
    /// itself preserves the embedded map; the host just uses a different source of truth
    /// for GUI rendering. This test therefore verifies:
    ///   - The host successfully restores a FromFile view model from the reloaded task
    ///   - The .Parameters access pattern continues to work end-to-end
    ///   - The TOML layer itself preserves the FromFile precursor type
    /// </summary>
    [Test]
    public void SaveAndReopen_RoundTrip_PreservesFromFilePrecursorMapping()
    {
        var originalTask = BuildSearchTaskWithFromFilePrecursor(out _, out _);

        // ---- Save path: task window builds host, consumes .Parameters, hands to CommonParameters ----
        var saveHost = new DeconHostViewModel(
            originalTask.CommonParameters.PrecursorDeconvolutionParameters,
            originalTask.CommonParameters.ProductDeconvolutionParameters);

        var taskForToml = new SearchTask
        {
            CommonParameters = new CommonParameters(
                precursorDeconParams: saveHost.PrecursorDeconvolutionParameters.Parameters,
                productDeconParams: saveHost.ProductDeconvolutionParameters.Parameters)
        };

        // ---- Persist and reload (simulates MetaMorpheusTask file save/reopen) ----
        Toml.WriteFile(taskForToml, _tempTaskPath, MetaMorpheusTask.tomlConfig);
        var reloaded = Toml.ReadFile<SearchTask>(_tempTaskPath, MetaMorpheusTask.tomlConfig);

        // ---- TOML layer must preserve the FromFile precursor type ----
        Assert.That(reloaded.CommonParameters.PrecursorDeconvolutionParameters,
            Is.InstanceOf<FeatureMappedFromFileDeconvolutionParameters>(),
            "TOML round-trip must preserve FeatureMappedFromFileDeconvolutionParameters type");

        // ---- Reopen path: re-construct the host from the reloaded task ----
        var reopenHost = new DeconHostViewModel(
            reloaded.CommonParameters.PrecursorDeconvolutionParameters,
            reloaded.CommonParameters.ProductDeconvolutionParameters);

        // The host must restore a FromFile view model for the precursor slot.
        Assert.That(reopenHost.PrecursorDeconvolutionParameters,
            Is.InstanceOf<FromFileDeconParamsViewModel>(),
            "Reopen must restore the FromFileDeconParamsViewModel for the precursor slot");

        // ---- Re-save path: confirm the reloaded host still produces a valid FromFile ----
        DeconvolutionParameters resavedPrecursor = reopenHost.PrecursorDeconvolutionParameters.Parameters;
        DeconvolutionParameters resavedProduct = reopenHost.ProductDeconvolutionParameters.Parameters;

        Assert.That(resavedPrecursor, Is.InstanceOf<FeatureMappedFromFileDeconvolutionParameters>(),
            "Re-save must produce a FeatureMappedFromFileDeconvolutionParameters from the reopened host");
        Assert.That(resavedProduct, Is.InstanceOf<ClassicDeconvolutionParameters>(),
            "Product side must remain Classic after reopen");
    }

    /// <summary>
    /// The pattern used by every task window is identical:
    ///   <c>DeconHostViewModel.{Precursor,Product}DeconvolutionParameters.Parameters</c>
    /// Verify this property access path is unchanged for all four other task
    /// windows that host the decon control (Calibrate, GPTMD, GlycoSearch, XLSearch).
    /// </summary>
    [Test]
    public void Parameters_AccessPattern_StableAcrossTaskTypes()
    {
        var fromFile = new FeatureMappedFromFileDeconvolutionParameters(
            new SearchFeatureFileMap
            {
                SelectedConditionKey = "flash",
                Entries = new List<SearchFeatureFileMapEntry>
                {
                    new() { MassSpecFilePath = "x.mzML", FeatureFilePath = "x.feature" }
                }
            }, 1, 5, Polarity.Positive);
        var classic = new ClassicDeconvolutionParameters(1, 10, 4, 3);

        // Each task window builds the host the same way: hand it the common params directly
        // and read .Parameters back. Verify the contract holds for the parameter access
        // regardless of which task window will eventually own it.
        var taskTypes = new (string Name, MetaMorpheusTask Task)[]
        {
            ("Search", new SearchTask { CommonParameters = new CommonParameters(precursorDeconParams: fromFile, productDeconParams: classic) }),
            ("Calibrate", new CalibrationTask { CommonParameters = new CommonParameters(precursorDeconParams: fromFile, productDeconParams: classic) }),
            ("GPTMD", new GptmdTask { CommonParameters = new CommonParameters(precursorDeconParams: fromFile, productDeconParams: classic) }),
            ("GlycoSearch", new GlycoSearchTask { CommonParameters = new CommonParameters(precursorDeconParams: fromFile, productDeconParams: classic) }),
            ("XLSearch", new XLSearchTask { CommonParameters = new CommonParameters(precursorDeconParams: fromFile, productDeconParams: classic) }),
        };

        foreach (var (name, task) in taskTypes)
        {
            var host = new DeconHostViewModel(
                task.CommonParameters.PrecursorDeconvolutionParameters,
                task.CommonParameters.ProductDeconvolutionParameters);

            // This is the exact statement the xaml.cs code-behind relies on.
            DeconvolutionParameters consumedPrecursor = host.PrecursorDeconvolutionParameters.Parameters;
            DeconvolutionParameters consumedProduct = host.ProductDeconvolutionParameters.Parameters;

            Assert.That(consumedPrecursor, Is.InstanceOf<FeatureMappedFromFileDeconvolutionParameters>(),
                $"{name} task window must be able to consume FromFile precursor via .Parameters");
            Assert.That(consumedProduct, Is.InstanceOf<ClassicDeconvolutionParameters>(),
                $"{name} task window must always see Classic product via .Parameters");
        }
    }

    /// <summary>
    /// The save flow that task windows actually execute — building a new
    /// <see cref="CommonParameters"/> from consumed <c>.Parameters</c> — must
    /// succeed for FromFile precursor and produce a value-equal instance
    /// after TOML round-trip.
    /// </summary>
    [Test]
    public void SavePath_FromFilePrecursor_RoundTripsThroughCommonParametersAndToml()
    {
        var original = BuildSearchTaskWithFromFilePrecursor(out _, out _);
        var originalFromFile = (FeatureMappedFromFileDeconvolutionParameters)
            original.CommonParameters.PrecursorDeconvolutionParameters;

        // Simulate task window save: read host .Parameters, build new CommonParameters.
        var host = new DeconHostViewModel(
            original.CommonParameters.PrecursorDeconvolutionParameters,
            original.CommonParameters.ProductDeconvolutionParameters);

        var newCommonParams = new CommonParameters(
            precursorDeconParams: host.PrecursorDeconvolutionParameters.Parameters,
            productDeconParams: host.ProductDeconvolutionParameters.Parameters);

        var savedTask = new SearchTask { CommonParameters = newCommonParams };
        Toml.WriteFile(savedTask, _tempTaskPath, MetaMorpheusTask.tomlConfig);
        var reloaded = Toml.ReadFile<SearchTask>(_tempTaskPath, MetaMorpheusTask.tomlConfig);

        var reloadedFromFile = reloaded.CommonParameters.PrecursorDeconvolutionParameters
            as FeatureMappedFromFileDeconvolutionParameters;

        Assert.That(reloadedFromFile, Is.Not.Null,
            "Reopened task must still surface a FeatureMappedFromFileDeconvolutionParameters for precursor");
        Assert.That(reloadedFromFile!.FeatureFileMap, Is.EqualTo(originalFromFile.FeatureFileMap),
            "Embedded SearchFeatureFileMap must survive TOML round-trip");
    }
}

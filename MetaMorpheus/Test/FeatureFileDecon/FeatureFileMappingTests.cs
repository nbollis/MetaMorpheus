using Chemistry;
using EngineLayer;
using MassSpectrometry;
using Nett;
using NUnit.Framework;
using Readers;
using System;
using System.Collections.Generic;
using System.IO;
using TaskLayer;
using TaskLayer.Deconvolution;
using TaskLayer.Deconvolution.FeatureFileMapping;

namespace Test;

[TestFixture]
public static class FeatureFileMappingTests
{
    [Test]
    public static void BuildSearchMap_IncludesOnlyRequestedConditionAndSearchFiles()
    {
        var map = new FeatureFileMap
        {
            FilePath = @"E:\maps\feature-map-a.toml"
        };
        map.AddOrReplaceCondition(new FeatureFileMapCondition
        {
            ConditionKey = "flash",
            DisplayName = "FlashDeconv"
        });
        map.AddOrReplaceCondition(new FeatureFileMapCondition
        {
            ConditionKey = "dino",
            DisplayName = "Dinosaur"
        });

        var sample1 = FeatureSpectraEntry.Create(@"E:\data\sample1.mzML");
        sample1.AddOrReplaceConditionFile(FeatureFileConditionEntry.Create("flash", @"E:\features\flash\sample1_ms1.feature"));
        sample1.AddOrReplaceConditionFile(FeatureFileConditionEntry.Create("dino", @"E:\features\dino\sample1.feature.tsv"));
        map.AddOrReplaceSpectraEntry(sample1);

        var sample2 = FeatureSpectraEntry.Create(@"E:\data\sample2.mzML");
        sample2.AddOrReplaceConditionFile(FeatureFileConditionEntry.Create("flash", @"E:\features\flash\sample2_ms1.feature"));
        map.AddOrReplaceSpectraEntry(sample2);

        var searchMap = map.BuildSearchMap(new[] { @"E:\data\sample1.mzML" }, "flash");

        Assert.That(searchMap.SourceMapPath, Is.EqualTo(map.FilePath));
        Assert.That(searchMap.SelectedConditionKey, Is.EqualTo("flash"));
        Assert.That(searchMap.SelectedConditionDisplayName, Is.EqualTo("FlashDeconv"));
        Assert.That(searchMap.Entries, Has.Count.EqualTo(1));
        Assert.That(searchMap.Entries[0].MassSpecFilePath, Is.EqualTo(@"E:\data\sample1.mzML"));
        Assert.That(searchMap.Entries[0].MassSpecFileName, Is.EqualTo("sample1.mzML"));
        Assert.That(searchMap.Entries[0].FeatureFilePath, Is.EqualTo(@"E:\features\flash\sample1_ms1.feature"));
        Assert.That(searchMap.Entries[0].FeatureFileName, Is.EqualTo("sample1_ms1.feature"));
    }

    [Test]
    public static void BuildSearchMap_ThrowsInformativeExceptionWhenConditionMissing()
    {
        var map = new FeatureFileMap();

        var exception = Assert.Throws<FeatureMappingException>(() => map.BuildSearchMap(new[] { @"E:\data\sample1.mzML" }, "flash"));

        Assert.That(exception!.Message, Does.Contain("Condition 'flash' not found in map."));
    }

    [Test]
    public static void BuildSearchMap_ThrowsInformativeExceptionWhenRawFileMissingConditionMapping()
    {
        var map = new FeatureFileMap();
        map.AddOrReplaceCondition(new FeatureFileMapCondition { ConditionKey = "flash", DisplayName = "FlashDeconv" });

        var sample1 = FeatureSpectraEntry.Create(@"E:\data\sample1.mzML");
        sample1.AddOrReplaceConditionFile(FeatureFileConditionEntry.Create("flash", @"E:\features\flash\sample1_ms1.feature"));
        map.AddOrReplaceSpectraEntry(sample1);

        var exception = Assert.Throws<FeatureMappingException>(() => map.BuildSearchMap(
            new[] { @"E:\data\sample1.mzML", @"E:\data\sample2.mzML" },
            "flash"));

        Assert.That(exception!.Message, Does.Contain(@"E:\data\sample2.mzML"));
        Assert.That(exception.Message, Does.Contain("condition 'flash'"));
    }

    [Test]
    public static void ToDeconvolutionParameters_CopiesInheritedDeconvolutionSettings()
    {
        string tempDir = Path.Combine(TestContext.CurrentContext.TestDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var searchMap = new SearchFeatureFileMap
        {
            Entries = new List<SearchFeatureFileMapEntry>
            {
                new()
                {
                    MassSpecFilePath = Path.Combine(tempDir, "sample1.mzML"),
                    MassSpecFileName = "sample1.mzML",
                    FeatureFilePath = Path.Combine(tempDir, "sample1.feature.tsv"),
                    FeatureFileName = "sample1.feature.tsv"
                }
            }
        };

        File.WriteAllText(searchMap.Entries[0].FeatureFilePath, string.Empty);

        var mappedParameters = new FeatureMappedFromFileDeconvolutionParameters(searchMap, 2, 18, Polarity.Negative, new Averagine(), 0.9876)
        {
            UseGenericScore = true
        };

        try
        {
            var resolved = (FromFileDeconvolutionParameters)mappedParameters.ToDeconvolutionParameters(searchMap.Entries[0].MassSpecFilePath);
            Assert.That(resolved.MinAssumedChargeState, Is.EqualTo(2));
            Assert.That(resolved.MaxAssumedChargeState, Is.EqualTo(18));
            Assert.That(resolved.Polarity, Is.EqualTo(Polarity.Negative));
            Assert.That(resolved.ExpectedIsotopeSpacing, Is.EqualTo(0.9876));
            Assert.That(resolved.UseGenericScore, Is.True);
            Assert.That(resolved.AverageResidueModel, Is.TypeOf<Averagine>());
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Test]
    public static void SearchFeatureFileMapClone_DeepClonesEntries()
    {
        var map = new SearchFeatureFileMap
        {
            SourceMapPath = @"E:\maps\feature-map-a.toml",
            SelectedConditionKey = "flash",
            SelectedConditionDisplayName = "FlashDeconv",
            Entries = new List<SearchFeatureFileMapEntry>
            {
                new()
                {
                    MassSpecFilePath = @"E:\data\sample1.mzML",
                    MassSpecFileName = "sample1.mzML",
                    FeatureFilePath = @"E:\features\sample1_ms1.feature",
                    FeatureFileName = "sample1_ms1.feature"
                }
            }
        };

        var clone = map.Clone();
        clone.Entries[0].FeatureFilePath = @"E:\features\changed.feature";

        Assert.That(map.Entries[0].FeatureFilePath, Is.EqualTo(@"E:\features\sample1_ms1.feature"));
        Assert.That(clone.Entries[0].FeatureFilePath, Is.EqualTo(@"E:\features\changed.feature"));
    }

    [Test]
    public static void SearchFeatureFileMap_Equality_IsValueBased()
    {
        var left = new SearchFeatureFileMap
        {
            SourceMapPath = @"E:\maps\feature-map-a.toml",
            SelectedConditionKey = "flash",
            SelectedConditionDisplayName = "FlashDeconv",
            Entries = new List<SearchFeatureFileMapEntry>
            {
                new()
                {
                    MassSpecFilePath = @"E:\data\sample1.mzML",
                    MassSpecFileName = "sample1.mzML",
                    FeatureFilePath = @"E:\features\sample1_ms1.feature",
                    FeatureFileName = "sample1_ms1.feature"
                }
            }
        };

        var right = left.Clone();

        Assert.That(left, Is.EqualTo(right));
        Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
    }

    [Test]
    public static void FeatureMappedFromFileDeconvolutionParameters_Equality_UsesSearchMapValueEquality()
    {
        var searchMap = new SearchFeatureFileMap
        {
            SourceMapPath = @"E:\maps\feature-map-a.toml",
            SelectedConditionKey = "flash",
            SelectedConditionDisplayName = "FlashDeconv",
            Entries = new List<SearchFeatureFileMapEntry>
            {
                new()
                {
                    MassSpecFilePath = @"E:\data\sample1.mzML",
                    MassSpecFileName = "sample1.mzML",
                    FeatureFilePath = @"E:\features\sample1_ms1.feature",
                    FeatureFileName = "sample1_ms1.feature"
                }
            }
        };

        var left = new FeatureMappedFromFileDeconvolutionParameters(searchMap, 2, 18, Polarity.Positive, new Averagine(), 0.9876)
        {
            UseGenericScore = true
        };
        var right = new FeatureMappedFromFileDeconvolutionParameters(searchMap.Clone(), 2, 18, Polarity.Positive, new Averagine(), 0.9876)
        {
            UseGenericScore = true
        };

        Assert.That(left, Is.EqualTo(right));
        Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
    }

    [Test]
    public static void FeatureMappedFromFileDeconvolutionParameters_TomlRoundTrip_PreservesSearchMap()
    {
        var original = new FeatureMappedFromFileDeconvolutionParameters(
            new SearchFeatureFileMap
            {
                SourceMapPath = @"E:\maps\feature-map-a.toml",
                SelectedConditionKey = "flash",
                SelectedConditionDisplayName = "FlashDeconv",
                Entries = new List<SearchFeatureFileMapEntry>
                {
                    new()
                    {
                        MassSpecFilePath = @"E:\data\sample1.mzML",
                        MassSpecFileName = "sample1.mzML",
                        FeatureFilePath = @"E:\features\sample1_ms1.feature",
                        FeatureFileName = "sample1_ms1.feature"
                    },
                    new()
                    {
                        MassSpecFilePath = @"E:\data\sample2.mzML",
                        MassSpecFileName = "sample2.mzML",
                        FeatureFilePath = @"E:\features\sample2_ms1.feature",
                        FeatureFileName = "sample2_ms1.feature"
                    }
                }
            },
            2,
            18,
            Polarity.Negative,
            new Averagine(),
            0.9876)
        {
            UseGenericScore = true
        };

        var searchTask = new SearchTask
        {
            CommonParameters = new CommonParameters(precursorDeconParams: original)
        };

        string toml = Toml.WriteString(searchTask, MetaMorpheusTask.tomlConfig);
        var roundTripped = Toml.ReadString<SearchTask>(toml, MetaMorpheusTask.tomlConfig);
        var parsed = roundTripped.CommonParameters.PrecursorDeconvolutionParameters as FeatureMappedFromFileDeconvolutionParameters;

        Assert.That(parsed, Is.Not.Null);
        Assert.That(parsed, Is.EqualTo(original));
        Assert.That(parsed!.FeatureFileMap, Is.EqualTo(original.FeatureFileMap));
    }

    [Test]
    public static void SetAllFileSpecificCommonParams_MaterializesMappedPrecursorPerRawFile()
    {
        string tempDir = Path.Combine(TestContext.CurrentContext.TestDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string rawFilePath = Path.Combine(tempDir, "sample1.mzML");
            string featureFilePath = Path.Combine(tempDir, "sample1.feature.tsv");
            File.WriteAllText(featureFilePath, string.Empty);

            var mappedParameters = new FeatureMappedFromFileDeconvolutionParameters(
                new SearchFeatureFileMap
                {
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
                },
                3,
                17,
                Polarity.Positive,
                new Averagine(),
                0.9988)
            {
                UseGenericScore = true
            };

            var commonParameters = new CommonParameters(
                precursorDeconParams: mappedParameters,
                productDeconParams: new ClassicDeconvolutionParameters(1, 10, 4, 3));

            var resolved = MetaMorpheusTask.SetAllFileSpecificCommonParams(commonParameters, null, rawFilePath);

            Assert.That(resolved.PrecursorDeconvolutionParameters, Is.TypeOf<FromFileDeconvolutionParameters>());
            var precursor = (FromFileDeconvolutionParameters)resolved.PrecursorDeconvolutionParameters;
            Assert.That(precursor.MinAssumedChargeState, Is.EqualTo(3));
            Assert.That(precursor.MaxAssumedChargeState, Is.EqualTo(17));
            Assert.That(precursor.ExpectedIsotopeSpacing, Is.EqualTo(0.9988));
            Assert.That(precursor.UseGenericScore, Is.True);
            Assert.That(resolved.ProductDeconvolutionParameters, Is.TypeOf<ClassicDeconvolutionParameters>());
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}

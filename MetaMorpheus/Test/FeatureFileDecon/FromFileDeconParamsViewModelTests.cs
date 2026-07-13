using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuiFunctions;
using MassSpectrometry;
using NUnit.Framework;
using TaskLayer.Deconvolution;
using TaskLayer.Deconvolution.FeatureFileMapping;

namespace Test.FeatureFileDecon
{
    [TestFixture]
    public class FromFileDeconParamsViewModelTests
    {
        private string _tempMapPath;

        [SetUp]
        public void SetUp()
        {
            _tempMapPath = Path.Combine(Path.GetTempPath(), "test_fromfile_vms_" + System.Guid.NewGuid().ToString() + ".toml");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_tempMapPath))
            {
                File.Delete(_tempMapPath);
            }
        }

        [Test]
        public void TestRowModelResolvesPath()
        {
            var row = new FromFileFeatureMappingRowModel(
                "C:\\temp\\spec.raw",
                new[] { "CondA", "CondB" },
                (msFile, condKey) => condKey == "CondA" ? "C:\\temp\\featA.txt" : "C:\\temp\\featB.txt"
            );

            Assert.That(row.MassSpecFileName, Is.EqualTo("spec.raw"));
            Assert.That(row.MassSpecFilePath, Is.EqualTo("C:\\temp\\spec.raw"));
            Assert.That(row.AvailableConditions.Count, Is.EqualTo(2));
            
            Assert.That(row.ResolvedFeatureFilePath, Is.Empty);

            row.SelectedCondition = "CondA";
            Assert.That(row.ResolvedFeatureFilePath, Is.EqualTo("C:\\temp\\featA.txt"));

            row.SelectedCondition = "CondB";
            Assert.That(row.ResolvedFeatureFilePath, Is.EqualTo("C:\\temp\\featB.txt"));
        }

        [Test]
        public void TestViewModelInitializesRowsFromGlobalStore()
        {
            var map = new FeatureFileMap();
            var condA = new FeatureFileMapCondition { ConditionKey = "CondA", DisplayName = "Condition A" };
            map.AddOrReplaceCondition(condA);
            
            var entry = FeatureSpectraEntry.Create("C:\\temp\\spec1.raw");
            entry.FeatureFiles.Add(new FeatureFileConditionEntry
            {
                ConditionKey = "CondA",
                FeatureFilePath = "C:\\temp\\feat1.txt"
            });
            map.AddOrReplaceSpectraEntry(entry);
            FeatureFileMapStore.Save(map, _tempMapPath);

            var parameters = new FeatureMappedFromFileDeconvolutionParameters();
            var vm = new FromFileDeconParamsViewModel(parameters, new[] { "C:\\temp\\spec1.raw", "C:\\temp\\spec2.raw" });
            vm.InitializeRows(new[] { "C:\\temp\\spec1.raw", "C:\\temp\\spec2.raw" }, _tempMapPath);

            Assert.That(vm.Rows.Count, Is.EqualTo(2));

            // Row 0 is spec1.raw, should have CondA and auto-select it because it's the only one
            Assert.That(vm.Rows[0].MassSpecFilePath, Is.EqualTo("C:\\temp\\spec1.raw"));
            Assert.That(vm.Rows[0].AvailableConditions.Count, Is.EqualTo(1));
            Assert.That(vm.Rows[0].AvailableConditions[0], Is.EqualTo("CondA"));
            Assert.That(vm.Rows[0].SelectedCondition, Is.EqualTo("CondA"));
            
            // Should be normalized because Save/Load normalizes it
            var expectedFeat1 = Path.GetFullPath("C:\\temp\\feat1.txt");
            Assert.That(vm.Rows[0].ResolvedFeatureFilePath, Is.EqualTo(expectedFeat1));

            // Row 1 is spec2.raw, not in the store, so empty conditions
            Assert.That(vm.Rows[1].MassSpecFilePath, Is.EqualTo("C:\\temp\\spec2.raw"));
            Assert.That(vm.Rows[1].AvailableConditions.Count, Is.EqualTo(0));
            Assert.That(vm.Rows[1].ResolvedFeatureFilePath, Is.Empty);
        }

        [Test]
        public void TestSimplerConstructor_DoesNotInitializeRows()
        {
            var parameters = new FeatureMappedFromFileDeconvolutionParameters();
            var vm = new FromFileDeconParamsViewModel(parameters);

            Assert.That(vm.Parameters, Is.SameAs(parameters));
            Assert.That(vm.DeconvolutionType, Is.EqualTo(DeconvolutionType.FromFile));
            Assert.That(vm.Rows, Is.Empty);
            Assert.That(vm.ToString(), Is.EqualTo("From Feature File"));
        }

        [Test]
        public void TestSimplerConstructor_RowsCanBeInitializedLater()
        {
            var map = new FeatureFileMap();
            var condA = new FeatureFileMapCondition { ConditionKey = "CondA", DisplayName = "Condition A" };
            map.AddOrReplaceCondition(condA);

            var entry = FeatureSpectraEntry.Create("C:\\temp\\spec1.raw");
            entry.FeatureFiles.Add(new FeatureFileConditionEntry
            {
                ConditionKey = "CondA",
                FeatureFilePath = "C:\\temp\\feat1.txt"
            });
            map.AddOrReplaceSpectraEntry(entry);
            FeatureFileMapStore.Save(map, _tempMapPath);

            var parameters = new FeatureMappedFromFileDeconvolutionParameters();
            var vm = new FromFileDeconParamsViewModel(parameters);
            Assert.That(vm.Rows, Is.Empty);

            vm.InitializeRows(new[] { "C:\\temp\\spec1.raw" }, _tempMapPath);

            Assert.That(vm.Rows.Count, Is.EqualTo(1));
            Assert.That(vm.Rows[0].AvailableConditions.Count, Is.EqualTo(1));
            Assert.That(vm.Rows[0].AvailableConditions[0], Is.EqualTo("CondA"));
        }

        [Test]
        public void TestParametersValidation_BlocksOnMissingCondition()
        {
            GuiFunctions.MessageBoxHelper.SuppressMessageBoxes = true;
            try
            {
                var map = new FeatureFileMap();
                var entry = FeatureSpectraEntry.Create("C:\\temp\\spec1.raw");
                // Missing condition assignment
                map.AddOrReplaceSpectraEntry(entry);
                FeatureFileMapStore.Save(map, _tempMapPath);

                var parameters = new FeatureMappedFromFileDeconvolutionParameters();
                var vm = new FromFileDeconParamsViewModel(parameters, new[] { "C:\\temp\\spec1.raw" });
                vm.InitializeRows(new[] { "C:\\temp\\spec1.raw" }, _tempMapPath);

                Assert.That(vm.Rows.Count, Is.EqualTo(1));
                Assert.That(vm.Rows[0].SelectedCondition, Is.Null.Or.Empty);

                Assert.That(vm.MaxAssumedChargeState, Is.EqualTo(0), "ViewModel MaxAssumedChargeState should be 0 on invalid");
                Assert.Throws<System.InvalidOperationException>(() => { var _ = vm.Parameters; }, "Parameters getter should throw on invalid mapping");
            }
            finally
            {
                GuiFunctions.MessageBoxHelper.SuppressMessageBoxes = false;
            }
        }

        [Test]
        public void TestInitializeRows_MultipleConditions_PopulatesAllConditions()
        {
            var map = new FeatureFileMap();
            var condA = new FeatureFileMapCondition { ConditionKey = "CondA", DisplayName = "Condition A" };
            var condB = new FeatureFileMapCondition { ConditionKey = "CondB", DisplayName = "Condition B" };
            map.AddOrReplaceCondition(condA);
            map.AddOrReplaceCondition(condB);

            var entry = FeatureSpectraEntry.Create("C:\\temp\\spec1.raw");
            entry.FeatureFiles.Add(new FeatureFileConditionEntry
            {
                ConditionKey = "CondA",
                FeatureFilePath = "C:\\temp\\featA.txt"
            });
            entry.FeatureFiles.Add(new FeatureFileConditionEntry
            {
                ConditionKey = "CondB",
                FeatureFilePath = "C:\\temp\\featB.txt"
            });
            map.AddOrReplaceSpectraEntry(entry);
            FeatureFileMapStore.Save(map, _tempMapPath);

            var parameters = new FeatureMappedFromFileDeconvolutionParameters();
            var vm = new FromFileDeconParamsViewModel(parameters, new[] { "C:\\temp\\spec1.raw" });
            vm.InitializeRows(new[] { "C:\\temp\\spec1.raw" }, _tempMapPath);

            Assert.That(vm.Rows.Count, Is.EqualTo(1));
            Assert.That(vm.Rows[0].AvailableConditions.Count, Is.EqualTo(2));
            Assert.That(vm.Rows[0].AvailableConditions, Does.Contain("CondA"));
            Assert.That(vm.Rows[0].AvailableConditions, Does.Contain("CondB"));
        }

        [Test]
        public void TestInitializeRows_MultipleConditions_NoAutoSelect()
        {
            var map = new FeatureFileMap();
            var condA = new FeatureFileMapCondition { ConditionKey = "CondA", DisplayName = "Condition A" };
            var condB = new FeatureFileMapCondition { ConditionKey = "CondB", DisplayName = "Condition B" };
            map.AddOrReplaceCondition(condA);
            map.AddOrReplaceCondition(condB);

            var entry = FeatureSpectraEntry.Create("C:\\temp\\spec1.raw");
            entry.FeatureFiles.Add(new FeatureFileConditionEntry
            {
                ConditionKey = "CondA",
                FeatureFilePath = "C:\\temp\\featA.txt"
            });
            entry.FeatureFiles.Add(new FeatureFileConditionEntry
            {
                ConditionKey = "CondB",
                FeatureFilePath = "C:\\temp\\featB.txt"
            });
            map.AddOrReplaceSpectraEntry(entry);
            FeatureFileMapStore.Save(map, _tempMapPath);

            var parameters = new FeatureMappedFromFileDeconvolutionParameters();
            var vm = new FromFileDeconParamsViewModel(parameters, new[] { "C:\\temp\\spec1.raw" });
            vm.InitializeRows(new[] { "C:\\temp\\spec1.raw" }, _tempMapPath);

            Assert.That(vm.Rows.Count, Is.EqualTo(1));
            Assert.That(vm.Rows[0].AvailableConditions.Count, Is.EqualTo(2));
            // When multiple conditions exist and no pre-selection, SelectedCondition should remain empty
            Assert.That(vm.Rows[0].SelectedCondition, Is.Null.Or.Empty);
            Assert.That(vm.Rows[0].ResolvedFeatureFilePath, Is.Empty);
        }

        [Test]
        public void TestMultipleRows_RowIsolation_ChangingConditionDoesNotAffectOtherRow()
        {
            var map = new FeatureFileMap();
            var condA = new FeatureFileMapCondition { ConditionKey = "CondA", DisplayName = "Condition A" };
            var condB = new FeatureFileMapCondition { ConditionKey = "CondB", DisplayName = "Condition B" };
            map.AddOrReplaceCondition(condA);
            map.AddOrReplaceCondition(condB);

            var entry1 = FeatureSpectraEntry.Create("C:\\temp\\spec1.raw");
            entry1.FeatureFiles.Add(new FeatureFileConditionEntry
            {
                ConditionKey = "CondA",
                FeatureFilePath = "C:\\temp\\featA_spec1.txt"
            });
            entry1.FeatureFiles.Add(new FeatureFileConditionEntry
            {
                ConditionKey = "CondB",
                FeatureFilePath = "C:\\temp\\featB_spec1.txt"
            });
            map.AddOrReplaceSpectraEntry(entry1);

            var entry2 = FeatureSpectraEntry.Create("C:\\temp\\spec2.raw");
            entry2.FeatureFiles.Add(new FeatureFileConditionEntry
            {
                ConditionKey = "CondA",
                FeatureFilePath = "C:\\temp\\featA_spec2.txt"
            });
            entry2.FeatureFiles.Add(new FeatureFileConditionEntry
            {
                ConditionKey = "CondB",
                FeatureFilePath = "C:\\temp\\featB_spec2.txt"
            });
            map.AddOrReplaceSpectraEntry(entry2);

            FeatureFileMapStore.Save(map, _tempMapPath);

            var parameters = new FeatureMappedFromFileDeconvolutionParameters();
            var vm = new FromFileDeconParamsViewModel(parameters, new[] { "C:\\temp\\spec1.raw", "C:\\temp\\spec2.raw" });
            vm.InitializeRows(new[] { "C:\\temp\\spec1.raw", "C:\\temp\\spec2.raw" }, _tempMapPath);

            Assert.That(vm.Rows.Count, Is.EqualTo(2));

            // Both rows have multiple conditions, so neither auto-selects
            Assert.That(vm.Rows[0].SelectedCondition, Is.Null.Or.Empty);
            Assert.That(vm.Rows[1].SelectedCondition, Is.Null.Or.Empty);

            // Select CondA on row 0
            vm.Rows[0].SelectedCondition = "CondA";
            var expectedFeat1 = Path.GetFullPath("C:\\temp\\featA_spec1.txt");
            Assert.That(vm.Rows[0].ResolvedFeatureFilePath, Is.EqualTo(expectedFeat1));

            // Row 1 must remain unchanged
            Assert.That(vm.Rows[1].SelectedCondition, Is.Null.Or.Empty);
            Assert.That(vm.Rows[1].ResolvedFeatureFilePath, Is.Empty);

            // Now select CondB on row 1
            vm.Rows[1].SelectedCondition = "CondB";
            var expectedFeat2 = Path.GetFullPath("C:\\temp\\featB_spec2.txt");
            Assert.That(vm.Rows[1].ResolvedFeatureFilePath, Is.EqualTo(expectedFeat2));

            // Row 0 must still have its original resolved path
            Assert.That(vm.Rows[0].ResolvedFeatureFilePath, Is.EqualTo(expectedFeat1));

            // Switch row 0 to CondB
            vm.Rows[0].SelectedCondition = "CondB";
            var expectedFeat1B = Path.GetFullPath("C:\\temp\\featB_spec1.txt");
            Assert.That(vm.Rows[0].ResolvedFeatureFilePath, Is.EqualTo(expectedFeat1B));

            // Row 1 must still be unchanged
            Assert.That(vm.Rows[1].ResolvedFeatureFilePath, Is.EqualTo(expectedFeat2));
        }

        [Test]
        public void TestIsValid_AllRowsValid_ReturnsTrue()
        {
            var map = new FeatureFileMap();
            var condA = new FeatureFileMapCondition { ConditionKey = "CondA", DisplayName = "Condition A" };
            map.AddOrReplaceCondition(condA);

            var entry1 = FeatureSpectraEntry.Create("C:\\temp\\spec1.raw");
            entry1.FeatureFiles.Add(new FeatureFileConditionEntry
            {
                ConditionKey = "CondA",
                FeatureFilePath = "C:\\temp\\feat1.txt"
            });
            map.AddOrReplaceSpectraEntry(entry1);

            var entry2 = FeatureSpectraEntry.Create("C:\\temp\\spec2.raw");
            entry2.FeatureFiles.Add(new FeatureFileConditionEntry
            {
                ConditionKey = "CondA",
                FeatureFilePath = "C:\\temp\\feat2.txt"
            });
            map.AddOrReplaceSpectraEntry(entry2);

            FeatureFileMapStore.Save(map, _tempMapPath);

            var parameters = new FeatureMappedFromFileDeconvolutionParameters();
            var vm = new FromFileDeconParamsViewModel(parameters, new[] { "C:\\temp\\spec1.raw", "C:\\temp\\spec2.raw" });
            vm.InitializeRows(new[] { "C:\\temp\\spec1.raw", "C:\\temp\\spec2.raw" }, _tempMapPath);

            Assert.That(vm.Rows.Count, Is.EqualTo(2));
            // Each row has exactly one condition, so they auto-select and become valid
            Assert.That(vm.Rows[0].SelectedCondition, Is.Not.Null.And.Not.Empty);
            Assert.That(vm.Rows[1].SelectedCondition, Is.Not.Null.And.Not.Empty);
            Assert.That(vm.Rows[0].ResolvedFeatureFilePath, Is.Not.Empty);
            Assert.That(vm.Rows[1].ResolvedFeatureFilePath, Is.Not.Empty);
            Assert.That(vm.IsValid, Is.True);
        }

        [Test]
        public void TestIsValid_RowMissingCondition_ReturnsFalse()
        {
            var map = new FeatureFileMap();
            var condA = new FeatureFileMapCondition { ConditionKey = "CondA", DisplayName = "Condition A" };
            map.AddOrReplaceCondition(condA);

            // spec1.raw in store with CondA -> feat1.txt
            var entry1 = FeatureSpectraEntry.Create("C:\\temp\\spec1.raw");
            entry1.FeatureFiles.Add(new FeatureFileConditionEntry
            {
                ConditionKey = "CondA",
                FeatureFilePath = "C:\\temp\\feat1.txt"
            });
            map.AddOrReplaceSpectraEntry(entry1);

            // spec2.raw NOT in the store -> row will have empty conditions
            FeatureFileMapStore.Save(map, _tempMapPath);

            var parameters = new FeatureMappedFromFileDeconvolutionParameters();
            var vm = new FromFileDeconParamsViewModel(parameters, new[] { "C:\\temp\\spec1.raw", "C:\\temp\\spec2.raw" });
            vm.InitializeRows(new[] { "C:\\temp\\spec1.raw", "C:\\temp\\spec2.raw" }, _tempMapPath);

            Assert.That(vm.Rows.Count, Is.EqualTo(2));
            // spec1.raw valid (auto-selected)
            Assert.That(vm.Rows[0].SelectedCondition, Is.Not.Null.And.Not.Empty);
            Assert.That(vm.Rows[0].ResolvedFeatureFilePath, Is.Not.Empty);
            // spec2.raw has no conditions -> invalid
            Assert.That(vm.Rows[1].AvailableConditions.Count, Is.EqualTo(0));
            Assert.That(vm.Rows[1].SelectedCondition, Is.Null.Or.Empty);
            Assert.That(vm.Rows[1].ResolvedFeatureFilePath, Is.Empty);

            Assert.That(vm.IsValid, Is.False);
        }

        [Test]
        public void TestIsValid_ConditionWithoutMatchingFeatureFile_ReturnsFalse()
        {
            var map = new FeatureFileMap();
            var condA = new FeatureFileMapCondition { ConditionKey = "CondA", DisplayName = "Condition A" };
            map.AddOrReplaceCondition(condA);

            // spec1.raw exists in store but has NO FeatureFileConditionEntry
            var entry = FeatureSpectraEntry.Create("C:\\temp\\spec1.raw");
            map.AddOrReplaceSpectraEntry(entry);

            FeatureFileMapStore.Save(map, _tempMapPath);

            var parameters = new FeatureMappedFromFileDeconvolutionParameters();
            var vm = new FromFileDeconParamsViewModel(parameters, new[] { "C:\\temp\\spec1.raw" });
            vm.InitializeRows(new[] { "C:\\temp\\spec1.raw" }, _tempMapPath);

            Assert.That(vm.Rows.Count, Is.EqualTo(1));
            Assert.That(vm.Rows[0].AvailableConditions.Count, Is.EqualTo(0));
            Assert.That(vm.IsValid, Is.False);
        }

        [Test]
        public void TestParametersGetter_WhenValid_BuildsMapFromRows()
        {
            var map = new FeatureFileMap();
            var condA = new FeatureFileMapCondition { ConditionKey = "CondA", DisplayName = "Condition A" };
            map.AddOrReplaceCondition(condA);

            var entry1 = FeatureSpectraEntry.Create("C:\\temp\\spec1.raw");
            entry1.FeatureFiles.Add(new FeatureFileConditionEntry
            {
                ConditionKey = "CondA",
                FeatureFilePath = "C:\\temp\\feat1.txt"
            });
            map.AddOrReplaceSpectraEntry(entry1);

            var entry2 = FeatureSpectraEntry.Create("C:\\temp\\spec2.raw");
            entry2.FeatureFiles.Add(new FeatureFileConditionEntry
            {
                ConditionKey = "CondA",
                FeatureFilePath = "C:\\temp\\feat2.txt"
            });
            map.AddOrReplaceSpectraEntry(entry2);

            FeatureFileMapStore.Save(map, _tempMapPath);

            var parameters = new FeatureMappedFromFileDeconvolutionParameters();
            var vm = new FromFileDeconParamsViewModel(parameters, new[] { "C:\\temp\\spec1.raw", "C:\\temp\\spec2.raw" });
            vm.InitializeRows(new[] { "C:\\temp\\spec1.raw", "C:\\temp\\spec2.raw" }, _tempMapPath);

            // All rows valid -> Parameters getter builds SearchFeatureFileMap
            var resultParams = vm.Parameters;
            var resultMap = ((FeatureMappedFromFileDeconvolutionParameters)resultParams).FeatureFileMap;

            Assert.That(resultMap, Is.Not.Null);
            Assert.That(resultMap.Entries.Count, Is.EqualTo(2));

            var fileNames = resultMap.Entries.Select(e => e.MassSpecFileName).OrderBy(n => n).ToList();
            Assert.That(fileNames, Is.EquivalentTo(new[] { "spec1.raw", "spec2.raw" }));

            // Each entry should have resolved feature file paths
            var expectedFeat1 = Path.GetFullPath("C:\\temp\\feat1.txt");
            var expectedFeat2 = Path.GetFullPath("C:\\temp\\feat2.txt");
            var actualPaths = resultMap.Entries.Select(e => e.FeatureFilePath).OrderBy(p => p).ToList();
            Assert.That(actualPaths, Is.EquivalentTo(new[] { expectedFeat1, expectedFeat2 }));

            // SourceMapPath should be set (from the store file path)
            Assert.That(resultMap.SourceMapPath, Is.Not.Empty);
            Assert.That(resultMap.SourceMapPath, Does.EndWith(".toml"));
        }
    }
}

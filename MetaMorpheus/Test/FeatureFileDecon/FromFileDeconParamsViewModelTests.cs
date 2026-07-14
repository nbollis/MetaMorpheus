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

        // ---- Row model ----

        [Test]
        public void TestRowModel_ExposesMassSpecFileMetadata()
        {
            var row = new FromFileFeatureMappingRowModel("C:\\temp\\spec.raw");

            Assert.That(row.MassSpecFileName, Is.EqualTo("spec.raw"));
            Assert.That(row.MassSpecFilePath, Is.EqualTo("C:\\temp\\spec.raw"));
            Assert.That(row.ResolvedFeatureFilePath, Is.Empty);
        }

        [Test]
        public void TestRowModel_ResolvedPathIsSettable()
        {
            var row = new FromFileFeatureMappingRowModel("C:\\temp\\spec.raw")
            {
                ResolvedFeatureFilePath = "C:\\temp\\feat.txt"
            };
            Assert.That(row.ResolvedFeatureFilePath, Is.EqualTo("C:\\temp\\feat.txt"));

            // null normalizes to empty
            row.ResolvedFeatureFilePath = null;
            Assert.That(row.ResolvedFeatureFilePath, Is.Empty);
        }

        // ---- ViewModel: store discovery + shared condition ----

        [Test]
        public void TestViewModelInitializesRowsFromGlobalStore_AndAutoPicksSoleCondition()
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

            // Only CondA is available, so the VM auto-picks it as the shared condition.
            Assert.That(vm.AvailableConditions, Is.EquivalentTo(new[] { "CondA" }));
            Assert.That(vm.SelectedCondition, Is.EqualTo("CondA"));

            var expectedFeat1 = Path.GetFullPath("C:\\temp\\feat1.txt");

            // Row 0 is spec1.raw, resolved against the shared condition
            Assert.That(vm.Rows[0].MassSpecFilePath, Is.EqualTo("C:\\temp\\spec1.raw"));
            Assert.That(vm.Rows[0].ResolvedFeatureFilePath, Is.EqualTo(expectedFeat1));

            // Row 1 is spec2.raw, not in the store → resolved path is empty (invalid mapping)
            Assert.That(vm.Rows[1].MassSpecFilePath, Is.EqualTo("C:\\temp\\spec2.raw"));
            Assert.That(vm.Rows[1].ResolvedFeatureFilePath, Is.Empty);
            Assert.That(vm.IsValid, Is.False, "IsValid should be false when any row has no resolved path");
        }

        [Test]
        public void TestSimplerConstructor_DoesNotInitializeRows()
        {
            var parameters = new FeatureMappedFromFileDeconvolutionParameters();
            var vm = new FromFileDeconParamsViewModel(parameters);

            Assert.That(vm.DeconvolutionType, Is.EqualTo(DeconvolutionType.FromFile),
                "DeconvolutionType must be readable on a fresh VM without throwing");
            Assert.That(vm.Rows, Is.Empty);
            Assert.That(vm.AvailableConditions, Is.Empty);
            Assert.That(vm.SelectedCondition, Is.Empty);
            // Pre-mapping state: empty rows are vacuously valid, NOT an exception.
            Assert.That(vm.IsValid, Is.True);
            Assert.That(vm.ToString(), Is.EqualTo("From Feature File"));

            // Parameters getter does not throw on the pre-mapping state. It returns the
            // underlying (unmapped) parameters. The save/run gating is on task-readiness
            // elsewhere, not on this getter.
            Assert.That(vm.Parameters, Is.SameAs(parameters));
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
            Assert.That(vm.AvailableConditions, Is.EquivalentTo(new[] { "CondA" }));
            Assert.That(vm.SelectedCondition, Is.EqualTo("CondA"));
        }

        // ---- Regression: fresh-search / switch-to-FromFile must not throw ----

        [Test]
        public void TestFreshSearch_SwitchingToFromFile_DoesNotThrow()
        {
            // Simulates a brand-new search where the user switches to FromFile before
            // dropping any feature files. The host selects the FromFile VM via type
            // (see DeconHostViewModel fix), so .Parameters must NOT be evaluated at that
            // point. Even if it were, the VM should remain in a valid no-throw state
            // when there are simply no rows.
            var vm = new FromFileDeconParamsViewModel(new FeatureMappedFromFileDeconvolutionParameters());

            Assert.That(vm.Rows, Is.Empty);
            Assert.That(vm.AvailableConditions, Is.Empty);
            Assert.That(vm.SelectedCondition, Is.Empty);
            // Empty Rows is the pre-mapping state — vacuously valid (not an exception).
            // The save/run path will not throw here, but a task that hasn't been
            // configured is still not a runnable task; that gating happens elsewhere.
            Assert.That(vm.IsValid, Is.True, "Empty Rows is the pre-mapping state, vacuously valid");

            // Constructing the VM (which is the same code path exercised when the user
            // switches to FromFile in the GUI) must not throw — neither .DeconvolutionType
            // (now a constant override) nor .IsValid nor .Parameters should throw on a
            // brand-new, unconfigured VM.
            Assert.DoesNotThrow(() =>
            {
                _ = vm.DeconvolutionType;
                _ = vm.IsValid;
                _ = vm.Parameters;
            });
        }

        [Test]
        public void TestInitializeRows_WithEmptyRawFileList_DoesNotThrow()
        {
            var map = new FeatureFileMap();
            var condA = new FeatureFileMapCondition { ConditionKey = "CondA", DisplayName = "Condition A" };
            map.AddOrReplaceCondition(condA);
            FeatureFileMapStore.Save(map, _tempMapPath);

            var parameters = new FeatureMappedFromFileDeconvolutionParameters();
            var vm = new FromFileDeconParamsViewModel(parameters);

            Assert.DoesNotThrow(() => vm.InitializeRows(new string[0], _tempMapPath));
            Assert.That(vm.Rows, Is.Empty);
            // Pre-mapping state: no rows, so IsValid is vacuously true (no exception).
            Assert.That(vm.IsValid, Is.True);
        }

        [Test]
        public void TestParametersValidation_BlocksOnMissingSharedCondition()
        {
            GuiFunctions.MessageBoxHelper.SuppressMessageBoxes = true;
            try
            {
                var map = new FeatureFileMap();
                // No conditions, no entries for the spectra file
                map.AddOrReplaceSpectraEntry(FeatureSpectraEntry.Create("C:\\temp\\spec1.raw"));
                FeatureFileMapStore.Save(map, _tempMapPath);

                var parameters = new FeatureMappedFromFileDeconvolutionParameters();
                var vm = new FromFileDeconParamsViewModel(parameters, new[] { "C:\\temp\\spec1.raw" });
                vm.InitializeRows(new[] { "C:\\temp\\spec1.raw" }, _tempMapPath);

                Assert.That(vm.Rows.Count, Is.EqualTo(1));
                // No condition in store → SelectedCondition must remain empty
                Assert.That(vm.SelectedCondition, Is.Null.Or.Empty);
                Assert.That(vm.AvailableConditions, Is.Empty);
                Assert.That(vm.IsValid, Is.False);

                // Validation surface still blocks save/run on invalid mapping
                Assert.That(vm.MaxAssumedChargeState, Is.EqualTo(0), "ViewModel MaxAssumedChargeState should be 0 on invalid");
                Assert.Throws<System.InvalidOperationException>(() => { var _ = vm.Parameters; },
                    "Parameters getter should throw on invalid mapping");
            }
            finally
            {
                GuiFunctions.MessageBoxHelper.SuppressMessageBoxes = false;
            }
        }

        // ---- Multiple conditions, NO auto-select ----

        [Test]
        public void TestInitializeRows_MultipleConditions_DoesNotAutoSelect()
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
            Assert.That(vm.AvailableConditions, Is.EquivalentTo(new[] { "CondA", "CondB" }));
            // Multiple conditions → user must pick one; no auto-select
            Assert.That(vm.SelectedCondition, Is.Null.Or.Empty);
            Assert.That(vm.Rows[0].ResolvedFeatureFilePath, Is.Empty);
            Assert.That(vm.IsValid, Is.False);
        }

        // ---- Shared condition behavior: changing it updates ALL rows ----

        [Test]
        public void TestSharedCondition_ChangingConditionUpdatesAllRows()
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
            Assert.That(vm.AvailableConditions, Is.EquivalentTo(new[] { "CondA", "CondB" }));
            Assert.That(vm.SelectedCondition, Is.Null.Or.Empty,
                "No pre-selection and multiple conditions → user must pick");

            // Pick CondA
            vm.SelectedCondition = "CondA";
            var expectedFeat1A = Path.GetFullPath("C:\\temp\\featA_spec1.txt");
            var expectedFeat2A = Path.GetFullPath("C:\\temp\\featA_spec2.txt");
            Assert.That(vm.Rows[0].ResolvedFeatureFilePath, Is.EqualTo(expectedFeat1A));
            Assert.That(vm.Rows[1].ResolvedFeatureFilePath, Is.EqualTo(expectedFeat2A));

            // Switch to CondB → both rows recompute
            vm.SelectedCondition = "CondB";
            var expectedFeat1B = Path.GetFullPath("C:\\temp\\featB_spec1.txt");
            var expectedFeat2B = Path.GetFullPath("C:\\temp\\featB_spec2.txt");
            Assert.That(vm.Rows[0].ResolvedFeatureFilePath, Is.EqualTo(expectedFeat1B));
            Assert.That(vm.Rows[1].ResolvedFeatureFilePath, Is.EqualTo(expectedFeat2B));

            // Clear → both rows blank
            vm.SelectedCondition = string.Empty;
            Assert.That(vm.Rows[0].ResolvedFeatureFilePath, Is.Empty);
            Assert.That(vm.Rows[1].ResolvedFeatureFilePath, Is.Empty);
        }

        [Test]
        public void TestSharedCondition_OneRowMissingPath_LeavesIsValidFalse()
        {
            var map = new FeatureFileMap();
            var condA = new FeatureFileMapCondition { ConditionKey = "CondA", DisplayName = "Condition A" };
            map.AddOrReplaceCondition(condA);

            // spec1.raw has CondA → feat1.txt
            var entry1 = FeatureSpectraEntry.Create("C:\\temp\\spec1.raw");
            entry1.FeatureFiles.Add(new FeatureFileConditionEntry
            {
                ConditionKey = "CondA",
                FeatureFilePath = "C:\\temp\\feat1.txt"
            });
            map.AddOrReplaceSpectraEntry(entry1);
            // spec2.raw not in store → no entry

            FeatureFileMapStore.Save(map, _tempMapPath);

            var parameters = new FeatureMappedFromFileDeconvolutionParameters();
            var vm = new FromFileDeconParamsViewModel(parameters, new[] { "C:\\temp\\spec1.raw", "C:\\temp\\spec2.raw" });
            vm.InitializeRows(new[] { "C:\\temp\\spec1.raw", "C:\\temp\\spec2.raw" }, _tempMapPath);

            // CondA auto-selected because it's the only one
            Assert.That(vm.SelectedCondition, Is.EqualTo("CondA"));
            var expectedFeat1 = Path.GetFullPath("C:\\temp\\feat1.txt");
            Assert.That(vm.Rows[0].ResolvedFeatureFilePath, Is.EqualTo(expectedFeat1));
            Assert.That(vm.Rows[1].ResolvedFeatureFilePath, Is.Empty);
            Assert.That(vm.IsValid, Is.False,
                "Even with the shared condition set, IsValid stays false when any row is unmapped");
        }

        [Test]
        public void TestSharedCondition_AllRowsMapped_AllValid()
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

            Assert.That(vm.SelectedCondition, Is.EqualTo("CondA"));
            Assert.That(vm.Rows[0].ResolvedFeatureFilePath, Is.Not.Empty);
            Assert.That(vm.Rows[1].ResolvedFeatureFilePath, Is.Not.Empty);
            Assert.That(vm.IsValid, Is.True);
        }

        // ---- Reopen / restore ----

        [Test]
        public void TestReopen_RestoresSharedConditionFromEmbeddedMap()
        {
            // Saved task: an embedded SearchFeatureFileMap that already pins a shared condition.
            // On reopen, InitializeRows must restore that single shared condition.
            var embeddedMap = new SearchFeatureFileMap
            {
                SourceMapPath = _tempMapPath,
                SelectedConditionKey = "CondB",
                SelectedConditionDisplayName = "Condition B",
                Entries = new List<SearchFeatureFileMapEntry>
                {
                    new() { MassSpecFilePath = "C:\\temp\\spec1.raw", MassSpecFileName = "spec1.raw", FeatureFilePath = "C:\\temp\\feat1.txt", FeatureFileName = "feat1.txt" }
                }
            };
            var parameters = new FeatureMappedFromFileDeconvolutionParameters(embeddedMap);

            // Global store contains both CondA and CondB; spec1 has both, so reopen should
            // restore CondB as the shared condition (not auto-pick the only-available one).
            var map = new FeatureFileMap();
            var condA = new FeatureFileMapCondition { ConditionKey = "CondA", DisplayName = "Condition A" };
            var condB = new FeatureFileMapCondition { ConditionKey = "CondB", DisplayName = "Condition B" };
            map.AddOrReplaceCondition(condA);
            map.AddOrReplaceCondition(condB);

            var entry1 = FeatureSpectraEntry.Create("C:\\temp\\spec1.raw");
            entry1.FeatureFiles.Add(new FeatureFileConditionEntry
            {
                ConditionKey = "CondA",
                FeatureFilePath = "C:\\temp\\feat1_condA.txt"
            });
            entry1.FeatureFiles.Add(new FeatureFileConditionEntry
            {
                ConditionKey = "CondB",
                FeatureFilePath = "C:\\temp\\feat1.txt"
            });
            map.AddOrReplaceSpectraEntry(entry1);
            FeatureFileMapStore.Save(map, _tempMapPath);

            var vm = new FromFileDeconParamsViewModel(parameters);
            vm.InitializeRows(new[] { "C:\\temp\\spec1.raw" }, _tempMapPath);

            Assert.That(vm.SelectedCondition, Is.EqualTo("CondB"),
                "Reopen must restore the shared condition from the embedded map, not auto-pick");
            var expected = Path.GetFullPath("C:\\temp\\feat1.txt");
            Assert.That(vm.Rows[0].ResolvedFeatureFilePath, Is.EqualTo(expected));
        }

        [Test]
        public void TestReopen_EmbeddedConditionMissingFromStore_StillRestored()
        {
            // The previously-saved condition is no longer present in the global store,
            // but the embedded map has it. The dropdown must still contain it and the
            // VM must restore it as the shared condition on reopen.
            var embeddedMap = new SearchFeatureFileMap
            {
                SourceMapPath = _tempMapPath,
                SelectedConditionKey = "GhostCondition",
                SelectedConditionDisplayName = "Ghost",
                Entries = new List<SearchFeatureFileMapEntry>()
            };
            var parameters = new FeatureMappedFromFileDeconvolutionParameters(embeddedMap);

            // Empty global store (no conditions)
            var map = new FeatureFileMap();
            FeatureFileMapStore.Save(map, _tempMapPath);

            var vm = new FromFileDeconParamsViewModel(parameters);
            vm.InitializeRows(new[] { "C:\\temp\\spec1.raw" }, _tempMapPath);

            Assert.That(vm.AvailableConditions, Does.Contain("GhostCondition"),
                "Embedded condition must be in the dropdown even if the store doesn't list it");
            Assert.That(vm.SelectedCondition, Is.EqualTo("GhostCondition"));
        }

        // ---- Parameters getter rebuilds the embedded map from the SHARED condition ----

        [Test]
        public void TestParametersGetter_WhenValid_BuildsMapFromSharedCondition()
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

            // All rows valid → Parameters getter rebuilds SearchFeatureFileMap from the
            // SINGLE shared SelectedCondition (not from per-row state).
            var resultParams = vm.Parameters;
            var resultMap = ((FeatureMappedFromFileDeconvolutionParameters)resultParams).FeatureFileMap;

            Assert.That(resultMap, Is.Not.Null);
            Assert.That(resultMap.SelectedConditionKey, Is.EqualTo("CondA"));
            Assert.That(resultMap.SelectedConditionDisplayName, Is.EqualTo("Condition A"));
            Assert.That(resultMap.Entries.Count, Is.EqualTo(2));

            var fileNames = resultMap.Entries.Select(e => e.MassSpecFileName).OrderBy(n => n).ToList();
            Assert.That(fileNames, Is.EquivalentTo(new[] { "spec1.raw", "spec2.raw" }));

            var expectedFeat1 = Path.GetFullPath("C:\\temp\\feat1.txt");
            var expectedFeat2 = Path.GetFullPath("C:\\temp\\feat2.txt");
            var actualPaths = resultMap.Entries.Select(e => e.FeatureFilePath).OrderBy(p => p).ToList();
            Assert.That(actualPaths, Is.EquivalentTo(new[] { expectedFeat1, expectedFeat2 }));

            // SourceMapPath is audit metadata, set from the global store path
            Assert.That(resultMap.SourceMapPath, Is.Not.Empty);
            Assert.That(resultMap.SourceMapPath, Does.EndWith(".toml"));
        }

        [Test]
        public void TestParametersGetter_DisplayNameFromStore_WhenConditionKnown()
        {
            // When the selected condition has a registered display name in the store,
            // the rebuilt SearchFeatureFileMap.SelectedConditionDisplayName must use it.
            var map = new FeatureFileMap();
            var condA = new FeatureFileMapCondition { ConditionKey = "CondA", DisplayName = "Pretty Display Name" };
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
            var vm = new FromFileDeconParamsViewModel(parameters, new[] { "C:\\temp\\spec1.raw" });
            vm.InitializeRows(new[] { "C:\\temp\\spec1.raw" }, _tempMapPath);

            var resultMap = ((FeatureMappedFromFileDeconvolutionParameters)vm.Parameters).FeatureFileMap;
            Assert.That(resultMap.SelectedConditionDisplayName, Is.EqualTo("Pretty Display Name"));
        }
    }
}

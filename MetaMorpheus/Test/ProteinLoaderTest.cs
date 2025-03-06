using EngineLayer;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework.Legacy;
using Proteomics;
using TaskLayer;
using UsefulProteomicsDatabases;

namespace Test
{
    [TestFixture]
    public class ProteinLoaderTest
    {
        [Test]
        public void ReadEmptyFasta()
        {
            new ProteinLoaderTask("").Run(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "empty.fa"));
        }

        [Test]
        public void ReadFastaWithEmptyEntry()
        {
            new ProteinLoaderTask("").Run(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "oneEmptyEntry.fa"));
        }

        [Test]
        public void TestProteinLoad()
        {
            new ProteinLoaderTask("").Run(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "gapdh.fasta"));
            new ProteinLoaderTask("").Run(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "gapdh.fa"));
            new ProteinLoaderTask("").Run(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "gapdh.fasta.gz"));
            new ProteinLoaderTask("").Run(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "gapdh.fa.gz"));
        }

        [Test]
        [TestCase("LowResSnip_B6_mouse_11700_117500.xml", DecoyType.None)]
        [TestCase("ProteaseModTest.fasta", DecoyType.None)]
        [TestCase("LowResSnip_B6_mouse_11700_117500.xml", DecoyType.Reverse)]
        [TestCase("ProteaseModTest.fasta", DecoyType.Reverse)]
        public void LoadingIsReproducible(string fileName, DecoyType decoyType)
        {
            // Load in proteins
            var dbPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "DatabaseTests", fileName);
            var task1 = new ProteinLoaderTask("");
            var task2 = new ProteinLoaderTask("");

            task1.Run(dbPath, decoyType);
            task2.Run(dbPath, decoyType);

            var proteins1 = task1.Proteins;
            var proteins2 = task2.Proteins;
            // check are equivalent lists of proteins
            Assert.That(proteins1.Count, Is.EqualTo(proteins2.Count));

            for (int i = 0; i < proteins1.Count; i++)
            {
                var protein1 = proteins1[i];
                var protein2 = proteins2[i];

                Assert.That(protein1.Accession, Is.EqualTo(protein2.Accession));
                Assert.That(protein1.BaseSequence, Is.EqualTo(protein2.BaseSequence));
                CollectionAssert.AreEquivalent(protein1.OneBasedPossibleLocalizedModifications, protein2.OneBasedPossibleLocalizedModifications);
            }
        }

        public class ProteinLoaderTask : MetaMorpheusTask
        {
            public List<Protein> Proteins { get; set; }

            private DecoyType _decoyType { get; set; }

            public ProteinLoaderTask(string x)
                : this()
            { }

            protected ProteinLoaderTask()
                : base(MyTask.Search)
            { }

            public void Run(string dbPath, DecoyType decoyType = DecoyType.None)
            {
                _decoyType = decoyType;
                RunSpecific("", new List<DbForTask> { new DbForTask(dbPath, false) }, null, "", null);
            }

            protected override MyTaskResults RunSpecific(string OutputFolder, List<DbForTask> dbFilenameList, List<string> currentRawFileList, string taskId, FileSpecificParameters[] fileSettingsList)
            {
                Proteins = LoadProteins("", dbFilenameList, true, _decoyType, new List<string>(), new CommonParameters());
                return null;
            }
        }
    }
}
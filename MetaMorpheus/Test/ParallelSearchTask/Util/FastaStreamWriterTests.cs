using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace Test.ParallelSearchTask.Util;

public class FastaStreamWriterTests
{
    private string _testDir = null!;
    private readonly Assembly _taskLayerAssembly;
    private readonly Type _writerType;

    public FastaStreamWriterTests()
    {
        _taskLayerAssembly = Assembly.Load("TaskLayer");
        _writerType = _taskLayerAssembly.GetType("TaskLayer.ParallelSearch.Util.FastaStreamWriter")!;
    }

    [SetUp]
    public void SetUp()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"FastaWriterTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private object CreateWriter(string filePath, bool append = false, bool checkDuplicates = true)
    {
        return _writerType.GetConstructor(new[] { typeof(string), typeof(bool), typeof(bool) })!
            .Invoke(new object[] { filePath, append, checkDuplicates });
    }

    [Test]
    public void WriteProtein_WithNullHeader_ReturnsFalse()
    {
        var writer = CreateWriter(Path.Combine(_testDir, "test.fasta"), false, false);
        var method = _writerType.GetMethod("WriteProtein", new[] { typeof(string), typeof(string) });

        bool result = (bool)method!.Invoke(writer, new object[] { null!, "ACGT" })!;

        Assert.That(result, Is.False);
    }

    [Test]
    public void WriteProtein_WithEmptyHeader_ReturnsFalse()
    {
        var writer = CreateWriter(Path.Combine(_testDir, "test.fasta"), false, false);
        var method = _writerType.GetMethod("WriteProtein", new[] { typeof(string), typeof(string) });

        bool result = (bool)method!.Invoke(writer, new object[] { "", "ACGT" })!;

        Assert.That(result, Is.False);
    }

    [Test]
    public void WriteProtein_WithNullSequence_ReturnsFalse()
    {
        var writer = CreateWriter(Path.Combine(_testDir, "test.fasta"), false, false);
        var method = _writerType.GetMethod("WriteProtein", new[] { typeof(string), typeof(string) });

        bool result = (bool)method!.Invoke(writer, new object[] { "protein1", null! })!;

        Assert.That(result, Is.False);
    }

    [Test]
    public void WriteProtein_WithEmptySequence_ReturnsFalse()
    {
        var writer = CreateWriter(Path.Combine(_testDir, "test.fasta"), false, false);
        var method = _writerType.GetMethod("WriteProtein", new[] { typeof(string), typeof(string) });

        bool result = (bool)method!.Invoke(writer, new object[] { "protein1", "" })!;

        Assert.That(result, Is.False);
    }

    [Test]
    public void WriteProtein_WithValidEntry_ReturnsTrue()
    {
        var writer = CreateWriter(Path.Combine(_testDir, "test.fasta"), false, false);
        var method = _writerType.GetMethod("WriteProtein", new[] { typeof(string), typeof(string) });

        bool result = (bool)method!.Invoke(writer, new object[] { "protein1", "ACGTACGT" })!;

        Assert.That(result, Is.True);
    }

    [Test]
    public void WriteProtein_WithDuplicate_TracksAndSkips()
    {
        var writer = CreateWriter(Path.Combine(_testDir, "test.fasta"), false, true);
        var method = _writerType.GetMethod("WriteProtein", new[] { typeof(string), typeof(string) });

        bool first = (bool)method!.Invoke(writer, new object[] { "protein1", "ACGT" })!;
        bool second = (bool)method!.Invoke(writer, new object[] { "protein1", "GGGG" })!;

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
        });
    }

    [Test]
    public void WriteProtein_FastaFormat_WrapsSequenceAt60Chars()
    {
        string filePath = Path.Combine(_testDir, "test.fasta");
        object writer = CreateWriter(filePath, false, false);
        var method = _writerType.GetMethod("WriteProtein", new[] { typeof(string), typeof(string) });
        method!.Invoke(writer, new object[] { "protein1", new string('A', 150) });
        
        var disposeMethod = _writerType.GetMethod("Dispose");
        disposeMethod!.Invoke(writer, null);

        var content = File.ReadAllText(filePath);
        var lines = content.Split('\n');

        Assert.Multiple(() =>
        {
            Assert.That(lines[0], Does.Contain(">protein1"));
            Assert.That(lines[1].Trim().Length, Is.EqualTo(60));
            Assert.That(lines[2].Trim().Length, Is.EqualTo(60));
            Assert.That(lines[3].Trim().Length, Is.EqualTo(30));
        });
    }

    [Test]
    public void WriteProteins_WithMultipleEntries_WritesAll()
    {
        var writer = CreateWriter(Path.Combine(_testDir, "test.fasta"), false, false);
        var method = _writerType.GetMethod("WriteProteins", new[] { typeof(List<(string, string)>) });

        var proteins = new List<(string, string)>
        {
            ("protein1", "ACGT"),
            ("protein2", "GGGG"),
            ("protein3", "TTTT")
        };

        int written = (int)method!.Invoke(writer, new object[] { proteins })!;

        Assert.That(written, Is.EqualTo(3));
    }

    [Test]
    public void WriteProteins_WithDuplicates_SkipsDuplicates()
    {
        var writer = CreateWriter(Path.Combine(_testDir, "test.fasta"), false, true);
        var method = _writerType.GetMethod("WriteProteins", new[] { typeof(List<(string, string)>) });

        var proteins = new List<(string, string)>
        {
            ("protein1", "ACGT"),
            ("protein1", "GGGG"),
            ("protein2", "TTTT")
        };

        int written = (int)method!.Invoke(writer, new object[] { proteins })!;

        Assert.That(written, Is.EqualTo(2));
    }

    [Test]
    public void ProteinsWritten_ReturnsCorrectCount()
    {
        var writer = CreateWriter(Path.Combine(_testDir, "test.fasta"), false, true);
        var method = _writerType.GetMethod("WriteProtein", new[] { typeof(string), typeof(string) });
        var prop = _writerType.GetProperty("ProteinsWritten");

        method!.Invoke(writer, new object[] { "p1", "ACGT" });
        method!.Invoke(writer, new object[] { "p2", "GGGG" });
        method!.Invoke(writer, new object[] { "p1", "TTTT" }); // duplicate

        int count = (int)prop!.GetValue(writer)!;

        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public void HasWritten_WithExistentHeader_ReturnsTrue()
    {
        var writer = CreateWriter(Path.Combine(_testDir, "test.fasta"), false, true);
        var writeMethod = _writerType.GetMethod("WriteProtein", new[] { typeof(string), typeof(string) });
        var hasMethod = _writerType.GetMethod("HasWritten", new[] { typeof(string) });

        writeMethod!.Invoke(writer, new object[] { "protein1", "ACGT" });

        bool result = (bool)hasMethod!.Invoke(writer, new object[] { "protein1" })!;

        Assert.That(result, Is.True);
    }

    [Test]
    public void HasWritten_WithNonexistentHeader_ReturnsFalse()
    {
        var writer = CreateWriter(Path.Combine(_testDir, "test.fasta"), false, true);
        var hasMethod = _writerType.GetMethod("HasWritten", new[] { typeof(string) });

        bool result = (bool)hasMethod!.Invoke(writer, new object[] { "nonexistent" })!;

        Assert.That(result, Is.False);
    }

    [Test]
    public void HasWritten_WithCheckDuplicatesDisabled_ReturnsFalse()
    {
        var writer = CreateWriter(Path.Combine(_testDir, "test.fasta"), false, false);
        var writeMethod = _writerType.GetMethod("WriteProtein", new[] { typeof(string), typeof(string) });
        var hasMethod = _writerType.GetMethod("HasWritten", new[] { typeof(string) });

        writeMethod!.Invoke(writer, new object[] { "protein1", "ACGT" });

        bool result = (bool)hasMethod!.Invoke(writer, new object[] { "protein1" })!;

        Assert.That(result, Is.False);
    }

    [Test]
    public void Flush_WithValidWriter_DoesNotThrow()
    {
        var writer = CreateWriter(Path.Combine(_testDir, "test.fasta"), false, false);
        var writeMethod = _writerType.GetMethod("WriteProtein", new[] { typeof(string), typeof(string) });
        var flushMethod = _writerType.GetMethod("Flush");

        writeMethod!.Invoke(writer, new object[] { "protein1", "ACGT" });

        Assert.That(() => flushMethod!.Invoke(writer, null), Throws.Nothing);
    }

    [Test]
    public void WriteAfterDispose_ThrowsObjectDisposedException()
    {
        var writer = CreateWriter(Path.Combine(_testDir, "test.fasta"), false, false);
        var disposeMethod = _writerType.GetMethod("Dispose");
        var writeMethod = _writerType.GetMethod("WriteProtein", new[] { typeof(string), typeof(string) });

        disposeMethod!.Invoke(writer, null);

        Assert.Throws<TargetInvocationException>(() => writeMethod!.Invoke(writer, new object[] { "p1", "ACGT" }));
    }
}
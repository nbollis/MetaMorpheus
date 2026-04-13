using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TaskLayer.ParallelSearch;

namespace Test.ParallelSearchTask.Util;

[TestFixture]
public class FastaUtilitiesTests
{
    [Test]
    public void ReadFastaChunks_WithNonPositiveChunkSize_Throws()
    {
        string fasta = CreateTempFasta(">p1", "ACDE");
        try
        {
            Type readerType = GetReaderType();
            MethodInfo method = readerType.GetMethod("ReadFastaChunks", BindingFlags.Static | BindingFlags.NonPublic)!;
            var enumerable = (IEnumerable)method.Invoke(null, new object[] { fasta, 0 })!;
            IEnumerator enumerator = enumerable.GetEnumerator();

            Assert.That(() => enumerator.MoveNext(), Throws.TypeOf<ArgumentOutOfRangeException>());
        }
        finally
        {
            File.Delete(fasta);
        }
    }

    [Test]
    public void CombineFastas_SkipsDuplicateHeaders_AndWrapsSequenceAt60()
    {
        string input1 = CreateTempFasta(">A", new string('M', 65), ">B", "PEPTIDE");
        string input2 = CreateTempFasta(">A", "SHOULD_SKIP", ">C", "ACDEFG");
        string output = Path.Combine(Path.GetTempPath(), $"combined_{Path.GetRandomFileName()}.fasta");

        try
        {
            Type readerType = GetReaderType();
            MethodInfo method = readerType.GetMethod("CombineFastas", BindingFlags.Static | BindingFlags.NonPublic)!;
            int written = (int)method.Invoke(null, new object[] { new[] { input1, input2 }, output })!;
            string[] lines = File.ReadAllLines(output);

            Assert.Multiple(() =>
            {
                Assert.That(written, Is.EqualTo(3));
                Assert.That(lines.Count(p => p.StartsWith(">")), Is.EqualTo(3));
                Assert.That(lines.Count(p => p == ">A"), Is.EqualTo(1));
                Assert.That(lines.Any(p => p.Length == 60), Is.True);
                Assert.That(lines.Any(p => p.Length == 5), Is.True);
            });
        }
        finally
        {
            SafeDelete(input1);
            SafeDelete(input2);
            SafeDelete(output);
        }
    }

    [Test]
    public void FastaStreamWriter_SkipsDuplicates_AndThrowsAfterDispose()
    {
        string output = Path.Combine(Path.GetTempPath(), $"writer_{Path.GetRandomFileName()}.fasta");
        object writer = CreateWriter(output);

        try
        {
            bool first = (bool)Invoke(writer, "WriteProtein", "P1", "ACDEFG");
            bool duplicate = (bool)Invoke(writer, "WriteProtein", "P1", "ACDEFG");
            int proteinsWritten = (int)writer.GetType().GetProperty("ProteinsWritten")!.GetValue(writer)!;

            Assert.Multiple(() =>
            {
                Assert.That(first, Is.True);
                Assert.That(duplicate, Is.False);
                Assert.That(proteinsWritten, Is.EqualTo(1));
            });

            Invoke(writer, "Dispose");
            Assert.That(() => Invoke(writer, "WriteProtein", "P2", "AAAA"), Throws.TypeOf<TargetInvocationException>());
        }
        finally
        {
            SafeDelete(output);
        }
    }

    private static Type GetReaderType()
    {
        return typeof(ParallelSearchParameters).Assembly.GetType("TaskLayer.ParallelSearch.Util.FastaStreamReader", true)!;
    }

    private static object CreateWriter(string path)
    {
        Type writerType = typeof(ParallelSearchParameters).Assembly.GetType("TaskLayer.ParallelSearch.Util.FastaStreamWriter", true)!;
        return Activator.CreateInstance(writerType, path, false, true)!;
    }

    private static object? Invoke(object instance, string methodName, params object[] args)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        return method.Invoke(instance, args);
    }

    private static string CreateTempFasta(params string[] lines)
    {
        string path = Path.Combine(Path.GetTempPath(), $"fasta_{Path.GetRandomFileName()}.fasta");
        File.WriteAllLines(path, lines);
        return path;
    }

    private static void SafeDelete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

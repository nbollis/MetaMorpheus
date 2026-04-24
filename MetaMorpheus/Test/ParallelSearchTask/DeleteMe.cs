using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.ParallelSearchTask;
[TestFixture]
public static class DeleteMe
{
    private record struct DirInfo(string Name, int FileCount, int TsvCount, int TxtCount);
    [Test]
    public static void CountEmptyDirs()
    {
        string path = @"";

        int dirCount = 0;
        List<DirInfo> dirInfo = new();

        foreach (var innerDir in Directory.GetDirectories(path))
        {
            string name = Path.GetFileName(innerDir);

            var files = Directory.GetFiles(innerDir);
            var txtCount = files.Count(p => p.EndsWith(".txt"));
            var tsvCount = files.Count(p => p.EndsWith("tsv"));

            dirInfo.Add(new(name, files.Length, tsvCount, txtCount));
        }

        int emptyDirs = dirInfo.Count(p => p.FileCount == 0);

    }

}

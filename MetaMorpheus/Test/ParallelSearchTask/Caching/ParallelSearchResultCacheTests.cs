using System.IO;
using NUnit.Framework;
using TaskLayer.ParallelSearch;
using TaskLayer.ParallelSearch.Analysis;

namespace Test.ParallelSearchTask.Caching;

[TestFixture]
public class ParallelSearchResultCacheTests
{
    [Test]
    public void AddAndRemove_TracksEntriesAndRejectsDuplicates()
    {
        string path = GetTempCsvPath();
        var cache = new ParallelSearchResultCache(path);
        var metrics = new TransientDatabaseMetrics("Db1");

        bool firstAdd = cache.Add(metrics);
        bool duplicateAdd = cache.Add(metrics);
        bool removed = cache.Remove(metrics);

        Assert.Multiple(() =>
        {
            Assert.That(firstAdd, Is.True);
            Assert.That(duplicateAdd, Is.False);
            Assert.That(removed, Is.True);
            Assert.That(cache.Count, Is.EqualTo(0));
        });
    }

    [Test]
    public void AddAndWrite_ThenInitializeCache_LoadsPersistedResult()
    {
        string path = GetTempCsvPath();
        try
        {
            var firstCache = new ParallelSearchResultCache(path);
            var metrics = new TransientDatabaseMetrics("DbPersisted");

            Assert.That(firstCache.AddAndWrite(metrics), Is.True);

            var secondCache = new ParallelSearchResultCache(path);
            secondCache.InitializeCache();

            bool found = secondCache.TryGetValue("DbPersisted", out var loaded);

            Assert.Multiple(() =>
            {
                Assert.That(found, Is.True);
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded!.DatabaseName, Is.EqualTo("DbPersisted"));
                Assert.That(secondCache.Count, Is.EqualTo(1));
            });
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    public void InitializeCache_WhenFileCannotBeRead_ClearsInMemoryCache()
    {
        string path = GetTempCsvPath();
        File.WriteAllText(path, "dummy");

        var cache = new ParallelSearchResultCache(path);
        cache.Add(new TransientDatabaseMetrics("DbInMemory"));
        Assert.That(cache.Count, Is.EqualTo(1));

        using var lockedStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        cache.InitializeCache();

        Assert.That(cache.Count, Is.EqualTo(0));

        lockedStream.Dispose();
        File.Delete(path);
    }

    private static string GetTempCsvPath()
    {
        return Path.Combine(Path.GetTempPath(), $"parallel_search_cache_{Path.GetRandomFileName()}.csv");
    }
}

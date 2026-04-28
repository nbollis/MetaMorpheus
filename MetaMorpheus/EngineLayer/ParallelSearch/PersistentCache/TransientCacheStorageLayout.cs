using System;
using System.IO;

namespace EngineLayer.ParallelSearch.PersistentCache;

internal sealed class TransientCacheStorageLayout
{
    public string RootDirectory { get; }
    public string ManifestPath { get; }
    public string PayloadDirectory { get; }

    private TransientCacheStorageLayout(string rootDirectory)
    {
        RootDirectory = rootDirectory;
        ManifestPath = Path.Combine(rootDirectory, TransientCacheSchema.ManifestFileName);
        PayloadDirectory = Path.Combine(rootDirectory, TransientCacheSchema.PayloadDirectoryName);
    }

    public static TransientCacheStorageLayout Create(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        return new TransientCacheStorageLayout(rootDirectory);
    }

    public static TransientCacheStorageLayout CreateDefault()
    {
        string rootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MetaMorpheus",
            "TransientCache",
            TransientCacheSchema.GetSchemaTag());

        return new TransientCacheStorageLayout(rootDirectory);
    }

    public string GetSegmentPath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        return Path.Combine(PayloadDirectory, relativePath);
    }

    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(PayloadDirectory);
    }
}

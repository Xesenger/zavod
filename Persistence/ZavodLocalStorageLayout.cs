using System;
using System.IO;

namespace zavod.Persistence;

public static class ZavodLocalStorageLayout
{
    private const string LocalRootName = ".zavod.local";

    public static string GetRoot(string projectRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);
        return Path.Combine(Path.GetFullPath(projectRootPath), LocalRootName);
    }

    public static string GetConversationsRoot(string projectRootPath) => Path.Combine(GetRoot(projectRootPath), "conversations");

    public static string GetRuntimeRoot(string projectRootPath) => Path.Combine(GetRoot(projectRootPath), "runtime");

    public static string GetCacheRoot(string projectRootPath) => Path.Combine(GetRoot(projectRootPath), "cache");

    public static string GetPreviewsRoot(string projectRootPath) => Path.Combine(GetRoot(projectRootPath), "previews");

    public static string GetResumeRoot(string projectRootPath) => Path.Combine(GetRoot(projectRootPath), "resume");

    public static string GetAttachmentsRoot(string projectRootPath) => Path.Combine(GetRoot(projectRootPath), "attachments");

    public static string GetMetaRoot(string projectRootPath) => Path.Combine(GetRoot(projectRootPath), "meta");

    public static void EnsureInitialized(string projectRootPath)
    {
        Directory.CreateDirectory(GetRoot(projectRootPath));
        Directory.CreateDirectory(GetConversationsRoot(projectRootPath));
        Directory.CreateDirectory(GetRuntimeRoot(projectRootPath));
        Directory.CreateDirectory(GetCacheRoot(projectRootPath));
        Directory.CreateDirectory(GetPreviewsRoot(projectRootPath));
        Directory.CreateDirectory(GetResumeRoot(projectRootPath));
        Directory.CreateDirectory(GetAttachmentsRoot(projectRootPath));
        Directory.CreateDirectory(GetMetaRoot(projectRootPath));
    }
}

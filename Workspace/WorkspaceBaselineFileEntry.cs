namespace zavod.Workspace;

public sealed record WorkspaceBaselineFileEntry(
    string RelativePath,
    long Length,
    long LastWriteTimeUtcTicks);

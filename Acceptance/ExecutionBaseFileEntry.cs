namespace zavod.Acceptance;

public sealed record ExecutionBaseFileEntry(
    string RelativePath,
    long Length,
    long LastWriteTimeUtcTicks);

using System.Collections.Generic;

namespace zavod.Workspace;

public sealed record WorkspaceChangeSummary(
    int RelevantFileCount,
    int SourceFileCount,
    int BuildFileCount,
    int ConfigFileCount,
    int DocumentFileCount,
    int AssetFileCount,
    int BinaryFileCount,
    int IgnoredNoiseFileCount,
    IReadOnlyList<string> IgnoredNoiseRoots,
    IReadOnlyList<string> SourceRoots,
    IReadOnlyList<string> BuildRoots,
    IReadOnlyList<string> EntryCandidates);

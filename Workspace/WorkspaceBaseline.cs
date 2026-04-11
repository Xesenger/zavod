using System;
using System.Collections.Generic;

namespace zavod.Workspace;

public sealed record WorkspaceBaseline(
    string BaselineId,
    DateTimeOffset CreatedAt,
    WorkspaceBaselineScope Scope,
    IReadOnlyList<WorkspaceBaselineFileEntry> RelevantFiles,
    bool IsPartial,
    string SummaryLine);

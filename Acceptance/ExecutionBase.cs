using System;
using System.Collections.Generic;

namespace zavod.Acceptance;

public sealed record ExecutionBase(
    string ExecutionId,
    DateTimeOffset CreatedAt,
    TouchedScope Scope,
    IReadOnlyList<ExecutionBaseFileEntry> Files,
    string SummaryLine);

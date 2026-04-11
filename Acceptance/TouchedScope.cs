using System.Collections.Generic;

namespace zavod.Acceptance;

public sealed record TouchedScope(
    IReadOnlyList<string> RelativePaths);

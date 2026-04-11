using System;
using System.Collections.Generic;

namespace zavod.Execution;

public sealed record ExternalProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    TimeSpan Timeout,
    string Purpose);

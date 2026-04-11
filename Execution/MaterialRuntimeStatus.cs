namespace zavod.Execution;

public enum MaterialRuntimeStatus
{
    Prepared,
    MissingFile,
    UnsupportedKind,
    BackendUnavailable,
    OcrRequired,
    Encrypted,
    Corrupt,
    Unreadable,
    Failed
}

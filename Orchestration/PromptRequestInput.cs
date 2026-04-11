using zavod.Contexting;
using zavod.Prompting;
using zavod.State;

namespace zavod.Orchestration;

public sealed record PromptRequestInput(
    PromptRole Role,
    Capsule Capsule,
    ShiftState ShiftState,
    TaskState TaskState,
    WorkerResultContext? WorkerResult = null,
    EscalationContext? Escalation = null);

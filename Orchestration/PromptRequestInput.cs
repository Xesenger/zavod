using System.Collections.Generic;
using zavod.Contexting;
using zavod.Prompting;
using zavod.State;

namespace zavod.Orchestration;

// PromptRequestInput is today's Work Packet carrier (see
// project_work_packet_v1.md). B2 adds optional fields that conform
// the input to the canonical Work Packet shape without forcing
// callers to populate them. Defaults preserve prior behavior.
//
// When IsFirstCycle is true for ShiftLead, TaskState may carry a synthetic
// first-cycle placeholder per first-cycle variant rules. That placeholder
// is runtime input for opening the first Work Packet; it is not required to
// already exist as shift task truth.
public sealed record PromptRequestInput(
    PromptRole Role,
    Capsule Capsule,
    ShiftState ShiftState,
    TaskState TaskState,
    WorkerResultContext? WorkerResult = null,
    EscalationContext? Escalation = null,
    CanonicalDocsStatus? CanonicalDocsStatus = null,
    PreviewStatus? PreviewStatus = null,
    IReadOnlyList<string>? MissingTruthWarnings = null,
    bool IsFirstCycle = false);

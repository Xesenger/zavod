using System.Collections.Generic;
using zavod.Contexting;

namespace zavod.Prompting;

public sealed record PromptRoleCore(
    string Role,
    string Stack,
    string Style,
    IReadOnlyList<string> Rules,
    IReadOnlyList<string> ResponseContract,
    IReadOnlyList<string> Constraints);

public sealed record ShiftContextBlock(
    string CurrentShift,
    string Goal,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<string> State);

public sealed record ScopeBlock(
    IReadOnlyList<string> Included,
    IReadOnlyList<string> Excluded);

public sealed record CandidateIntentBlock(
    string Summary,
    ScopeBlock? Scope,
    IReadOnlyList<string> Constraints);

public sealed record ValidatedIntentBlock(
    string Summary,
    ScopeBlock Scope,
    IReadOnlyList<string> AcceptanceCriteria,
    IReadOnlyList<string> Constraints);

public sealed record TaskBlock(
    ContextIntentState IntentState,
    string Description,
    IReadOnlyList<string> Scope,
    IReadOnlyList<string> Acceptance);

public sealed record WorkerResultContext(
    IReadOnlyList<string> Plan,
    IReadOnlyList<string> ExecutionSummary,
    IReadOnlyList<string> Outputs,
    IReadOnlyList<string> SelfCheck);

public sealed record EscalationContext(
    string Reason,
    IReadOnlyList<string> ConflictingPoints,
    IReadOnlyList<string> UnresolvedItems);

public sealed record PromptAnchor(
    string Id,
    PromptAnchorType Type,
    string Source,
    string Value,
    double? Confidence = null,
    string? Scope = null,
    string? Reference = null);

public sealed record PromptAssemblyRequest(
    PromptRole Role,
    PromptTruthMode TruthMode,
    ShiftContextBlock ShiftContext,
    TaskBlock TaskBlock,
    IReadOnlyList<PromptAnchor> Anchors,
    CandidateIntentBlock? CandidateIntent = null,
    ValidatedIntentBlock? ValidatedIntent = null,
    WorkerResultContext? WorkerResult = null,
    EscalationContext? Escalation = null);

public sealed record PromptRequestPacket(
    PromptRole Role,
    PromptTruthMode TruthMode,
    PromptAssemblyRequest Request,
    PromptPacketMetadata Metadata);

public sealed record PromptPacketMetadata(
    string ShiftId,
    string TaskId,
    int AnchorCount);

public sealed record SerializedPromptAnchor(
    string Id,
    string Type,
    string Source,
    string Value,
    double? Confidence = null,
    string? Scope = null,
    string? Reference = null);

public sealed record PromptTransportPacket(
    PromptRole Role,
    PromptTruthMode TruthMode,
    string RoleCoreText,
    string ShiftContextText,
    string TaskBlockText,
    IReadOnlyList<SerializedPromptAnchor> SerializedAnchors,
    string AnchorPackText,
    PromptPacketMetadata Metadata);

using System;
using System.Collections.Generic;
using System.Linq;
using zavod.Contexting;
using zavod.Prompting;
using zavod.State;

namespace zavod.Orchestration;

public static class PromptRequestPipeline
{
    public static PromptAssemblyResult Execute(PromptRequestInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        ValidateInput(input);

        var shiftContext = IsShiftLeadFirstCycle(input)
            ? BuildFirstCycleShiftContext(input)
            : ShiftContextBuilder.Build(input.ShiftState, input.TaskState);
        var projectedContext = ShiftContextBuilder.ProjectForRole(shiftContext, input.Capsule, input.Role);
        var promptShiftContext = AddWorkPacketState(
            input,
            PromptContextAdapter.ToPromptShiftContext(projectedContext));
        var taskBlock = BuildTaskBlock(input);
        var anchors = PromptAnchorProvider.Build(
            input.Role,
            input.Capsule,
            projectedContext,
            BuildIntentFact(input.TaskState),
            input.Escalation);
        var truthMode = DetermineTruthMode(input);
        Require(truthMode != PromptTruthMode.Unknown,
            input.Role,
            "truth mode",
            "Prompt truth mode could not be determined from the structured request.");

        var request = new PromptAssemblyRequest(
            input.Role,
            truthMode,
            promptShiftContext,
            taskBlock,
            anchors,
            BuildCandidateIntent(input.TaskState),
            BuildValidatedIntent(input.TaskState),
            input.WorkerResult,
            input.Escalation);
        var packet = new PromptRequestPacket(
            input.Role,
            truthMode,
            request,
            new PromptPacketMetadata(
                input.ShiftState.ShiftId,
                input.TaskState.TaskId,
                anchors.Count));

        var finalPrompt = PromptAssembler.Build(packet);

        return new PromptAssemblyResult(
            finalPrompt,
            input.Role,
            truthMode,
            new PromptAssemblyMetadata(
                input.ShiftState.ShiftId,
                input.TaskState.TaskId,
                anchors.Count,
                truthMode,
                input.CanonicalDocsStatus,
                input.PreviewStatus,
                input.IsFirstCycle));
    }

    private static void ValidateInput(PromptRequestInput input)
    {
        if (input.IsFirstCycle)
        {
            Require(input.Role == PromptRole.ShiftLead,
                input.Role,
                "first-cycle role",
                "Only Shift Lead may open a first-cycle Work Packet.");
            Require(input.ShiftState.Status == ShiftStateStatus.Active,
                input.Role,
                "active shift",
                "First-cycle Work Packet requires an active shift.");
            Require(input.TaskState.Scope.Count > 0,
                input.Role,
                "first-cycle scope",
                "First-cycle Work Packet requires bounded project scope.");
            return;
        }

        Require(input.ShiftState.Tasks.Any(task => task.TaskId == input.TaskState.TaskId),
            input.Role,
            "task membership",
            "TaskState must belong to ShiftState.");

        Require(input.TaskState.IntentState == ContextIntentState.Validated,
            input.Role,
            "intent state",
            "Canonical task runtime requires validated intent state.");

        switch (input.Role)
        {
            case PromptRole.Worker:
                Require(input.TaskState.AssignedRole == PromptRole.Worker,
                    input.Role,
                    "task assignment",
                    "Worker requires task assigned to Worker.");
                Require(input.TaskState.AcceptanceCriteria.Count > 0,
                    input.Role,
                    "acceptance criteria",
                    "Worker requires acceptance criteria.");
                break;

            case PromptRole.ShiftLead:
                Require(input.TaskState.AssignedRole == PromptRole.ShiftLead || input.TaskState.CreatedByRole == PromptRole.ShiftLead,
                    input.Role,
                    "lead task ownership",
                    "Shift Lead requires task created by or assigned to Shift Lead.");
                break;

            case PromptRole.Qc:
                Require(input.WorkerResult is not null,
                    input.Role,
                    "worker result context",
                    "QC requires worker result context.");
                break;

            case PromptRole.SeniorSpecialist:
                Require(input.Escalation is not null,
                    input.Role,
                    "escalation context",
                    "Senior Specialist requires escalation context.");
                break;
        }
    }

    private static TaskBlock BuildTaskBlock(PromptRequestInput input)
    {
        var acceptance = input.Role is PromptRole.Worker or PromptRole.Qc
            ? input.TaskState.AcceptanceCriteria
            : Array.Empty<string>();

        return new TaskBlock(
            input.TaskState.IntentState,
            BuildTaskDescription(input),
            input.TaskState.Scope,
            acceptance);
    }

    private static string BuildTaskDescription(PromptRequestInput input)
    {
        if (IsShiftLeadFirstCycle(input))
        {
            return $"Open first work cycle: {input.TaskState.Description}";
        }

        return input.Role switch
        {
            PromptRole.Worker => $"Implement active task: {input.TaskState.Description}",
            PromptRole.Qc => $"Verify execution result for task: {input.TaskState.Description}",
            PromptRole.ShiftLead => $"Coordinate active task: {input.TaskState.Description}",
            PromptRole.SeniorSpecialist => $"Resolve escalation for task: {input.TaskState.Description}",
            _ => input.TaskState.Description
        };
    }

    private static CandidateIntentBlock? BuildCandidateIntent(TaskState taskState)
    {
        if (taskState.IntentState == ContextIntentState.Validated)
        {
            return null;
        }

        return new CandidateIntentBlock(
            taskState.Description,
            new ScopeBlock(taskState.Scope, Array.Empty<string>()),
            taskState.AcceptanceCriteria);
    }

    private static ValidatedIntentBlock? BuildValidatedIntent(TaskState taskState)
    {
        if (taskState.IntentState != ContextIntentState.Validated)
        {
            return null;
        }

        return new ValidatedIntentBlock(
            taskState.Description,
            new ScopeBlock(taskState.Scope, Array.Empty<string>()),
            taskState.AcceptanceCriteria,
            Array.Empty<string>());
    }

    private static string BuildIntentFact(TaskState taskState)
    {
        if (taskState.IntentState != ContextIntentState.Validated)
        {
            return $"Candidate intent: {taskState.Description}";
        }

        return $"Validated intent: {taskState.Description}";
    }

    private static PromptTruthMode DetermineTruthMode(PromptRequestInput input)
    {
        if (IsShiftLeadFirstCycle(input))
        {
            return PromptTruthMode.Anchored;
        }

        return input.TaskState.IntentState == ContextIntentState.Validated
            ? PromptTruthMode.Anchored
            : PromptTruthMode.Unknown;
    }

    private static bool IsShiftLeadFirstCycle(PromptRequestInput input)
    {
        return input.IsFirstCycle && input.Role == PromptRole.ShiftLead;
    }

    private static ShiftContext BuildFirstCycleShiftContext(PromptRequestInput input)
    {
        return ShiftContextBuilder.Build(new ShiftContextSourceInput(
            input.ShiftState.ShiftId,
            input.ShiftState.Goal,
            "First work cycle",
            $"{input.ShiftState.Status}/FirstCycle",
            input.TaskState.Scope,
            input.ShiftState.AcceptedResults,
            input.ShiftState.Constraints,
            input.ShiftState.OpenIssues,
            input.TaskState.IntentState,
            new[]
            {
                "shift_state",
                "work_packet",
                "project_truth_status"
            },
            new[]
            {
                $"TaskId: {input.TaskState.TaskId}",
                "IsFirstCycle: true"
            },
            "Assess project memory honestly; if thin or preview-only, propose review, clarification, or a bounded first task."));
    }

    private static ShiftContextBlock AddWorkPacketState(PromptRequestInput input, ShiftContextBlock context)
    {
        if (!input.IsFirstCycle
            && input.CanonicalDocsStatus is null
            && input.PreviewStatus is null
            && input.MissingTruthWarnings is null)
        {
            return context;
        }

        var state = context.State.ToList();
        state.Add($"WorkPacket: first_cycle={input.IsFirstCycle.ToString().ToLowerInvariant()}");

        if (input.IsFirstCycle)
        {
            state.Add("FirstCycleGuidance: determine whether project memory is mature enough for direct execution.");
            state.Add("FirstCycleGuardrail: do not pretend the project is fully understood when truth is preview-only, stale, or absent.");
        }

        if (input.CanonicalDocsStatus is not null)
        {
            var status = input.CanonicalDocsStatus;
            state.Add($"WorkPacket: canonical_docs_status project={status.Project}; direction={status.Direction}; roadmap={status.Roadmap}; canon={status.Canon}; capsule={status.Capsule}");
            state.Add($"WorkPacket: canonical_docs_count={status.CanonicalCount}/5");
            state.Add($"WorkPacket: at_least_preview_count={status.AtLeastPreviewCount}/5");
        }

        if (input.PreviewStatus is { PreviewKinds.Count: > 0 } preview)
        {
            state.Add($"WorkPacket: preview_docs={string.Join(", ", preview.PreviewKinds)}");
        }

        if (input.MissingTruthWarnings is { Count: > 0 })
        {
            foreach (var warning in input.MissingTruthWarnings.Where(static warning => !string.IsNullOrWhiteSpace(warning)))
            {
                state.Add($"WorkPacketWarning: {warning.Trim()}");
            }
        }

        return context with { State = state };
    }

    private static void Require(bool condition, PromptRole role, string missingRequirement, string reason)
    {
        if (!condition)
        {
            throw new PromptRequestPipelineException(role, missingRequirement, reason);
        }
    }
}

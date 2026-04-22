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

        var shiftContext = ShiftContextBuilder.Build(input.ShiftState, input.TaskState);
        var projectedContext = ShiftContextBuilder.ProjectForRole(shiftContext, input.Capsule, input.Role);
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
            PromptContextAdapter.ToPromptShiftContext(projectedContext),
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
        return null;
    }

    private static ValidatedIntentBlock? BuildValidatedIntent(TaskState taskState)
    {
        return new ValidatedIntentBlock(
            taskState.Description,
            new ScopeBlock(taskState.Scope, Array.Empty<string>()),
            taskState.AcceptanceCriteria,
            Array.Empty<string>());
    }

    private static string BuildIntentFact(TaskState taskState)
    {
        return $"Validated intent: {taskState.Description}";
    }

    private static PromptTruthMode DetermineTruthMode(PromptRequestInput input)
    {
        return input.TaskState.IntentState == ContextIntentState.Validated
            ? PromptTruthMode.Anchored
            : PromptTruthMode.Unknown;
    }

    private static void Require(bool condition, PromptRole role, string missingRequirement, string reason)
    {
        if (!condition)
        {
            throw new PromptRequestPipelineException(role, missingRequirement, reason);
        }
    }
}

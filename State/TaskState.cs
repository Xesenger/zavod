using System;
using System.Collections.Generic;
using zavod.Contexting;
using zavod.Prompting;

namespace zavod.State;

public sealed record TaskState(
    string TaskId,
    ContextIntentState IntentState,
    TaskStateStatus Status,
    string Description,
    IReadOnlyList<string> Scope,
    IReadOnlyList<string> AcceptanceCriteria,
    PromptRole CreatedByRole,
    PromptRole AssignedRole,
    DateTimeOffset LastUpdated)
{
    public TaskState Assign(PromptRole actor, PromptRole assignee, DateTimeOffset updatedAt)
    {
        Require(actor == PromptRole.ShiftLead || actor == PromptRole.SeniorSpecialist, "Assign", "Only Shift Lead or Senior Specialist can assign tasks.");
        return this with { AssignedRole = assignee, LastUpdated = updatedAt };
    }

    public TaskState Complete(PromptRole actor, DateTimeOffset updatedAt)
    {
        Require(actor == PromptRole.ShiftLead || actor == PromptRole.SeniorSpecialist, "Complete", "Only Shift Lead or Senior Specialist can complete task truth.");
        Require(IntentState == ContextIntentState.Validated, "Complete", "Only validated intent may reach completed task truth.");
        Require(Status == TaskStateStatus.Active, "Complete", "Only active task can be completed.");
        return this with { Status = TaskStateStatus.Completed, LastUpdated = updatedAt };
    }

    public TaskState Abandon(PromptRole actor, DateTimeOffset updatedAt)
    {
        Require(actor == PromptRole.ShiftLead || actor == PromptRole.SeniorSpecialist, "Abandon", "Only Shift Lead or Senior Specialist can abandon task truth.");
        Require(IntentState == ContextIntentState.Validated, "Abandon", "Only validated intent may reach abandoned task truth.");
        Require(Status == TaskStateStatus.Active, "Abandon", "Only active task can be abandoned.");
        return this with { Status = TaskStateStatus.Abandoned, LastUpdated = updatedAt };
    }

    private void Require(bool condition, string action, string reason)
    {
        if (!condition)
        {
            throw new StateTransitionException(TaskId, action, reason);
        }
    }
}

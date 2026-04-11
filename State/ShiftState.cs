using System;
using System.Collections.Generic;
using System.Linq;
using zavod.Prompting;

namespace zavod.State;

public sealed record ShiftState(
    string ShiftId,
    string Goal,
    string? CurrentTaskId,
    ShiftStateStatus Status,
    IReadOnlyList<TaskState> Tasks,
    IReadOnlyList<string> OpenIssues,
    IReadOnlyList<string> AcceptedResults,
    IReadOnlyList<string> Constraints)
{
    public ShiftState AssignTask(PromptRole actor, string taskId)
    {
        Require(actor == PromptRole.ShiftLead || actor == PromptRole.SeniorSpecialist, "AssignTask", "Only Shift Lead or Senior Specialist can assign current task.");
        Require(Status == ShiftStateStatus.Active, "AssignTask", "Only active shift can assign current task.");
        Require(Tasks.Any(task => task.TaskId == taskId), "AssignTask", "Shift cannot assign unknown task.");
        return this with { CurrentTaskId = taskId };
    }

    public ShiftState UpdateTask(TaskState taskState)
    {
        Require(Status == ShiftStateStatus.Active, "UpdateTask", "Only active shift can update task truth.");
        Require(Tasks.Any(task => task.TaskId == taskState.TaskId), "UpdateTask", "Shift cannot update unknown task.");
        var tasks = Tasks.Select(task => task.TaskId == taskState.TaskId ? taskState : task).ToArray();
        return this with { Tasks = tasks };
    }

    public ShiftState Complete(PromptRole actor)
    {
        Require(actor == PromptRole.ShiftLead || actor == PromptRole.SeniorSpecialist, "Complete", "Only Shift Lead or Senior Specialist can complete shift.");
        Require(Status == ShiftStateStatus.Active, "Complete", "Only active shift can be completed.");
        Require(CurrentTaskId is null, "Complete", "Shift closure requires empty current task slot before completion.");
        return this with { Status = ShiftStateStatus.Completed };
    }

    public ShiftState RecordAcceptedResult(PromptRole actor, string acceptedResultReference)
    {
        Require(actor == PromptRole.ShiftLead || actor == PromptRole.SeniorSpecialist, "RecordAcceptedResult", "Only Shift Lead or Senior Specialist can record committed result in shift state.");
        Require(Status == ShiftStateStatus.Active, "RecordAcceptedResult", "Only active shift can record committed result.");
        Require(!string.IsNullOrWhiteSpace(acceptedResultReference), "RecordAcceptedResult", "Accepted result reference is required.");

        var acceptedResults = AcceptedResults
            .Concat(new[] { acceptedResultReference.Trim() })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

        return this with { AcceptedResults = acceptedResults };
    }

    private void Require(bool condition, string action, string reason)
    {
        if (!condition)
        {
            throw new StateTransitionException(ShiftId, action, reason);
        }
    }
}

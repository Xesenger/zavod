using System.Collections.Generic;
using zavod.Prompting;

namespace zavod.Contexting;

public static class PromptContextAdapter
{
    public static ShiftContextBlock ToPromptShiftContext(ProjectedShiftContext context)
    {
        var state = new List<string>
        {
            $"ShiftId: {context.ShiftId}",
            $"CurrentStep: {context.CurrentStep}",
            $"CurrentStatus: {context.CurrentStatus}",
            $"IntentState: {context.CurrentIntentState}"
        };

        if (context.Scope.Count > 0)
        {
            state.Add($"Scope: {string.Join("; ", context.Scope)}");
        }

        foreach (var item in context.AcceptedResultsSummary)
        {
            state.Add($"AcceptedResult: {item}");
        }

        foreach (var item in context.OpenIssues)
        {
            state.Add($"OpenIssue: {item}");
        }

        foreach (var item in context.PreviousStepSummary)
        {
            state.Add($"PreviousStep: {item}");
        }

        if (!string.IsNullOrWhiteSpace(context.NextExpectedAction))
        {
            state.Add($"NextExpectedAction: {context.NextExpectedAction}");
        }

        foreach (var item in context.ContextSourceSummary)
        {
            state.Add($"ContextSource: {item}");
        }

        return new ShiftContextBlock(
            context.ShiftId,
            context.ShiftGoal,
            context.RelevantConstraints,
            state);
    }
}

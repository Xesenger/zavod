using System;
using System.Collections.Generic;
using System.Linq;
using zavod.Contexting;

namespace zavod.Prompting;

public static class PromptAnchorProvider
{
    public static IReadOnlyList<PromptAnchor> Build(
        PromptRole role,
        Capsule capsule,
        ProjectedShiftContext context,
        string intentFact,
        EscalationContext? escalation)
    {
        ArgumentNullException.ThrowIfNull(capsule);
        ArgumentNullException.ThrowIfNull(context);

        var anchors = new List<PromptAnchor>();

        anchors.AddRange(GetBaseAnchors(intentFact, context.CurrentIntentState, context.CurrentStep));
        anchors.AddRange(context.Scope.Select((item, index) =>
            new PromptAnchor($"A-CODE-{index + 1:000}", PromptAnchorType.Code, "scope", item)));
        anchors.AddRange(capsule.CoreCanonRules.Select((item, index) =>
            new PromptAnchor($"A-TRUTH-CANON-{index + 1:000}", PromptAnchorType.Truth, "capsule.canon", item)));
        anchors.AddRange(context.RelevantConstraints.Select((item, index) =>
            new PromptAnchor($"A-CONSTRAINT-CTX-{index + 1:000}", PromptAnchorType.Constraint, "shift_context", item)));
        anchors.AddRange(GetRoleAnchors(role));

        if (escalation is not null)
        {
            anchors.Add(new PromptAnchor("A-DECISION-001", PromptAnchorType.Decision, "escalation", escalation.Reason));
            anchors.AddRange(escalation.ConflictingPoints.Select((item, index) =>
                new PromptAnchor($"A-DECISION-{index + 2:000}", PromptAnchorType.Decision, "escalation.conflict", item)));
        }

        return PromptAnchorCanonicalizer.Order(anchors);
    }

    private static IEnumerable<PromptAnchor> GetBaseAnchors(string intentFact, ContextIntentState intentState, string currentStep)
    {
        return
        [
            new PromptAnchor("A-TASK-001", PromptAnchorType.Task, "intent", intentFact),
            new PromptAnchor("A-TRUTH-001", PromptAnchorType.Truth, "system", "Code is truth."),
            new PromptAnchor("A-TRUTH-002", PromptAnchorType.Truth, "system", "Do not hallucinate APIs."),
            new PromptAnchor("A-CONSTRAINT-001", PromptAnchorType.Constraint, "system", "Minimal sufficient solution."),
            new PromptAnchor("A-CONSTRAINT-002", PromptAnchorType.Constraint, "system", "Verify if unsure."),
            new PromptAnchor("A-STATE-001", PromptAnchorType.State, "shift_state", $"Intent state: {intentState}.", Reference: "task.intent_state"),
            new PromptAnchor("A-STATE-002", PromptAnchorType.State, "shift_state", $"Current step: {currentStep}.", Reference: "shift.current_step")
        ];
    }

    private static IEnumerable<PromptAnchor> GetRoleAnchors(PromptRole role)
    {
        return role switch
        {
            PromptRole.Worker =>
            [
                new PromptAnchor("A-CONSTRAINT-ROLE-001", PromptAnchorType.Constraint, "role.worker", "Try to solve independently first."),
                new PromptAnchor("A-CONSTRAINT-ROLE-002", PromptAnchorType.Constraint, "role.worker", "Do not escalate too early.")
            ],
            PromptRole.ShiftLead =>
            [
                new PromptAnchor("A-CONSTRAINT-ROLE-001", PromptAnchorType.Constraint, "role.shift_lead", "Break task into clear steps."),
                new PromptAnchor("A-CONSTRAINT-ROLE-002", PromptAnchorType.Constraint, "role.shift_lead", "Avoid ambiguity.")
            ],
            PromptRole.Qc =>
            [
                new PromptAnchor("A-CONSTRAINT-ROLE-001", PromptAnchorType.Constraint, "role.qc", "Verify in layers."),
                new PromptAnchor("A-CONSTRAINT-ROLE-002", PromptAnchorType.Constraint, "role.qc", "Reject unverifiable output.")
            ],
            PromptRole.SeniorSpecialist =>
            [
                new PromptAnchor("A-CONSTRAINT-ROLE-001", PromptAnchorType.Constraint, "role.senior", "Challenge assumptions."),
                new PromptAnchor("A-CONSTRAINT-ROLE-002", PromptAnchorType.Constraint, "role.senior", "Do not overcomplicate without reason.")
            ],
            _ => Array.Empty<PromptAnchor>()
        };
    }

}

using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace zavod.Prompting;

internal static class PromptAssembler
{
    public static string Build(PromptRequestPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        Validate(packet.Request);
        Require(packet.TruthMode != PromptTruthMode.Unknown, packet.Role, "truth mode", "Prompt truth mode must be determined before assembly.");

        var transport = PromptTransportSerializer.Serialize(packet);
        return BuildFromTransport(transport);
    }

    private static string BuildFromTransport(PromptTransportPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        Require(packet.TruthMode != PromptTruthMode.Unknown, packet.Role, "truth mode", "Transport packet must have known truth mode.");
        Require(!string.IsNullOrWhiteSpace(packet.RoleCoreText), packet.Role, "role core", "Role core text is required.");
        Require(!string.IsNullOrWhiteSpace(packet.ShiftContextText), packet.Role, "shift context", "Shift context text is required.");
        Require(!string.IsNullOrWhiteSpace(packet.TaskBlockText), packet.Role, "task block", "Task block text is required.");
        Require(packet.SerializedAnchors is { Count: > 0 }, packet.Role, "serialized anchors", "Serialized anchor pack is required.");
        Require(!string.IsNullOrWhiteSpace(packet.AnchorPackText), packet.Role, "anchor pack", "Anchor pack text is required.");

        var builder = new StringBuilder();
        builder.AppendLine("[ROLE CORE]");
        builder.AppendLine(packet.RoleCoreText);
        builder.AppendLine();
        builder.AppendLine("[SHIFT CONTEXT]");
        builder.AppendLine(packet.ShiftContextText);
        builder.AppendLine();
        builder.AppendLine("[TASK BLOCK]");
        builder.AppendLine(packet.TaskBlockText);
        builder.AppendLine();
        builder.AppendLine("[ANCHORS]");
        builder.Append(packet.AnchorPackText);
        return builder.ToString().TrimEnd();
    }

    internal static void Validate(PromptAssemblyRequest request)
    {
        Require(!string.IsNullOrWhiteSpace(request.ShiftContext.CurrentShift), request.Role, "shift context", "Current shift is required.");
        Require(!string.IsNullOrWhiteSpace(request.ShiftContext.Goal), request.Role, "shift context", "Shift goal is required.");
        Require(!string.IsNullOrWhiteSpace(request.TaskBlock.Description), request.Role, "task block", "Task description is required.");
        Require(request.TaskBlock.Scope is { Count: > 0 }, request.Role, "task block", "Task scope is required.");
        Require(request.Anchors is { Count: > 0 }, request.Role, "anchor basis", "At least one anchor is required.");

        foreach (var anchor in request.Anchors)
        {
            Require(!string.IsNullOrWhiteSpace(anchor.Id), request.Role, "anchor id", "Each anchor must include an id.");
            Require(!string.IsNullOrWhiteSpace(anchor.Source), request.Role, $"anchor '{anchor.Id}' source", "Anchor source is required.");
            Require(!string.IsNullOrWhiteSpace(anchor.Value), request.Role, $"anchor '{anchor.Id}' value", "Anchor value is required.");
            if (anchor.Confidence is not null)
            {
                Require(anchor.Confidence is >= 0 and <= 1, request.Role, $"anchor '{anchor.Id}' confidence", "Anchor confidence must be between 0 and 1.");
            }
        }

        switch (request.Role)
        {
            case PromptRole.Worker:
                ValidateWorkerRequest(request);
                break;
            case PromptRole.ShiftLead:
                ValidateShiftLeadRequest(request);
                break;
            case PromptRole.Qc:
                ValidateQcRequest(request);
                break;
            case PromptRole.SeniorSpecialist:
                ValidateSeniorRequest(request);
                break;
        }
    }

    internal static string RenderRoleCore(PromptAssemblyRequest request)
    {
        return RenderRoleCore(PromptRoleCoreCatalog.Get(request.Role));
    }

    internal static string RenderShiftContext(PromptAssemblyRequest request)
    {
        return RenderShiftContext(request.ShiftContext);
    }

    internal static string RenderTaskBlock(PromptAssemblyRequest request)
    {
        return RenderTaskBlock(request.TaskBlock);
    }

    private static string RenderRoleCore(PromptRoleCore core)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Role: {core.Role}");
        builder.AppendLine($"Stack: {core.Stack}");
        builder.AppendLine($"Style: {core.Style}");
        builder.AppendLine("Rules:");

        foreach (var rule in core.Rules)
        {
            builder.AppendLine($"- {rule}");
        }

        builder.AppendLine("Response Contract:");

        foreach (var item in core.ResponseContract)
        {
            builder.AppendLine($"- {item}");
        }

        builder.AppendLine("Role Constraints:");

        foreach (var item in core.Constraints)
        {
            builder.AppendLine($"- {item}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string RenderShiftContext(ShiftContextBlock context)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Current shift: {context.CurrentShift}");
        builder.AppendLine($"Goal: {context.Goal}");
        builder.AppendLine("Constraints:");

        foreach (var constraint in context.Constraints.Where(static value => !string.IsNullOrWhiteSpace(value)))
        {
            builder.AppendLine($"- {constraint}");
        }

        builder.AppendLine("State:");

        foreach (var stateLine in context.State.Where(static value => !string.IsNullOrWhiteSpace(value)))
        {
            builder.AppendLine($"- {stateLine}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string RenderTaskBlock(TaskBlock taskBlock)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"IntentState: {taskBlock.IntentState}");
        builder.AppendLine($"Description: {taskBlock.Description.Trim()}");
        builder.AppendLine("Scope:");

        foreach (var item in taskBlock.Scope.Where(static value => !string.IsNullOrWhiteSpace(value)))
        {
            builder.AppendLine($"- {item}");
        }

        if (taskBlock.Acceptance.Count > 0)
        {
            builder.AppendLine("Acceptance:");

            foreach (var item in taskBlock.Acceptance.Where(static value => !string.IsNullOrWhiteSpace(value)))
            {
                builder.AppendLine($"- {item}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static void ValidateWorkerRequest(PromptAssemblyRequest request)
    {
        Require(request.ValidatedIntent is not null, request.Role, "validated intent", "Worker assembly requires validated intent.");
        Require(IsDefinedScope(request.ValidatedIntent!.Scope), request.Role, "clear scope", "Worker requires included or excluded scope.");
        Require(request.ValidatedIntent.AcceptanceCriteria.Count > 0, request.Role, "acceptance criteria", "Worker requires acceptance criteria.");
        Require(HasAnchorType(request.Anchors, PromptAnchorType.Task), request.Role, "task anchor", "Worker requires task anchor.");
        Require(HasAnchorType(request.Anchors, PromptAnchorType.Truth), request.Role, "truth anchor", "Worker requires truth anchor.");
    }

    private static void ValidateShiftLeadRequest(PromptAssemblyRequest request)
    {
        Require(request.CandidateIntent is not null || request.ValidatedIntent is not null, request.Role, "intent context", "Shift Lead requires candidate or validated intent.");
        Require(request.CandidateIntent is null || !string.IsNullOrWhiteSpace(request.CandidateIntent.Summary), request.Role, "candidate intent", "Candidate intent summary is required.");
        Require(request.ValidatedIntent is null || !string.IsNullOrWhiteSpace(request.ValidatedIntent.Summary), request.Role, "validated intent", "Validated intent summary is required.");
    }

    private static void ValidateQcRequest(PromptAssemblyRequest request)
    {
        Require(request.ValidatedIntent is not null, request.Role, "validated intent", "QC assembly requires validated intent.");
        Require(IsDefinedScope(request.ValidatedIntent!.Scope), request.Role, "scope", "QC requires defined scope.");
        Require(request.ValidatedIntent.AcceptanceCriteria.Count > 0, request.Role, "acceptance criteria", "QC requires acceptance criteria.");
        Require(request.WorkerResult is not null, request.Role, "worker result context", "QC requires worker result context.");
        Require(request.WorkerResult!.Outputs.Count > 0, request.Role, "worker outputs", "QC requires outputs, diff, or artifacts.");
        Require(HasAnchorType(request.Anchors, PromptAnchorType.Task), request.Role, "task anchor", "QC requires task anchor.");
        Require(HasAnchorType(request.Anchors, PromptAnchorType.Truth), request.Role, "truth anchor", "QC requires truth anchor.");
    }

    private static void ValidateSeniorRequest(PromptAssemblyRequest request)
    {
        Require(request.Escalation is not null, request.Role, "escalation context", "Senior Specialist requires escalation context.");
        Require(!string.IsNullOrWhiteSpace(request.Escalation!.Reason), request.Role, "escalation reason", "Escalation reason is required.");
        Require(request.CandidateIntent is not null || request.ValidatedIntent is not null, request.Role, "intent context", "Senior Specialist requires candidate or validated intent.");
        Require(HasAnchorType(request.Anchors, PromptAnchorType.Truth), request.Role, "truth anchor", "Senior Specialist requires truth anchor.");
        Require(HasAnchorType(request.Anchors, PromptAnchorType.Decision), request.Role, "decision anchor", "Senior Specialist requires decision anchor.");
    }

    private static bool HasAnchorType(IReadOnlyList<PromptAnchor> anchors, PromptAnchorType type)
    {
        return anchors.Any(anchor => anchor.Type == type);
    }

    private static bool IsDefinedScope(ScopeBlock scope)
    {
        return scope.Included.Count > 0 || scope.Excluded.Count > 0;
    }

    private static void Require(bool condition, PromptRole role, string missingRequirement, string reason)
    {
        if (!condition)
        {
            throw new PromptAssemblyException(role, missingRequirement, reason);
        }
    }
}

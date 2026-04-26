using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using zavod.Execution;
using zavod.Orchestration;
using zavod.Prompting;

namespace zavod.Worker;

public sealed record WorkerAgentInput(
    string ProjectName,
    string ProjectRoot,
    string ProjectKind,
    string TaskId,
    string TaskDescription,
    IReadOnlyList<string> Scope,
    IReadOnlyList<string> AcceptanceCriteria,
    IReadOnlyList<string> AdvisoryNotes,
    IReadOnlyList<string>? Anchors = null,
    IReadOnlyList<WorkerEditSlot>? EditSlots = null,
    IReadOnlyList<string>? RevisionNotes = null,
    CanonicalDocsStatus? CanonicalDocsStatus = null,
    PreviewStatus? PreviewStatus = null,
    IReadOnlyList<string>? MissingTruthWarnings = null,
    bool IsFirstCycle = false);

public sealed record WorkerAgentModification(
    string Path,
    string Kind,
    string Summary);

public sealed record WorkerAgentParsedResult(
    string Status,
    string Summary,
    IReadOnlyList<string> Plan,
    IReadOnlyList<string> Actions,
    IReadOnlyList<WorkerAgentModification> Modifications,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<WorkerEdit> Edits);

public sealed record WorkerAgentResult(
    bool Success,
    WorkerAgentParsedResult? Parsed,
    string ModelId,
    long LatencyMs,
    string? DiagnosticCode,
    string? DiagnosticMessage,
    string? RawResponse,
    string? TelemetryDirectory);

public sealed class WorkerAgentRuntime
{
    private const int MaxRepairRawResponseChars = 1200;

    private readonly RoleProfile _profile;
    private readonly LabTelemetryWriter _telemetry;
    private readonly Func<RoleProfile, IOpenRouterExecutionClient> _clientFactory;
    private readonly Lazy<string> _systemPrompt;

    public WorkerAgentRuntime(
        RolesConfiguration? rolesConfiguration = null,
        LabTelemetryWriter? telemetryWriter = null,
        Func<RoleProfile, IOpenRouterExecutionClient>? clientFactory = null)
    {
        _profile = (rolesConfiguration ?? RolesConfiguration.LoadOrDefault()).Worker;
        _telemetry = telemetryWriter ?? new LabTelemetryWriter();
        _clientFactory = clientFactory ?? DefaultClientFactory;
        _systemPrompt = new Lazy<string>(LoadSystemPrompt);
    }

    public WorkerAgentResult Run(WorkerAgentInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var systemPrompt = _systemPrompt.Value;
        var userPrompt = BuildUserPrompt(input);
        var client = _clientFactory(_profile);
        var request = new OpenRouterExecutionRequest(
            RouteId: "worker.agent",
            SystemPrompt: systemPrompt,
            UserPrompt: userPrompt,
            ModelId: _profile.Model,
            Temperature: _profile.Temperature,
            Attachments: null,
            MaxTokens: _profile.MaxTokens,
            ResponseFormatJsonObject: true,
            ReasoningEffort: "none");

        var stopwatch = Stopwatch.StartNew();
        var response = client.Execute(request);
        WorkerAgentParsedResult? parsed = null;
        string? parseError = null;
        if (response.Success)
        {
            try
            {
                parsed = ParseResult(response.Content);
            }
            catch (Exception exception)
            {
                parseError = exception.Message;
            }
        }

        var repairAttempted = false;
        string? firstParseError = null;
        string? firstRawResponse = null;
        if (response.Success && parsed is null)
        {
            repairAttempted = true;
            firstParseError = parseError;
            firstRawResponse = response.Content ?? string.Empty;

            var repairRequest = request with
            {
                RouteId = "worker.agent.repair",
                UserPrompt = BuildRepairPrompt(userPrompt, firstRawResponse, parseError)
            };
            response = client.Execute(repairRequest);
            parsed = null;
            parseError = null;
            if (response.Success)
            {
                try
                {
                    parsed = ParseResult(response.Content);
                }
                catch (Exception exception)
                {
                    parseError = exception.Message;
                }
            }
        }

        stopwatch.Stop();

        var success = response.Success && parsed is not null;
        var diagnosticCode = response.Success
            ? (parsed is null ? "WORKER_PARSE_FAILED" : null)
            : response.Diagnostic?.Code;
        var diagnosticMessage = response.Success
            ? parseError
            : response.Diagnostic?.Message;

        var telemetryDirectory = _telemetry.Write(
            input.ProjectRoot,
            "worker",
            input.TaskId,
            new
            {
                request.RouteId,
                request.ModelId,
                request.Temperature,
                input.ProjectName,
                input.ProjectKind,
                input.TaskId,
                input.TaskDescription,
                scopeCount = input.Scope?.Count ?? 0,
                acceptanceCount = input.AcceptanceCriteria?.Count ?? 0,
                advisoryCount = input.AdvisoryNotes?.Count ?? 0,
                anchorCount = input.Anchors?.Count ?? 0,
                editSlotCount = input.EditSlots?.Count ?? 0,
                repairAttempted,
                firstParseError,
                firstRawResponsePreview = Truncate(firstRawResponse, MaxRepairRawResponseChars),
                systemPromptChars = systemPrompt.Length,
                userPrompt
            },
            response.Content ?? string.Empty,
            parsed,
            new
            {
                success,
                response.ModelId,
                response.StatusCode,
                latencyMs = stopwatch.ElapsedMilliseconds,
                diagnosticCode,
                diagnosticMessage,
                profile = new
                {
                    _profile.Model,
                    _profile.Temperature,
                    _profile.TimeoutSeconds,
                    _profile.MaxTokens
                }
            });

        return new WorkerAgentResult(
            success,
            parsed,
            response.ModelId,
            stopwatch.ElapsedMilliseconds,
            diagnosticCode,
            diagnosticMessage,
            response.Content,
            string.IsNullOrEmpty(telemetryDirectory) ? null : telemetryDirectory);
    }

    private static string LoadSystemPrompt()
    {
        var path = PromptSystemCatalog.ResolvePromptPath("worker.system.md");
        return File.ReadAllText(path).Trim();
    }

    private static string BuildRepairPrompt(string originalPrompt, string rawResponse, string? parseError)
    {
        var builder = new StringBuilder(originalPrompt.Length + MaxRepairRawResponseChars + 512);
        builder.AppendLine(originalPrompt);
        builder.AppendLine();
        builder.AppendLine("PREVIOUS WORKER OUTPUT WAS MALFORMED.");
        builder.AppendLine($"Parse error: {Safe(parseError)}");
        builder.AppendLine("Malformed output preview:");
        builder.AppendLine(Truncate(rawResponse, MaxRepairRawResponseChars));
        builder.AppendLine();
        builder.AppendLine("Repair instruction:");
        builder.AppendLine("- Return one complete valid JSON object using the exact OUTPUT schema above.");
        builder.AppendLine("- Do not explain the repair.");
        builder.AppendLine("- Include non-empty status and summary.");
        builder.AppendLine("- If concrete edits are possible, include complete edits; otherwise use status \"failed\" with blockers.");
        return builder.ToString();
    }

    private static string BuildUserPrompt(WorkerAgentInput input)
    {
        var builder = new StringBuilder();
        builder.AppendLine("PROJECT CONTEXT");
        builder.AppendLine($"- name: {Safe(input.ProjectName)}");
        builder.AppendLine($"- kind: {Safe(input.ProjectKind)}");
        builder.AppendLine($"- root: {Safe(input.ProjectRoot)}");
        builder.AppendLine();
        builder.AppendLine("TASK");
        builder.AppendLine($"- id: {Safe(input.TaskId)}");
        builder.AppendLine($"- description: {Safe(input.TaskDescription)}");
        builder.AppendLine();

        builder.AppendLine("SCOPE (allowed paths / focus)");
        AppendList(builder, input.Scope);
        builder.AppendLine();

        builder.AppendLine("ACCEPTANCE CRITERIA");
        AppendList(builder, input.AcceptanceCriteria);
        builder.AppendLine();

        AppendWorkPacketBlock(builder, input);

        if (input.Anchors is { Count: > 0 })
        {
            builder.AppendLine("CODE ANCHORS (grounded relative file paths and snippets - use these paths as edit targets)");
            foreach (var anchor in input.Anchors)
            {
                if (anchor is null)
                {
                    continue;
                }

                builder.AppendLine(anchor.TrimEnd());
            }

            builder.AppendLine();
        }

        if (input.EditSlots is { Count: > 0 })
        {
            builder.AppendLine("EDIT SLOTS (deterministic insertion points - prefer insert_at_slot over raw anchors when a slot fits)");
            foreach (var slot in input.EditSlots)
            {
                if (slot is null
                    || string.IsNullOrWhiteSpace(slot.Path)
                    || string.IsNullOrWhiteSpace(slot.SlotId))
                {
                    continue;
                }

                builder.AppendLine($"- path: {slot.Path} | slotId: {slot.SlotId} | kind: {slot.Kind} | reason: {slot.Reason}");
            }

            builder.AppendLine();
        }

        if (input.RevisionNotes is { Count: > 0 } revisionNotes)
        {
            builder.AppendLine("REVISION NOTES (feedback from previous attempts within THIS task - use to refine, do not redefine the intent)");
            foreach (var note in revisionNotes)
            {
                if (string.IsNullOrWhiteSpace(note))
                {
                    continue;
                }

                builder.AppendLine($"- {note.Trim()}");
            }

            builder.AppendLine();
        }

        if (input.AdvisoryNotes is { Count: > 0 })
        {
            builder.AppendLine("ADVISORY NOTES (hints from project history, may be stale)");
            foreach (var note in input.AdvisoryNotes)
            {
                if (string.IsNullOrWhiteSpace(note))
                {
                    continue;
                }

                builder.AppendLine($"- {note.Trim()}");
            }

            builder.AppendLine();
        }

        builder.AppendLine("OUTPUT - reply with a single strict JSON object only, no code fences, no prose around it.");
        builder.AppendLine("Return a JSON object with exactly these top-level keys:");
        builder.AppendLine("- status: string; allowed values are success, partial, failed, refused.");
        builder.AppendLine("- summary: non-empty string describing the concrete result.");
        builder.AppendLine("- plan: array of short strings.");
        builder.AppendLine("- actions: array of short strings.");
        builder.AppendLine("- modifications: array of objects with string fields path, kind, summary. kind is edit, create, or delete.");
        builder.AppendLine("- edits: array of objects with string fields path, operation, content, optional anchor, and optional slotId. operation is write_full, insert_at_slot, or insert_after.");
        builder.AppendLine("- blockers: array of strings.");
        builder.AppendLine("- risks: array of strings.");
        builder.AppendLine("- warnings: array of strings.");
        builder.AppendLine("Do not output pipe-separated alternatives. Do not copy placeholder values. Use only task-specific paths, anchors, and edit content.");
        builder.AppendLine("Use paths exactly as listed under CODE ANCHORS; they are relative to the project root and use forward slashes.");
        builder.AppendLine();
        builder.AppendLine("Use status \"refused\" when execution basis is insufficient or the task violates scope/canon.");
        builder.AppendLine("Use status \"failed\" only for concrete blockers you ran into.");
        builder.AppendLine("Do not invent files or APIs. All paths in modifications and edits must be plausible relative to the project root.");
        builder.AppendLine();
        builder.AppendLine("EDITS CONTRACT:");
        builder.AppendLine("- For every path listed in modifications you MUST emit a corresponding edit in the edits array - a plan without edits is not a deliverable.");
        builder.AppendLine("- write_full: set content to the COMPLETE new file body. Use for small files or when rewriting entirely.");
        builder.AppendLine("- insert_at_slot: set slotId to one exact slotId from EDIT SLOTS, and content to the text to insert at that deterministic point. Prefer this over insert_after when a matching slot exists.");
        builder.AppendLine("- insert_after: fallback only; set anchor to a unique substring present in the current file (draw from the snippet you saw in CODE ANCHORS), and content to the text to insert right after that anchor.");
        builder.AppendLine("- Anchors are exact string matches. Copy anchor text from CODE ANCHORS byte-for-byte, including leading spaces, punctuation, and blank lines.");
        builder.AppendLine("- Slot IDs are exact identifiers. Copy slotId from EDIT SLOTS; do not invent slot IDs or copy slot bodies.");
        builder.AppendLine("- If EDIT SLOTS contains a matching path and insertion point for your target file, do not use insert_after for that file.");
        builder.AppendLine("- Each edit's content is written verbatim. No markdown fences, no \"...\" placeholders, no commentary.");
        builder.AppendLine("- If you cannot produce concrete edit content for a path, drop it from modifications and list the reason in blockers - do not pretend a plan equals a deliverable.");
        return builder.ToString();
    }

    private static void AppendWorkPacketBlock(StringBuilder builder, WorkerAgentInput input)
    {
        if (input.CanonicalDocsStatus is null
            && input.PreviewStatus is null
            && input.MissingTruthWarnings is null
            && !input.IsFirstCycle)
        {
            return;
        }

        builder.AppendLine("WORK PACKET (project truth status; preview is below canonical)");
        builder.AppendLine($"- first_cycle: {input.IsFirstCycle.ToString().ToLowerInvariant()}");

        if (input.CanonicalDocsStatus is not null)
        {
            var status = input.CanonicalDocsStatus;
            builder.AppendLine($"- canonical_docs_status: project={status.Project}; direction={status.Direction}; roadmap={status.Roadmap}; canon={status.Canon}; capsule={status.Capsule}");
            builder.AppendLine($"- canonical_docs_count: {status.CanonicalCount}/5");
            builder.AppendLine($"- at_least_preview_count: {status.AtLeastPreviewCount}/5");
        }

        if (input.PreviewStatus is { PreviewKinds.Count: > 0 } preview)
        {
            builder.AppendLine($"- preview_docs: {string.Join(", ", preview.PreviewKinds)}");
        }
        else
        {
            builder.AppendLine("- preview_docs: none");
        }

        if (input.MissingTruthWarnings is { Count: > 0 })
        {
            builder.AppendLine("- missing_truth_warnings:");
            foreach (var warning in input.MissingTruthWarnings)
            {
                if (string.IsNullOrWhiteSpace(warning))
                {
                    continue;
                }

                builder.AppendLine($"  - {warning.Trim()}");
            }
        }

        builder.AppendLine();
    }

    private static void AppendList(StringBuilder builder, IReadOnlyList<string>? items)
    {
        if (items is null || items.Count == 0)
        {
            builder.AppendLine("- (none)");
            return;
        }

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                continue;
            }

            builder.AppendLine($"- {item.Trim()}");
        }
    }

    private static WorkerAgentParsedResult ParseResult(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);
        var trimmed = StripCodeFence(raw.Trim());
        using var document = JsonDocument.Parse(trimmed);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Worker reply JSON root must be an object.");
        }

        var status = NormalizeStatus(ReadString(root, "status") ?? string.Empty);
        var summary = ReadString(root, "summary") ?? string.Empty;
        var plan = ReadStringArray(root, "plan");
        var actions = ReadStringArray(root, "actions");
        var modifications = ReadModifications(root);
        var blockers = ReadStringArray(root, "blockers");
        var risks = ReadStringArray(root, "risks");
        var warnings = ReadStringArray(root, "warnings");
        var edits = ReadEdits(root);

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new InvalidOperationException("Worker reply JSON must include non-empty 'status'.");
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new InvalidOperationException("Worker reply JSON must include non-empty 'summary'.");
        }

        return new WorkerAgentParsedResult(status, summary.Trim(), plan, actions, modifications, blockers, risks, warnings, edits);
    }

    private static string NormalizeStatus(string status)
    {
        var trimmed = status.Trim();
        var normalized = trimmed.Trim(':', '"', '\'').ToLowerInvariant();
        return normalized switch
        {
            "success" => "success",
            "partial" => "partial",
            "failed" => "failed",
            "fail" => "failed",
            "refused" => "refused",
            "refuse" => "refused",
            "complete" => "complete",
            "completed" => "completed",
            "done" => "done",
            _ => trimmed
        };
    }

    private static IReadOnlyList<WorkerEdit> ReadEdits(JsonElement root)
    {
        if (!root.TryGetProperty("edits", out var node) || node.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<WorkerEdit>();
        }

        var items = new List<WorkerEdit>();
        foreach (var item in node.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var path = ReadString(item, "path");
            var operation = ReadString(item, "operation");
            var content = ReadString(item, "content");
            var anchor = ReadString(item, "anchor");
            var slotId = ReadString(item, "slotId");

            if (string.IsNullOrWhiteSpace(path)
                || string.IsNullOrWhiteSpace(operation)
                || content is null)
            {
                continue;
            }

            items.Add(new WorkerEdit(
                path.Trim(),
                operation.Trim().ToLowerInvariant(),
                content,
                string.IsNullOrWhiteSpace(anchor) ? null : anchor,
                string.IsNullOrWhiteSpace(slotId) ? null : slotId.Trim()));
        }

        return items;
    }

    private static IReadOnlyList<WorkerAgentModification> ReadModifications(JsonElement root)
    {
        if (!root.TryGetProperty("modifications", out var node) || node.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<WorkerAgentModification>();
        }

        var items = new List<WorkerAgentModification>();
        foreach (var item in node.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var path = ReadString(item, "path");
            var kind = ReadString(item, "kind");
            var summary = ReadString(item, "summary");
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(kind))
            {
                continue;
            }

            items.Add(new WorkerAgentModification(path.Trim(), kind.Trim(), (summary ?? string.Empty).Trim()));
        }

        return items;
    }

    private static string StripCodeFence(string value)
    {
        if (!value.StartsWith("```", StringComparison.Ordinal))
        {
            return value;
        }

        var firstNewline = value.IndexOf('\n');
        if (firstNewline < 0)
        {
            return value;
        }

        var body = value[(firstNewline + 1)..];
        var lastFence = body.LastIndexOf("```", StringComparison.Ordinal);
        return lastFence >= 0 ? body[..lastFence].Trim() : body.Trim();
    }

    private static string? ReadString(JsonElement root, string property)
    {
        return root.TryGetProperty(property, out var node) && node.ValueKind == JsonValueKind.String
            ? node.GetString()
            : null;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var node) || node.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var items = new List<string>();
        foreach (var item in node.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    items.Add(value.Trim());
                }
            }
        }

        return items;
    }

    private static string Safe(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(none)" : value.Trim();
    }

    private static string Truncate(string? value, int maxChars)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxChars ? value : value[..maxChars];
    }

    private static IOpenRouterExecutionClient DefaultClientFactory(RoleProfile profile)
    {
        var configuration = OpenRouterConfiguration.FromEnvironment();
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(profile.TimeoutSeconds)
        };
        return new OpenRouterExecutionClient(configuration, httpClient, allowEnvironmentFallback: false);
    }
}

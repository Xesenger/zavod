using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using zavod.Execution;
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
    IReadOnlyList<string>? RevisionNotes = null);

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
            MaxTokens: _profile.MaxTokens);

        var stopwatch = Stopwatch.StartNew();
        var response = client.Execute(request);
        stopwatch.Stop();

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

        if (input.Anchors is { Count: > 0 })
        {
            builder.AppendLine("CODE ANCHORS (grounded file tree — use these paths as the basis for your plan)");
            foreach (var anchor in input.Anchors)
            {
                if (string.IsNullOrWhiteSpace(anchor))
                {
                    continue;
                }

                builder.AppendLine(anchor.Trim());
            }

            builder.AppendLine();
        }

        if (input.RevisionNotes is { Count: > 0 } revisionNotes)
        {
            builder.AppendLine("REVISION NOTES (feedback from previous attempts within THIS task — use to refine, do not redefine the intent)");
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

        builder.AppendLine("OUTPUT — reply with a single strict JSON object only, no code fences, no prose around it:");
        builder.AppendLine("{");
        builder.AppendLine("  \"status\": \"success\" | \"partial\" | \"failed\" | \"refused\",");
        builder.AppendLine("  \"summary\": \"<short human-readable summary of what was done or why refused>\",");
        builder.AppendLine("  \"plan\": [\"<step 1>\", \"<step 2>\"],");
        builder.AppendLine("  \"actions\": [\"<bounded action taken>\"],");
        builder.AppendLine("  \"modifications\": [{\"path\": \"<relative path>\", \"kind\": \"edit|create|delete\", \"summary\": \"<one line>\"}],");
        builder.AppendLine("  \"edits\": [");
        builder.AppendLine("    {");
        builder.AppendLine("      \"path\": \"<relative path, forward slashes>\",");
        builder.AppendLine("      \"operation\": \"write_full\" | \"insert_after\",");
        builder.AppendLine("      \"content\": \"<literal text to write or insert; no markdown fences, no ellipses>\",");
        builder.AppendLine("      \"anchor\": \"<for insert_after: exact-match unique string from current file; omit for write_full>\"");
        builder.AppendLine("    }");
        builder.AppendLine("  ],");
        builder.AppendLine("  \"blockers\": [\"<what stopped further progress, empty if none>\"],");
        builder.AppendLine("  \"risks\": [\"<known risks, empty if none>\"],");
        builder.AppendLine("  \"warnings\": [\"<scope or canon warnings, empty if none>\"]");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("Use status \"refused\" when execution basis is insufficient or the task violates scope/canon.");
        builder.AppendLine("Use status \"failed\" only for concrete blockers you ran into.");
        builder.AppendLine("Do not invent files or APIs. All paths in modifications and edits must be plausible relative to the project root.");
        builder.AppendLine();
        builder.AppendLine("EDITS CONTRACT:");
        builder.AppendLine("- For every path listed in modifications you MUST emit a corresponding edit in the edits array — a plan without edits is not a deliverable.");
        builder.AppendLine("- write_full: set content to the COMPLETE new file body. Use for small files or when rewriting entirely.");
        builder.AppendLine("- insert_after: set anchor to a unique substring present in the current file (draw from the snippet you saw in CODE ANCHORS), and content to the text to insert right after that anchor. Use for targeted additions to larger files.");
        builder.AppendLine("- Each edit's content is written verbatim. No markdown fences, no \"...\" placeholders, no commentary.");
        builder.AppendLine("- If you cannot produce concrete edit content for a path, drop it from modifications and list the reason in blockers — do not pretend a plan equals a deliverable.");
        return builder.ToString();
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

        var status = ReadString(root, "status") ?? string.Empty;
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

        return new WorkerAgentParsedResult(status.Trim(), summary.Trim(), plan, actions, modifications, blockers, risks, warnings, edits);
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
                string.IsNullOrWhiteSpace(anchor) ? null : anchor));
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

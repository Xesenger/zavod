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

namespace zavod.Qc;

public sealed record QcAgentInput(
    string ProjectName,
    string ProjectRoot,
    string ProjectKind,
    string TaskId,
    string TaskDescription,
    IReadOnlyList<string> AcceptanceCriteria,
    string WorkerStatus,
    string WorkerSummary,
    IReadOnlyList<string> WorkerBlockers,
    IReadOnlyList<string> WorkerWarnings,
    IReadOnlyList<string> WorkerModifications,
    IReadOnlyList<string>? StagedArtifacts = null,
    CanonicalDocsStatus? CanonicalDocsStatus = null,
    PreviewStatus? PreviewStatus = null,
    IReadOnlyList<string>? MissingTruthWarnings = null,
    bool IsFirstCycle = false);

public sealed record QcAgentParsedReply(
    string Decision,
    string Rationale,
    IReadOnlyList<string> Issues,
    string NextAction);

public sealed record QcAgentResult(
    bool Success,
    QcAgentParsedReply? Parsed,
    string ModelId,
    long LatencyMs,
    string? DiagnosticCode,
    string? DiagnosticMessage,
    string? RawResponse,
    string? TelemetryDirectory);

public sealed class QcAgentRuntime
{
    private readonly RoleProfile _profile;
    private readonly LabTelemetryWriter _telemetry;
    private readonly Func<RoleProfile, IOpenRouterExecutionClient> _clientFactory;
    private readonly Lazy<string> _systemPrompt;

    public QcAgentRuntime(
        RolesConfiguration? rolesConfiguration = null,
        LabTelemetryWriter? telemetryWriter = null,
        Func<RoleProfile, IOpenRouterExecutionClient>? clientFactory = null)
    {
        _profile = (rolesConfiguration ?? RolesConfiguration.LoadOrDefault()).Qc;
        _telemetry = telemetryWriter ?? new LabTelemetryWriter();
        _clientFactory = clientFactory ?? DefaultClientFactory;
        _systemPrompt = new Lazy<string>(LoadSystemPrompt);
    }

    public QcAgentResult Run(QcAgentInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var systemPrompt = _systemPrompt.Value;
        var userPrompt = BuildUserPrompt(input);
        var client = _clientFactory(_profile);
        var request = new OpenRouterExecutionRequest(
            RouteId: "qc.agent",
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
        stopwatch.Stop();

        QcAgentParsedReply? parsed = null;
        string? parseError = null;
        if (response.Success)
        {
            try
            {
                parsed = ParseReply(response.Content);
            }
            catch (Exception exception)
            {
                parseError = exception.Message;
            }
        }

        var success = response.Success && parsed is not null;
        var diagnosticCode = response.Success
            ? (parsed is null ? "QC_PARSE_FAILED" : null)
            : response.Diagnostic?.Code;
        var diagnosticMessage = response.Success
            ? parseError
            : response.Diagnostic?.Message;

        var telemetryDirectory = _telemetry.Write(
            input.ProjectRoot,
            "qc",
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
                acceptanceCount = input.AcceptanceCriteria?.Count ?? 0,
                input.WorkerStatus,
                workerBlockerCount = input.WorkerBlockers?.Count ?? 0,
                workerWarningCount = input.WorkerWarnings?.Count ?? 0,
                workerModificationCount = input.WorkerModifications?.Count ?? 0,
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

        return new QcAgentResult(
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
        var path = PromptSystemCatalog.ResolvePromptPath("qc.system.md");
        return File.ReadAllText(path).Trim();
    }

    private static string BuildUserPrompt(QcAgentInput input)
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

        builder.AppendLine("ACCEPTANCE CRITERIA");
        AppendList(builder, input.AcceptanceCriteria);
        builder.AppendLine();

        AppendWorkPacketBlock(builder, input);

        builder.AppendLine("WORKER RESULT TO REVIEW");
        builder.AppendLine($"- status: {Safe(input.WorkerStatus)}");
        builder.AppendLine($"- summary: {Safe(input.WorkerSummary)}");
        builder.AppendLine();

        builder.AppendLine("WORKER BLOCKERS");
        AppendList(builder, input.WorkerBlockers);
        builder.AppendLine();

        builder.AppendLine("WORKER WARNINGS");
        AppendList(builder, input.WorkerWarnings);
        builder.AppendLine();

        builder.AppendLine("WORKER MODIFICATIONS");
        AppendList(builder, input.WorkerModifications);
        builder.AppendLine();

        if (input.StagedArtifacts is { Count: > 0 } stagedArtifacts)
        {
            builder.AppendLine("STAGED EXECUTION ARTEFACTS (real content written to the staging sandbox, not the project)");
            AppendList(builder, stagedArtifacts);
            builder.AppendLine();
            builder.AppendLine("Absence of staged artefacts when the Worker claims real modifications is a REVISE signal; presence with concrete byte deltas is evidence you may cite.");
            builder.AppendLine("Staged artefacts are not applied to the project yet. Even on ACCEPT, next_action must say to surface the candidate result for user review/acceptance, not to apply it automatically.");
            builder.AppendLine();
        }

        builder.AppendLine("OUTPUT - reply with a single strict JSON object only, no code fences, no prose around it.");
        builder.AppendLine("Return a JSON object with exactly these top-level keys:");
        builder.AppendLine("- decision: string; allowed values are ACCEPT, REVISE, REJECT.");
        builder.AppendLine("- rationale: non-empty string grounded in Worker result and staged artefacts.");
        builder.AppendLine("- issues: array of strings; empty when no issue is found.");
        builder.AppendLine("- next_action: one short sentence describing what should happen next.");
        builder.AppendLine("Do not output pipe-separated alternatives. Do not copy placeholder values.");
        return builder.ToString();
    }

    private static void AppendWorkPacketBlock(StringBuilder builder, QcAgentInput input)
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

        var appended = 0;
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                continue;
            }

            builder.AppendLine($"- {item.Trim()}");
            appended++;
        }

        if (appended == 0)
        {
            builder.AppendLine("- (none)");
        }
    }

    private static QcAgentParsedReply ParseReply(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);
        var trimmed = StripCodeFence(raw.Trim());
        using var document = JsonDocument.Parse(trimmed);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("QC reply JSON root must be an object.");
        }

        var decision = ReadString(root, "decision") ?? string.Empty;
        var rationale = ReadString(root, "rationale") ?? string.Empty;
        var issues = ReadStringArray(root, "issues");
        var nextAction = ReadString(root, "next_action") ?? string.Empty;

        var normalizedDecision = NormalizeDecision(decision);
        if (normalizedDecision is not ("ACCEPT" or "REVISE" or "REJECT"))
        {
            throw new InvalidOperationException($"QC reply 'decision' must be ACCEPT|REVISE|REJECT (got '{decision}').");
        }

        if (string.IsNullOrWhiteSpace(rationale))
        {
            throw new InvalidOperationException("QC reply JSON must include non-empty 'rationale'.");
        }

        return new QcAgentParsedReply(normalizedDecision, rationale.Trim(), issues, nextAction.Trim());
    }

    private static string NormalizeDecision(string decision)
    {
        return decision.Trim().Trim(':', '"', '\'').Trim().ToUpperInvariant();
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

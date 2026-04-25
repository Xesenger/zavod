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

namespace zavod.Lead;

public sealed record LeadAgentTurn(
    string Role,
    string Text);

public sealed record LeadAgentInput(
    string ProjectName,
    string ProjectRoot,
    string ProjectKind,
    string UserMessage,
    string PreClassifierIntentState,
    string CurrentIntentSummary,
    IReadOnlyList<string> AdvisoryNotes,
    IReadOnlyList<LeadAgentTurn> RecentTurns,
    bool IsOrientationRequest,
    IReadOnlyList<string> ProjectStackSummary,
    CanonicalDocsStatus? CanonicalDocsStatus = null,
    PreviewStatus? PreviewStatus = null,
    IReadOnlyList<string>? MissingTruthWarnings = null,
    bool IsFirstCycle = false);

public sealed record LeadAgentParsedReply(
    string IntentState,
    string Reply,
    string ScopeNotes,
    string TaskBrief,
    IReadOnlyList<string> Warnings);

public sealed record LeadAgentResult(
    bool Success,
    string Reply,
    LeadAgentParsedReply? Parsed,
    string ModelId,
    long LatencyMs,
    string? DiagnosticCode,
    string? DiagnosticMessage,
    string? RawResponse,
    string? TelemetryDirectory);

public sealed class LeadAgentRuntime
{
    private readonly RoleProfile _profile;
    private readonly LabTelemetryWriter _telemetry;
    private readonly Func<RoleProfile, IOpenRouterExecutionClient> _clientFactory;
    private readonly Lazy<string> _systemPrompt;

    public LeadAgentRuntime(
        RolesConfiguration? rolesConfiguration = null,
        LabTelemetryWriter? telemetryWriter = null,
        Func<RoleProfile, IOpenRouterExecutionClient>? clientFactory = null)
    {
        _profile = (rolesConfiguration ?? RolesConfiguration.LoadOrDefault()).Lead;
        _telemetry = telemetryWriter ?? new LabTelemetryWriter();
        _clientFactory = clientFactory ?? DefaultClientFactory;
        _systemPrompt = new Lazy<string>(LoadSystemPrompt);
    }

    public LeadAgentResult Run(LeadAgentInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var systemPrompt = _systemPrompt.Value;
        var userPrompt = BuildUserPrompt(input);
        var client = _clientFactory(_profile);
        var request = new OpenRouterExecutionRequest(
            RouteId: "lead.agent",
            SystemPrompt: systemPrompt,
            UserPrompt: userPrompt,
            ModelId: _profile.Model,
            Temperature: _profile.Temperature,
            Attachments: null,
            MaxTokens: _profile.MaxTokens);

        var stopwatch = Stopwatch.StartNew();
        var response = client.Execute(request);
        stopwatch.Stop();

        LeadAgentParsedReply? parsed = null;
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
            ? (parsed is null ? "LEAD_PARSE_FAILED" : null)
            : response.Diagnostic?.Code;
        var diagnosticMessage = response.Success
            ? parseError
            : response.Diagnostic?.Message;

        var telemetryDirectory = _telemetry.Write(
            input.ProjectRoot,
            "lead",
            "send-message",
            new
            {
                request.RouteId,
                request.ModelId,
                request.Temperature,
                input.ProjectName,
                input.ProjectKind,
                input.PreClassifierIntentState,
                input.CurrentIntentSummary,
                advisoryNotesCount = input.AdvisoryNotes?.Count ?? 0,
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

        var replyText = parsed?.Reply ?? string.Empty;
        return new LeadAgentResult(
            success,
            replyText,
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
        var path = PromptSystemCatalog.ResolvePromptPath("lead.system.md");
        return File.ReadAllText(path).Trim();
    }

    private static string BuildUserPrompt(LeadAgentInput input)
    {
        var builder = new StringBuilder();
        builder.AppendLine("PROJECT CONTEXT");
        builder.AppendLine($"- name: {Safe(input.ProjectName)}");
        builder.AppendLine($"- kind: {Safe(input.ProjectKind)}");
        builder.AppendLine($"- root: {Safe(input.ProjectRoot)}");
        builder.AppendLine();

        if (input.ProjectStackSummary is { Count: > 0 })
        {
            builder.AppendLine("PROJECT STACK (observed by scanner — trust over guesses)");
            foreach (var line in input.ProjectStackSummary)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                builder.AppendLine($"- {line.Trim()}");
            }

            builder.AppendLine();
        }

        AppendWorkPacketBlock(builder, input);

        builder.AppendLine("PRE-CLASSIFIER HINT");
        builder.AppendLine($"- intent_state: {Safe(input.PreClassifierIntentState)}");
        builder.AppendLine($"- current_summary: {Safe(input.CurrentIntentSummary)}");
        builder.AppendLine();

        if (input.AdvisoryNotes is { Count: > 0 })
        {
            builder.AppendLine("ADVISORY NOTES (project history hints, may be stale)");
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

        if (input.RecentTurns is { Count: > 0 })
        {
            builder.AppendLine("RECENT CONVERSATION (oldest first, so you can see the framing dialogue)");
            foreach (var turn in input.RecentTurns)
            {
                if (turn is null || string.IsNullOrWhiteSpace(turn.Text))
                {
                    continue;
                }

                var role = string.IsNullOrWhiteSpace(turn.Role) ? "unknown" : turn.Role.Trim().ToLowerInvariant();
                var collapsed = turn.Text.Replace("\r", " ").Replace("\n", " ").Trim();
                if (collapsed.Length > 400)
                {
                    collapsed = collapsed[..397] + "...";
                }

                builder.AppendLine($"- [{role}] {collapsed}");
            }

            builder.AppendLine();
        }

        if (input.IsOrientationRequest)
        {
            builder.AppendLine("ORIENTATION MODE — user is asking a meta or identity question.");
            builder.AppendLine("- Anchor your reply in the ZAVOD system: explain that this is ZAVOD, a project-aware execution environment, and your role is Shift Lead.");
            builder.AppendLine("- Name the current project from PROJECT CONTEXT.");
            builder.AppendLine("- If asked \"what model\" or \"who are you\" — say you are Shift Lead running on the configured model for this role; do not speculate about specific model names unless they were provided in context.");
            builder.AppendLine("- Keep it short, warm, and project-aware. Intent state for orientation questions is \"orientation\".");
            builder.AppendLine();
        }

        builder.AppendLine("USER MESSAGE");
        builder.AppendLine(input.UserMessage?.Trim() ?? string.Empty);
        builder.AppendLine();
        builder.AppendLine("OUTPUT — reply with a single strict JSON object only, no code fences, no prose around it:");
        builder.AppendLine("{");
        builder.AppendLine("  \"intent_state\": \"candidate\" | \"refining\" | \"ready_for_validation\" | \"orientation\" | \"rejected\",");
        builder.AppendLine("  \"reply\": \"<conversational reply to the user, in their language, focused on framing>\",");
        builder.AppendLine("  \"scope_notes\": \"<one short line about scope or framing or empty string>\",");
        builder.AppendLine("  \"task_brief\": \"<ACTIONABLE ONE-LINE TASK DESCRIPTION for a Worker, written in English or the user's language; REQUIRED when intent_state is ready_for_validation, may be empty otherwise>\",");
        builder.AppendLine("  \"warnings\": [\"<optional canon/scope warnings, empty array if none>\"]");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("Use \"rejected\" only when the request directly contradicts project canon or is out-of-scope for the project kind.");
        builder.AppendLine("When intent_state is ready_for_validation, task_brief must be a concrete imperative task summary (e.g. \"Add an FPS counter to the top-right corner of the cssDOOM browser game HUD\"), NOT the raw user reply.");
        builder.AppendLine("Stay concise. Do not promise execution. Do not invent files or APIs.");
        return builder.ToString();
    }

    private static void AppendWorkPacketBlock(StringBuilder builder, LeadAgentInput input)
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

    private static LeadAgentParsedReply ParseReply(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);
        var trimmed = StripCodeFence(raw.Trim());
        using var document = JsonDocument.Parse(trimmed);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Lead reply JSON root must be an object.");
        }

        var intentState = ReadString(root, "intent_state") ?? string.Empty;
        var reply = ReadString(root, "reply") ?? string.Empty;
        var scopeNotes = ReadString(root, "scope_notes") ?? string.Empty;
        var taskBrief = ReadString(root, "task_brief") ?? string.Empty;
        var warnings = ReadStringArray(root, "warnings");

        if (string.IsNullOrWhiteSpace(reply))
        {
            throw new InvalidOperationException("Lead reply JSON must include non-empty 'reply'.");
        }

        return new LeadAgentParsedReply(intentState, reply.Trim(), scopeNotes.Trim(), taskBrief.Trim(), warnings);
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

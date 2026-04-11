using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace zavod.Contexting;

public sealed record IntentSignalMatch(bool Detected, IReadOnlyList<string> Matches);

public sealed record IntentClassificationResult(
    string NormalizedText,
    IntentSignalMatch DetectedAction,
    IntentSignalMatch DetectedTarget,
    IReadOnlyList<string> DetectedBlockers,
    int Score,
    bool IntentOverride,
    ContextIntentState FinalState)
{
    public string ToDebugString(string text)
    {
        return string.Join(
            Environment.NewLine,
            "[IntentClassifier]",
            $"text=\"{text}\"",
            $"normalized=\"{NormalizedText}\"",
            $"action={DetectedAction.Detected.ToString().ToLowerInvariant()} (match: {FormatList(DetectedAction.Matches)})",
            $"target={DetectedTarget.Detected.ToString().ToLowerInvariant()} (match: {FormatList(DetectedTarget.Matches)})",
            $"blockers={FormatList(DetectedBlockers)}",
            $"score={Score}",
            $"override={IntentOverride.ToString().ToLowerInvariant()}",
            $"state={FinalState}");
    }

    private static string FormatList(IReadOnlyList<string> values)
    {
        return values.Count == 0
            ? "[]"
            : $"[{string.Join(", ", values.Select(value => $"\"{value}\""))}]";
    }
}

public static class ProductIntentClassifier
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex RepeatedLettersRegex = new(@"(\p{L})\1{2,}", RegexOptions.Compiled);
    private sealed record BlockerSignalGroup(string Level, string Category, string[] Signals);

    private static readonly string[] ActionSignals =
    {
        "\u0438\u0441\u043f\u0440\u0430\u0432",
        "\u0438\u0437\u043c\u0435\u043d",
        "\u043e\u0431\u043d\u043e\u0432",
        "\u0434\u043e\u0431\u0430\u0432",
        "\u0441\u0434\u0435\u043b",
        "\u043f\u043e\u043f\u0440\u0430\u0432",
        "\u043f\u0435\u0440\u0435\u0434\u0435\u043b",
        "\u043f\u0435\u0440\u0435\u043c\u0435\u0441\u0442",
        "\u043f\u043e\u043a\u0430\u0436",
        "\u0441\u043a\u0440\u043e\u0439",
        "\u043f\u0435\u0440\u0435\u0438\u043c\u0435\u043d",
        "fix",
        "change",
        "update",
        "add",
        "make",
        "move",
        "show",
        "hide",
        "rename",
        "align"
    };

    private static readonly string[] RequestSignals =
    {
        "\u0445\u043e\u0447\u0443",
        "\u043d\u0443\u0436\u043d\u043e",
        "\u043d\u0430\u0434\u043e",
        "need",
        "want"
    };

    private static readonly string[] TargetSignals =
    {
        "button",
        "ui",
        "screen",
        "layout",
        "alignment",
        "project home",
        "xaml",
        "\u043a\u043d\u043e\u043f\u043a",
        "\u044d\u043a\u0440\u0430\u043d",
        "\u0444\u043e\u043d",
        "\u0440\u0435\u0437\u0443\u043b\u044c\u0442\u0430\u0442",
        "\u0440\u0430\u0437\u043c\u0435\u0442\u043a",
        ".qml",
        ".xaml",
        ".cs"
    };

    private static readonly BlockerSignalGroup[] BlockerGroups =
    {
        new("hard", "thinking", new[]
        {
            "\u0434\u0430\u0432\u0430\u0439 \u043f\u043e\u0434\u0443\u043c\u0430\u0435\u043c",
            "\u043f\u043e\u0434\u0443\u043c\u0430\u0435\u043c",
            "\u0447\u0442\u043e \u0443 \u043d\u0430\u0441 \u043e\u0441\u0442\u0430\u043b\u043e\u0441\u044c",
            "let's think"
        }),
        new("hard", "question", new[]
        {
            "\u043a\u0430\u043a",
            "\u043f\u043e\u0447\u0435\u043c\u0443",
            "\u0447\u0442\u043e \u0435\u0441\u043b\u0438",
            "\u0447\u0442\u043e \u0434\u0443\u043c\u0430\u0435\u0448\u044c",
            "how",
            "why",
            "what if"
        }),
        new("hard", "social", new[]
        {
            "\u043f\u0440\u0438\u0432\u0435\u0442",
            "\u043e\u043a",
            "\u043f\u043e\u043d\u044f\u043b",
            "hello",
            "ok",
            "got it"
        }),
        new("soft", "polite", new[]
        {
            "\u043c\u043e\u0436\u0435\u0448\u044c",
            "\u043f\u043e\u0436\u0430\u043b\u0443\u0439\u0441\u0442\u0430",
            "can you",
            "please",
            "pls",
            "\u043f\u043b\u0438\u0437"
        }),
        new("soft", "uncertainty", new[]
        {
            "\u043d\u0430\u0432\u0435\u0440\u043d\u043e\u0435",
            "\u043d\u0430\u0434\u043e \u0431\u044b",
            "\u043c\u043e\u0436\u0435\u0442 \u0431\u044b\u0442\u044c",
            "maybe",
            "maybe we should"
        }),
        new("soft", "filler", new[]
        {
            "\u044d\u044d",
            "\u0431\u043b\u0438\u043d"
        })
    };

    public static IntentClassificationResult Classify(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var normalized = NormalizeInput(text);
        var wordCount = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var actionMatches = CollectMatches(normalized, ActionSignals, RequestSignals);
        var targetMatches = CollectMatches(normalized, TargetSignals);
        var blockerMatches = CollectBlockers(normalized);
        var hasHardBlockers = blockerMatches.Any(static blocker => blocker.StartsWith("hard:", StringComparison.Ordinal));

        var hasAction = actionMatches.Count > 0;
        var hasTarget = targetMatches.Count > 0;
        var score = 0;
        var intentOverride = hasAction && hasTarget;

        if (hasAction)
        {
            score += 1;
        }

        if (hasTarget)
        {
            score += 1;
        }

        if (blockerMatches.Count > 0)
        {
            score -= 1;
        }

        return new IntentClassificationResult(
            normalized,
            new IntentSignalMatch(hasAction, actionMatches),
            new IntentSignalMatch(hasTarget, targetMatches),
            blockerMatches,
            score,
            intentOverride,
            ResolveFinalState(wordCount, hasAction, hasTarget, hasHardBlockers, score, intentOverride));
    }

    public static string NormalizeInput(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var normalized = text.Trim().ToLowerInvariant();
        normalized = WhitespaceRegex.Replace(normalized, " ");
        normalized = RepeatedLettersRegex.Replace(normalized, "$1");
        return normalized;
    }

    private static List<string> CollectMatches(string text, params string[][] signalGroups)
    {
        var matches = new List<string>();

        foreach (var signal in signalGroups.SelectMany(static group => group))
        {
            if (text.Contains(signal, StringComparison.Ordinal))
            {
                matches.Add(signal);
            }
        }

        return matches;
    }

    private static List<string> CollectBlockers(string text)
    {
        var matches = new List<string>();

        foreach (var group in BlockerGroups)
        {
            foreach (var signal in group.Signals)
            {
                if (text.Contains(signal, StringComparison.Ordinal))
                {
                    matches.Add($"{group.Level}:{group.Category}:{signal}");
                }
            }
        }

        return matches;
    }

    private static ContextIntentState ResolveFinalState(int wordCount, bool hasAction, bool hasTarget, bool hasHardBlockers, int score, bool intentOverride)
    {
        if (intentOverride)
        {
            return ContextIntentState.ReadyForValidation;
        }

        if (score >= 2)
        {
            return ContextIntentState.ReadyForValidation;
        }

        if (!hasAction && !hasTarget)
        {
            return wordCount <= 2
                ? ContextIntentState.Candidate
                : ContextIntentState.Refining;
        }

        if (hasHardBlockers)
        {
            return ContextIntentState.Refining;
        }

        return ContextIntentState.Candidate;
    }
}

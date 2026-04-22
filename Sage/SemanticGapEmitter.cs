using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace zavod.Sage;

// S3 MVP emitter: semantic_gap.
//
// Detects clear concept drop between the user's raw request and what
// Lead reduced it to (TaskBrief + LeadReply).
//
// Design bias (per user directive for S3):
//   - Prefer false negatives over false positives.
//   - Detect clear dropped concepts, not paraphrase drift.
//
// Contract (v2.1 / v2.1a):
//   - Severity  = Hint only    (never blocks, never must-show)
//   - Channel   = SageOnly     (never injected into role prompts)
//   - Returns   = at most ONE observation per call
//   - No state  = stateless function; does not mutate any caller input
//
// S4a morphology guard: before marking a user token as dropped, check
// whether any brief token shares its leading 5 characters. This covers
// common Russian declension ("красным" vs "красный" share "красн") and
// English suffix variation ("counter" vs "counted" share "count").
// Known residual false-negatives: unrelated words with shared prefix
// ("красный" vs "красивый") will be treated as match. Acceptable per
// "prefer underfiring over overfiring" — false negatives cost silence,
// false positives cost trust.
internal static class SemanticGapEmitter
{
    private const int MinTokenLength = 6;
    private const int MinUserTokens = 5;
    private const int MinDroppedTokens = 2;
    private const double MinDroppedRatio = 0.5;
    private const double MaxDroppedRatio = 0.9;

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "should", "would", "could", "please", "maybe", "about",
        "cannot", "using", "where", "which",
        "задача", "проект", "чтобы", "после", "перед", "через",
        "нужно", "можно", "сделать", "делать", "давайте", "пожалуйста",
        "сейчас", "также", "можешь"
    };

    public static SageObservation? TryObserve(SageAfterIntentContext context)
    {
        if (context is null) return null;
        if (!context.LeadSuccess) return null;
        if (!string.Equals(context.FinalIntentState, "ReadyForValidation", StringComparison.Ordinal)) return null;

        var userTokens = Tokenize(context.UserMessage);
        if (userTokens.Count < MinUserTokens) return null;

        var briefBuffer = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(context.TaskBrief))
        {
            briefBuffer.Append(context.TaskBrief).Append(' ');
        }
        if (!string.IsNullOrWhiteSpace(context.LeadReply))
        {
            briefBuffer.Append(context.LeadReply);
        }
        if (briefBuffer.Length == 0) return null;

        var briefTokens = Tokenize(briefBuffer.ToString());
        if (briefTokens.Count == 0) return null;

        var dropped = userTokens.Where(t => !IsCoveredByBrief(t, briefTokens)).ToArray();
        if (dropped.Length < MinDroppedTokens) return null;

        var ratio = (double)dropped.Length / userTokens.Count;
        if (ratio < MinDroppedRatio) return null;
        if (ratio > MaxDroppedRatio) return null;

        var preview = string.Join(", ", dropped.OrderBy(t => t, StringComparer.Ordinal).Take(3));
        return new SageObservation(
            Type: SageObservationType.SemanticGap,
            Severity: SageSeverity.Hint,
            Message: $"Lead brief dropped {dropped.Length} user concepts: {preview}",
            Stage: SageStage.AfterIntent,
            Channel: SageChannel.SageOnly,
            ObservedAt: DateTimeOffset.UtcNow,
            ProjectId: context.ProjectId,
            TaskId: context.ActiveTaskId);
    }

    private const int MorphologyPrefixLength = 5;

    private static bool IsCoveredByBrief(string userToken, HashSet<string> briefTokens)
    {
        if (briefTokens.Contains(userToken))
        {
            return true;
        }
        if (userToken.Length < MorphologyPrefixLength)
        {
            return false;
        }
        var prefix = userToken.AsSpan(0, MorphologyPrefixLength);
        foreach (var brief in briefTokens)
        {
            if (brief.Length >= MorphologyPrefixLength
                && brief.AsSpan(0, MorphologyPrefixLength).SequenceEqual(prefix))
            {
                return true;
            }
        }
        return false;
    }

    private static HashSet<string> Tokenize(string? text)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(text)) return result;

        var buffer = new List<char>(text.Length);

        void Flush()
        {
            if (buffer.Count == 0) return;
            var token = new string(buffer.ToArray());
            buffer.Clear();
            if (token.Length < MinTokenLength) return;
            if (StopWords.Contains(token)) return;
            result.Add(token);
        }

        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer.Add(char.ToLowerInvariant(ch));
            }
            else
            {
                Flush();
            }
        }
        Flush();
        return result;
    }
}

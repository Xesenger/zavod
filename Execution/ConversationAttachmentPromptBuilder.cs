using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using zavod.Persistence;

namespace zavod.Execution;

internal static class ConversationAttachmentPromptBuilder
{
    private const int MaxAttachments = 4;
    private const int MaxAttachmentChars = 4000;

    public static IReadOnlyList<OpenRouterAttachment> Load(IReadOnlyList<ConversationComposerDraftItem> drafts)
    {
        ArgumentNullException.ThrowIfNull(drafts);

        return drafts
            .Take(MaxAttachments)
            .Select(BuildAttachment)
            .ToArray();
    }

    public static string BuildUserPrompt(string userPrompt, IReadOnlyList<OpenRouterAttachment>? attachments)
    {
        var normalizedPrompt = userPrompt?.Trim() ?? string.Empty;
        if (attachments is null || attachments.Count == 0)
        {
            return normalizedPrompt;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Attached content:");
        builder.AppendLine();
        for (var index = 0; index < attachments.Count; index++)
        {
            var attachment = attachments[index];
            builder.Append('[').Append(index + 1).Append("] ")
                .Append(attachment.Label)
                .Append(" (type=")
                .Append(attachment.IntakeType)
                .Append(", source=")
                .Append(attachment.FilePath)
                .AppendLine(")");
            if (attachment.UsedPreviewOnly)
            {
                builder.AppendLine("Preview only:");
            }

            builder.AppendLine(attachment.Content);
            if (index < attachments.Count - 1)
            {
                builder.AppendLine();
                builder.AppendLine("---");
                builder.AppendLine();
            }
        }

        builder.AppendLine();
        builder.AppendLine("User message:");
        builder.AppendLine(string.IsNullOrWhiteSpace(normalizedPrompt) ? "[none]" : normalizedPrompt);
        return builder.ToString().Trim();
    }

    private static OpenRouterAttachment BuildAttachment(ConversationComposerDraftItem draft)
    {
        var filePath = draft.Reference.FilePath;
        var usedPreviewOnly = true;
        var content = draft.Preview;
        if (ShouldInlineContent(draft, filePath))
        {
            try
            {
                var raw = File.ReadAllText(filePath, Encoding.UTF8);
                var normalized = NormalizeContent(raw);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    content = normalized.Length > MaxAttachmentChars
                        ? normalized[..MaxAttachmentChars].TrimEnd() + Environment.NewLine + "[truncated]"
                        : normalized;
                    usedPreviewOnly = false;
                }
            }
            catch (Exception) when (File.Exists(filePath))
            {
                usedPreviewOnly = true;
                content = draft.Preview;
            }
        }

        return new OpenRouterAttachment(
            draft.DisplayName,
            draft.IntakeType,
            filePath,
            string.IsNullOrWhiteSpace(content) ? "[empty attachment]" : content,
            usedPreviewOnly);
    }

    private static bool ShouldInlineContent(ConversationComposerDraftItem draft, string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        if (string.Equals(draft.IntakeType, "text", StringComparison.OrdinalIgnoreCase)
            || string.Equals(draft.IntakeType, "document", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var extension = Path.GetExtension(filePath);
        return string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".log", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".yml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".yaml", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeContent(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
    }
}

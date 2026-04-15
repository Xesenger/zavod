using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using zavod.Tooling;

namespace zavod.Persistence;

public sealed record ConversationComposerDraftItem(
    string DraftId,
    string ConversationId,
    string? ProjectId,
    string Origin,
    string IntakeType,
    string DisplayName,
    string Preview,
    string Detail,
    long SizeBytes,
    ConversationArtifactReference Reference);

public sealed record ConversationComposerSubmission(
    string ConversationId,
    string Text,
    IReadOnlyList<ConversationComposerDraftItem> Attachments,
    string? ProjectId = null)
{
    public string NormalizedConversationId => ConversationId?.Trim() ?? string.Empty;

    public bool HasText => !string.IsNullOrWhiteSpace(Text);

    public bool HasAttachments => Attachments.Count > 0;

    public bool IsEmpty => !HasText && !HasAttachments;
}

public sealed class ConversationComposerDraftStore
{
    private const int LongTextArtifactMaxChars = 4000;
    private const int LongTextArtifactMaxLines = 40;

    private readonly ConversationArtifactStorage _artifactStorage;
    private readonly Dictionary<string, List<ConversationComposerDraftItem>> _draftsByConversation = new(StringComparer.Ordinal);

    public ConversationComposerDraftStore(ConversationArtifactStorage artifactStorage)
    {
        _artifactStorage = artifactStorage ?? throw new ArgumentNullException(nameof(artifactStorage));
    }

    public IReadOnlyList<ConversationComposerDraftItem> GetDrafts(string conversationId)
    {
        return _draftsByConversation.TryGetValue(conversationId, out var drafts)
            ? drafts.ToArray()
            : Array.Empty<ConversationComposerDraftItem>();
    }

    public IReadOnlyList<ConversationComposerDraftItem> ConsumeDrafts(string conversationId)
    {
        if (!_draftsByConversation.TryGetValue(conversationId, out var drafts) || drafts.Count == 0)
        {
            return Array.Empty<ConversationComposerDraftItem>();
        }

        _draftsByConversation.Remove(conversationId);
        return drafts.ToArray();
    }

    public bool RemoveDraft(string conversationId, string draftId)
    {
        if (!_draftsByConversation.TryGetValue(conversationId, out var drafts))
        {
            return false;
        }

        var removedItems = drafts
            .Where(candidate => string.Equals(candidate.DraftId, draftId, StringComparison.Ordinal))
            .ToArray();
        if (removedItems.Length == 0)
        {
            return false;
        }

        drafts.RemoveAll(candidate => string.Equals(candidate.DraftId, draftId, StringComparison.Ordinal));
        foreach (var item in removedItems)
        {
            if (File.Exists(item.Reference.FilePath))
            {
                File.Delete(item.Reference.FilePath);
            }
        }

        if (drafts.Count == 0)
        {
            _draftsByConversation.Remove(conversationId);
        }

        return true;
    }

    public IReadOnlyList<ConversationComposerDraftItem> StageFiles(
        string conversationId,
        string? projectId,
        IReadOnlyList<string> filePaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentNullException.ThrowIfNull(filePaths);

        var staged = new List<ConversationComposerDraftItem>();
        foreach (var path in filePaths.Where(static value => !string.IsNullOrWhiteSpace(value)))
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var fileInfo = new FileInfo(fullPath);
            var displayName = fileInfo.Name;
            var intakeType = IntakeArtifactFactory.DetermineType(new IntakeSourceInput(
                Guid.NewGuid().ToString("N"),
                "user_upload",
                displayName,
                FileExtension: fileInfo.Extension)).ToString().ToLowerInvariant();
            var reference = _artifactStorage.ImportConversationFile(conversationId, fullPath, displayName);
            staged.Add(AddDraft(
                conversationId,
                projectId,
                "user_upload",
                intakeType,
                displayName,
                reference.Preview,
                BuildFileDetail(intakeType, fileInfo.Length),
                fileInfo.Length,
                reference));
        }

        return staged;
    }

    public ConversationComposerDraftItem? StageLongTextArtifact(
        string conversationId,
        string? projectId,
        string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (!ShouldBecomeArtifact(text))
        {
            return null;
        }

        var normalized = text.Trim();
        var lines = CountLines(normalized);
        var reference = _artifactStorage.SaveConversationTextArtifact(
            conversationId,
            "Pasted text",
            normalized,
            "txt",
            preview: BuildTextPreview(normalized));
        return AddDraft(
            conversationId,
            projectId,
            "user_paste",
            "text",
            "Pasted text",
            reference.Preview,
            $"{normalized.Length} chars · {lines} lines",
            normalized.Length,
            reference);
    }

    public static bool ShouldBecomeArtifact(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim();
        return normalized.Length > LongTextArtifactMaxChars || CountLines(normalized) > LongTextArtifactMaxLines;
    }

    private ConversationComposerDraftItem AddDraft(
        string conversationId,
        string? projectId,
        string origin,
        string intakeType,
        string displayName,
        string preview,
        string detail,
        long sizeBytes,
        ConversationArtifactReference reference)
    {
        if (!_draftsByConversation.TryGetValue(conversationId, out var drafts))
        {
            drafts = new List<ConversationComposerDraftItem>();
            _draftsByConversation[conversationId] = drafts;
        }

        var item = new ConversationComposerDraftItem(
            Guid.NewGuid().ToString("N"),
            conversationId,
            string.IsNullOrWhiteSpace(projectId) ? null : projectId.Trim(),
            origin,
            intakeType,
            displayName,
            preview,
            detail,
            sizeBytes,
            reference);
        drafts.Add(item);
        return item;
    }

    private static string BuildTextPreview(string normalized)
    {
        var singleLine = normalized.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        return singleLine.Length <= 120
            ? singleLine
            : $"{singleLine[..117].TrimEnd()}...";
    }

    private static int CountLines(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n').Length;
    }

    private static string BuildFileDetail(string intakeType, long sizeBytes)
    {
        return $"{intakeType} · {sizeBytes} bytes";
    }
}

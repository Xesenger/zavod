using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace zavod.Persistence;

public sealed record ConversationIndexEntry(
    string ConversationId,
    string Mode,
    string? ProjectId,
    string Title,
    DateTimeOffset UpdatedAt);

public static class ConversationIndexStorage
{
    private const string IndexFileName = "index.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string GetIndexPath(string projectRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);
        return Path.Combine(ZavodLocalStorageLayout.GetRoot(Path.GetFullPath(projectRootPath)), IndexFileName);
    }

    public static IReadOnlyList<ConversationIndexEntry> Load(string projectRootPath)
    {
        var path = GetIndexPath(projectRootPath);
        if (!File.Exists(path))
        {
            return Array.Empty<ConversationIndexEntry>();
        }

        PersistedConversationIndex? document;
        try
        {
            document = JsonSerializer.Deserialize<PersistedConversationIndex>(File.ReadAllText(path, Encoding.UTF8), JsonOptions);
        }
        catch (JsonException)
        {
            return Array.Empty<ConversationIndexEntry>();
        }

        if (document?.Conversations is null)
        {
            return Array.Empty<ConversationIndexEntry>();
        }

        return document.Conversations
            .Where(IsValid)
            .Select(entry => new ConversationIndexEntry(
                entry.ConversationId!.Trim(),
                entry.Mode!.Trim(),
                string.IsNullOrWhiteSpace(entry.ProjectId) ? null : entry.ProjectId.Trim(),
                entry.Title!.Trim(),
                entry.UpdatedAt))
            .OrderByDescending(entry => entry.UpdatedAt)
            .ThenBy(entry => entry.ConversationId, StringComparer.Ordinal)
            .ToArray();
    }

    public static void Upsert(string projectRootPath, ConversationIndexEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.ConversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Mode);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Title);

        ZavodLocalStorageLayout.EnsureInitialized(projectRootPath);
        var existing = Load(projectRootPath)
            .Where(candidate => !string.Equals(candidate.ConversationId, entry.ConversationId, StringComparison.Ordinal))
            .ToList();
        existing.Add(entry with
        {
            ConversationId = entry.ConversationId.Trim(),
            Mode = entry.Mode.Trim(),
            ProjectId = string.IsNullOrWhiteSpace(entry.ProjectId) ? null : entry.ProjectId.Trim(),
            Title = entry.Title.Trim()
        });

        var document = new PersistedConversationIndex(
            existing
                .OrderByDescending(candidate => candidate.UpdatedAt)
                .ThenBy(candidate => candidate.ConversationId, StringComparer.Ordinal)
                .Select(candidate => new PersistedConversationIndexEntry(
                    candidate.ConversationId,
                    candidate.Mode,
                    candidate.ProjectId,
                    candidate.Title,
                    candidate.UpdatedAt))
                .ToArray());

        var serialized = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(GetIndexPath(projectRootPath), serialized, Encoding.UTF8);
    }

    private static bool IsValid(PersistedConversationIndexEntry entry)
    {
        return entry is not null
            && !string.IsNullOrWhiteSpace(entry.ConversationId)
            && !string.IsNullOrWhiteSpace(entry.Mode)
            && !string.IsNullOrWhiteSpace(entry.Title);
    }

    private sealed record PersistedConversationIndex(
        [property: JsonPropertyName("conversations")] PersistedConversationIndexEntry[]? Conversations);

    private sealed record PersistedConversationIndexEntry(
        [property: JsonPropertyName("conversationId")] string? ConversationId,
        [property: JsonPropertyName("mode")] string? Mode,
        [property: JsonPropertyName("projectId")] string? ProjectId,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt);
}

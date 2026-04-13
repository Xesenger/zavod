using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace zavod.Persistence;

public sealed record ConversationLogSnapshot(
    string MessageId,
    DateTimeOffset Timestamp,
    string Role,
    string Kind,
    string Text,
    string? Markdown,
    string? StepId,
    string? Phase,
    IReadOnlyList<string> Attachments,
    string Source,
    string Adapter,
    bool IsStreaming,
    IReadOnlyDictionary<string, string>? Metadata);

public sealed class ConversationLogStorage
{
    private const string ContractVersion = "1.0";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly string _projectRootPath;
    private readonly string _fileName;
    private readonly string _adapter;

    private ConversationLogStorage(string projectRootPath, string fileName, string adapter)
    {
        _projectRootPath = Path.GetFullPath(projectRootPath);
        _fileName = fileName;
        _adapter = adapter;
    }

    public static ConversationLogStorage ForProjects(string projectRootPath)
    {
        return new ConversationLogStorage(projectRootPath, "projects-active.jsonl", "projects");
    }

    public static ConversationLogStorage ForChats(string projectRootPath)
    {
        return new ConversationLogStorage(projectRootPath, "chats-active.jsonl", "chats");
    }

    public string FilePath => Path.Combine(ZavodLocalStorageLayout.GetConversationsRoot(_projectRootPath), _fileName);

    public bool HasEntries()
    {
        return File.Exists(FilePath) && new FileInfo(FilePath).Length > 0;
    }

    public void Append(ConversationLogSnapshot snapshot, string eventType)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        ZavodLocalStorageLayout.EnsureInitialized(_projectRootPath);
        var record = new PersistedConversationLogEntry(
            ContractVersion,
            snapshot.MessageId,
            snapshot.Timestamp,
            snapshot.Role,
            snapshot.Kind,
            snapshot.Text,
            snapshot.Markdown,
            snapshot.StepId,
            snapshot.Phase,
            snapshot.Attachments.ToArray(),
            snapshot.Source,
            snapshot.Adapter,
            snapshot.IsStreaming,
            snapshot.Metadata is null ? null : new Dictionary<string, string>(snapshot.Metadata, StringComparer.Ordinal),
            eventType);
        var serialized = JsonSerializer.Serialize(record, JsonOptions);
        File.AppendAllText(FilePath, serialized + Environment.NewLine, Encoding.UTF8);
    }

    public void ReplaceAll(IReadOnlyList<ConversationLogSnapshot> snapshots, string eventType = "replace")
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        ZavodLocalStorageLayout.EnsureInitialized(_projectRootPath);

        if (snapshots.Count == 0)
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }

            return;
        }

        var lines = snapshots.Select(snapshot =>
        {
            var record = new PersistedConversationLogEntry(
                ContractVersion,
                snapshot.MessageId,
                snapshot.Timestamp,
                snapshot.Role,
                snapshot.Kind,
                snapshot.Text,
                snapshot.Markdown,
                snapshot.StepId,
                snapshot.Phase,
                snapshot.Attachments.ToArray(),
                snapshot.Source,
                snapshot.Adapter,
                snapshot.IsStreaming,
                snapshot.Metadata is null ? null : new Dictionary<string, string>(snapshot.Metadata, StringComparer.Ordinal),
                eventType);
            return JsonSerializer.Serialize(record, JsonOptions);
        });

        File.WriteAllLines(FilePath, lines, Encoding.UTF8);
    }

    public IReadOnlyList<ConversationLogSnapshot> LoadLatest()
    {
        if (!File.Exists(FilePath))
        {
            return Array.Empty<ConversationLogSnapshot>();
        }

        var orderedIds = new List<string>();
        var latest = new Dictionary<string, ConversationLogSnapshot>(StringComparer.Ordinal);

        foreach (var line in File.ReadLines(FilePath, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            PersistedConversationLogEntry? record;
            try
            {
                record = JsonSerializer.Deserialize<PersistedConversationLogEntry>(line, JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (record is null ||
                !string.Equals(record.Version, ContractVersion, StringComparison.Ordinal) ||
                !string.Equals(record.Adapter, _adapter, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(record.MessageId))
            {
                continue;
            }

            if (!latest.ContainsKey(record.MessageId))
            {
                orderedIds.Add(record.MessageId);
            }

            latest[record.MessageId] = new ConversationLogSnapshot(
                record.MessageId,
                record.Timestamp,
                record.Role ?? string.Empty,
                record.Kind ?? string.Empty,
                record.Text ?? string.Empty,
                record.Markdown,
                record.StepId,
                record.Phase,
                record.Attachments ?? Array.Empty<string>(),
                record.Source ?? _adapter,
                record.Adapter ?? _adapter,
                record.IsStreaming,
                record.Metadata);
        }

        return orderedIds
            .Select(id => latest[id])
            .OrderBy(snapshot => snapshot.Timestamp)
            .ThenBy(snapshot => snapshot.MessageId, StringComparer.Ordinal)
            .ToArray();
    }

    private sealed record PersistedConversationLogEntry(
        string Version,
        string MessageId,
        DateTimeOffset Timestamp,
        string? Role,
        string? Kind,
        string? Text,
        string? Markdown,
        string? StepId,
        string? Phase,
        string[]? Attachments,
        string? Source,
        string? Adapter,
        bool IsStreaming,
        Dictionary<string, string>? Metadata,
        string EventType);
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
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

public sealed record ConversationLogWindowSlice(
    IReadOnlyList<ConversationLogSnapshot> Snapshots,
    int WindowStartSeq,
    int WindowEndSeq,
    int TotalCount);

public sealed class ConversationLogStorage
{
    private const string ContractVersion = "1.0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _projectRootPath;
    private readonly string _filePath;
    private readonly string? _fallbackReadFilePath;
    private readonly string _adapter;

    private ConversationLogStorage(string projectRootPath, string filePath, string adapter, string? fallbackReadFilePath = null)
    {
        _projectRootPath = Path.GetFullPath(projectRootPath);
        _filePath = filePath;
        _fallbackReadFilePath = fallbackReadFilePath;
        _adapter = adapter;
    }

    public static ConversationLogStorage ForProjects(string projectRootPath)
    {
        return new ConversationLogStorage(projectRootPath, GetLegacyFilePath(projectRootPath, "projects-active.jsonl"), "projects");
    }

    public static ConversationLogStorage ForChats(string projectRootPath)
    {
        return new ConversationLogStorage(projectRootPath, GetLegacyFilePath(projectRootPath, "chats-active.jsonl"), "chats");
    }

    public static ConversationLogStorage ForProjectConversation(string projectRootPath, string conversationId, string? fallbackFileName = null)
    {
        return new ConversationLogStorage(
            projectRootPath,
            GetConversationFilePath(projectRootPath, conversationId),
            "projects",
            GetFallbackReadFilePath(projectRootPath, fallbackFileName));
    }

    public static ConversationLogStorage ForChatConversation(string projectRootPath, string conversationId, string? fallbackFileName = null)
    {
        return new ConversationLogStorage(
            projectRootPath,
            GetConversationFilePath(projectRootPath, conversationId),
            "chats",
            GetFallbackReadFilePath(projectRootPath, fallbackFileName));
    }

    public string FilePath => _filePath;

    public bool HasEntries()
    {
        var readFilePath = ResolveReadFilePath();
        return readFilePath is not null && File.Exists(readFilePath) && new FileInfo(readFilePath).Length > 0;
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
        File.AppendAllText(_filePath, serialized + Environment.NewLine, Encoding.UTF8);
    }

    public void UpsertLatest(ConversationLogSnapshot snapshot, string eventType = "replace")
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        var snapshots = LoadLatest().ToList();
        var existingIndex = snapshots.FindIndex(candidate => string.Equals(candidate.MessageId, snapshot.MessageId, StringComparison.Ordinal));
        if (existingIndex >= 0)
        {
            snapshots[existingIndex] = snapshot;
        }
        else
        {
            snapshots.Add(snapshot);
        }

        ReplaceAll(
            snapshots
                .OrderBy(candidate => candidate.Timestamp)
                .ThenBy(candidate => candidate.MessageId, StringComparer.Ordinal)
                .ToArray(),
            eventType);
    }

    public void ReplaceAll(IReadOnlyList<ConversationLogSnapshot> snapshots, string eventType = "replace")
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        ZavodLocalStorageLayout.EnsureInitialized(_projectRootPath);

        if (snapshots.Count == 0)
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
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

        File.WriteAllLines(_filePath, lines, Encoding.UTF8);
    }

    public IReadOnlyList<ConversationLogSnapshot> LoadLatest()
    {
        var readFilePath = ResolveReadFilePath();
        if (readFilePath is null || !File.Exists(readFilePath))
        {
            return Array.Empty<ConversationLogSnapshot>();
        }

        return LoadLatestFromPath(readFilePath);
    }

    public ConversationLogWindowSlice LoadLatestWindow(int take)
    {
        var snapshots = LoadLatest();
        if (take <= 0 || snapshots.Count == 0)
        {
            return new ConversationLogWindowSlice(Array.Empty<ConversationLogSnapshot>(), 0, 0, 0);
        }

        var totalCount = snapshots.Count;
        var window = snapshots.Skip(Math.Max(0, totalCount - take)).ToArray();
        var windowStartSeq = totalCount - window.Length + 1;
        var windowEndSeq = totalCount;
        return new ConversationLogWindowSlice(window, windowStartSeq, windowEndSeq, totalCount);
    }

    public ConversationLogWindowSlice LoadWindowBefore(int beforeSeq, int take)
    {
        var snapshots = LoadLatest();
        if (take <= 0 || beforeSeq <= 1 || snapshots.Count == 0)
        {
            return new ConversationLogWindowSlice(Array.Empty<ConversationLogSnapshot>(), 0, 0, 0);
        }

        var totalCount = snapshots.Count;
        var clampedBeforeSeq = Math.Min(beforeSeq, totalCount + 1);
        var prefixCount = clampedBeforeSeq - 1;
        if (prefixCount <= 0)
        {
            return new ConversationLogWindowSlice(Array.Empty<ConversationLogSnapshot>(), 0, 0, totalCount);
        }

        var window = snapshots
            .Skip(Math.Max(0, prefixCount - take))
            .Take(Math.Min(take, prefixCount))
            .ToArray();
        var windowStartSeq = prefixCount - window.Length + 1;
        var windowEndSeq = prefixCount;
        return new ConversationLogWindowSlice(window, windowStartSeq, windowEndSeq, totalCount);
    }

    private IReadOnlyList<ConversationLogSnapshot> LoadLatestFromPath(string readFilePath)
    {
        var orderedIds = new List<string>();
        var latest = new Dictionary<string, ConversationLogSnapshot>(StringComparer.Ordinal);

        foreach (var line in File.ReadLines(readFilePath, Encoding.UTF8))
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

    private bool TryDeserializeRecord(string? line, out ConversationLogSnapshot? snapshot)
    {
        snapshot = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        PersistedConversationLogEntry? record;
        try
        {
            record = JsonSerializer.Deserialize<PersistedConversationLogEntry>(line, JsonOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        if (record is null ||
            !string.Equals(record.Version, ContractVersion, StringComparison.Ordinal) ||
            !string.Equals(record.Adapter, _adapter, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(record.MessageId))
        {
            return false;
        }

        snapshot = new ConversationLogSnapshot(
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
        return true;
    }

    private string? ResolveReadFilePath()
    {
        if (File.Exists(_filePath))
        {
            return _filePath;
        }

        return _fallbackReadFilePath;
    }

    private static string GetLegacyFilePath(string projectRootPath, string fileName)
    {
        return Path.Combine(ZavodLocalStorageLayout.GetConversationsRoot(Path.GetFullPath(projectRootPath)), fileName);
    }

    private static string GetConversationFilePath(string projectRootPath, string conversationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        return Path.Combine(
            ZavodLocalStorageLayout.GetConversationsRoot(Path.GetFullPath(projectRootPath)),
            $"{conversationId.Trim()}.jsonl");
    }

    private static string? GetFallbackReadFilePath(string projectRootPath, string? fallbackFileName)
    {
        return string.IsNullOrWhiteSpace(fallbackFileName)
            ? null
            : GetLegacyFilePath(projectRootPath, fallbackFileName.Trim());
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

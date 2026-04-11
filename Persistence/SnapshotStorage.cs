using System;
using System.IO;
using System.Text;
using System.Text.Json;
using zavod.Traceing;

namespace zavod.Persistence;

public static class SnapshotStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string Save(string projectRootPath, Snapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (string.IsNullOrWhiteSpace(projectRootPath))
        {
            throw new ZavodPersistenceException("InvalidProjectRoot", "Project root path is required.");
        }

        if (string.IsNullOrWhiteSpace(snapshot.SnapshotId))
        {
            throw new ZavodPersistenceException("InvalidSnapshot", "Snapshot id is required.");
        }

        var normalizedProjectRoot = Path.GetFullPath(projectRootPath);
        var state = ProjectStateStorage.Load(normalizedProjectRoot);
        var snapshotsRoot = Path.Combine(state.Paths.ZavodRoot, "snapshots");
        Directory.CreateDirectory(snapshotsRoot);

        var snapshotFilePath = Path.Combine(snapshotsRoot, $"{snapshot.SnapshotId}.json");
        var serialized = JsonSerializer.Serialize(snapshot, JsonOptions);
        File.WriteAllText(snapshotFilePath, serialized, Encoding.UTF8);
        Console.WriteLine("Snapshot saved to: " + snapshotFilePath);
        return snapshotFilePath;
    }
}

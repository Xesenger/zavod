using System;
using System.IO;
using System.Text;
using System.Text.Json;
using zavod.State;

namespace zavod.Persistence;

public static class ShiftStateStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string Save(string projectRootPath, ShiftState shiftState)
    {
        ArgumentNullException.ThrowIfNull(shiftState);

        if (string.IsNullOrWhiteSpace(projectRootPath))
        {
            throw new ZavodPersistenceException("InvalidProjectRoot", "Project root path is required.");
        }

        if (string.IsNullOrWhiteSpace(shiftState.ShiftId))
        {
            throw new ZavodPersistenceException("InvalidShift", "Shift id is required.");
        }

        var normalizedProjectRoot = Path.GetFullPath(projectRootPath);
        var state = ProjectStateStorage.Load(normalizedProjectRoot);
        var shiftsRoot = Path.Combine(state.Paths.ZavodRoot, "shifts");
        Directory.CreateDirectory(shiftsRoot);

        var filePath = Path.Combine(shiftsRoot, $"{shiftState.ShiftId}.json");
        var serialized = JsonSerializer.Serialize(shiftState, JsonOptions);
        File.WriteAllText(filePath, serialized, Encoding.UTF8);
        return filePath;
    }

    public static ShiftState Load(string projectRootPath, string shiftId)
    {
        if (string.IsNullOrWhiteSpace(projectRootPath))
        {
            throw new ZavodPersistenceException("InvalidProjectRoot", "Project root path is required.");
        }

        if (string.IsNullOrWhiteSpace(shiftId))
        {
            throw new ZavodPersistenceException("InvalidShiftId", "Shift id is required.");
        }

        var normalizedProjectRoot = Path.GetFullPath(projectRootPath);
        var state = ProjectStateStorage.Load(normalizedProjectRoot);
        var filePath = Path.Combine(state.Paths.ZavodRoot, "shifts", $"{shiftId.Trim()}.json");

        if (!File.Exists(filePath))
        {
            throw new ZavodPersistenceException("ShiftNotFound", $"Shift file '{shiftId}' was not found.");
        }

        var serialized = File.ReadAllText(filePath, Encoding.UTF8);
        var shiftState = JsonSerializer.Deserialize<ShiftState>(serialized, JsonOptions);
        if (shiftState is null)
        {
            throw new ZavodPersistenceException("InvalidShift", $"Shift file '{shiftId}' could not be deserialized.");
        }

        return shiftState;
    }
}

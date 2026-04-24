using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace zavod.Workspace;

public sealed record WorkspaceScannerConfig(
    string? ConfigPath,
    IReadOnlyList<string> PrimaryUnits,
    IReadOnlyList<string> IgnoreZones,
    IReadOnlyList<string> VendorZones,
    IReadOnlyList<string> GeneratedPatterns)
{
    public static WorkspaceScannerConfig Load(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var configPath = Path.Combine(workspaceRoot, ".zavod", "scanner", "config.json");
        if (!File.Exists(configPath))
        {
            return Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(configPath));
            var root = document.RootElement;
            return new WorkspaceScannerConfig(
                ".zavod\\scanner\\config.json",
                ReadStringArray(root, "primaryUnits"),
                ReadStringArray(root, "ignoreZones"),
                ReadStringArray(root, "vendorZones"),
                ReadStringArray(root, "generatedPatterns"));
        }
        catch (JsonException)
        {
            return Empty;
        }
        catch (IOException)
        {
            return Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return Empty;
        }
    }

    public bool IsPrimaryUnit(string relativePath)
    {
        return MatchesAnyZone(relativePath, PrimaryUnits);
    }

    public bool IsIgnoredUnit(string relativePath)
    {
        return MatchesAnyZone(relativePath, IgnoreZones);
    }

    public bool IsVendorUnit(string relativePath)
    {
        return MatchesAnyZone(relativePath, VendorZones);
    }

    public bool IsGeneratedFile(string relativePath)
    {
        var normalizedPath = NormalizeZone(relativePath);
        return GeneratedPatterns.Any(pattern => MatchesPattern(normalizedPath, pattern));
    }

    private static WorkspaceScannerConfig Empty { get; } = new(
        null,
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>());

    private static string[] ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return property.EnumerateArray()
            .Where(static item => item.ValueKind == JsonValueKind.String)
            .Select(static item => NormalizeZone(item.GetString() ?? string.Empty))
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool MatchesAnyZone(string relativePath, IReadOnlyList<string> zones)
    {
        var normalizedPath = NormalizeZone(relativePath);
        return zones.Any(zone =>
            string.Equals(normalizedPath, zone, StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith(zone + "\\", StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesPattern(string relativePath, string pattern)
    {
        var normalizedPattern = NormalizeZone(pattern);
        if (!normalizedPattern.Contains('*', StringComparison.Ordinal))
        {
            return string.Equals(relativePath, normalizedPattern, StringComparison.OrdinalIgnoreCase) ||
                   relativePath.EndsWith("\\" + normalizedPattern, StringComparison.OrdinalIgnoreCase);
        }

        var parts = normalizedPattern.Split('*', StringSplitOptions.None);
        var cursor = 0;
        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                continue;
            }

            var index = relativePath.IndexOf(part, cursor, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }

            cursor = index + part.Length;
        }

        return normalizedPattern.StartsWith('*') ||
               (parts.Length > 0 && relativePath.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeZone(string value)
    {
        var normalized = value.Trim().Replace('/', '\\').Trim('\\');
        return string.IsNullOrWhiteSpace(normalized) ? "." : normalized;
    }
}

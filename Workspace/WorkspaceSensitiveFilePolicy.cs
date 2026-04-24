using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace zavod.Workspace;

public static class WorkspaceSensitiveFilePolicy
{
    private static readonly HashSet<string> SensitiveExactFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".env",
        "credentials",
        "credentials.json",
        "secrets.json",
        "id_dsa",
        "id_ecdsa",
        "id_ed25519",
        "id_rsa"
    };

    private static readonly HashSet<string> SensitiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".key",
        ".p12",
        ".pem",
        ".pfx"
    };

    private static readonly HashSet<string> SensitiveConfigExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".env",
        ".ini",
        ".json",
        ".toml",
        ".txt",
        ".yaml",
        ".yml"
    };

    private static readonly HashSet<string> SensitiveNameTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "credential",
        "credentials",
        "secret",
        "secrets",
        "token",
        "tokens"
    };

    public static bool IsSensitivePath(string path)
    {
        return !string.IsNullOrWhiteSpace(GetSensitiveReason(path));
    }

    public static string GetSensitiveReason(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalizedPath = path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var fileName = Path.GetFileName(normalizedPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        if (SensitiveExactFileNames.Contains(fileName) ||
            fileName.StartsWith(".env.", StringComparison.OrdinalIgnoreCase))
        {
            return "sensitive-file-name";
        }

        var extension = Path.GetExtension(fileName);
        if (SensitiveExtensions.Contains(extension))
        {
            return "sensitive-extension";
        }

        if (!SensitiveConfigExtensions.Contains(extension))
        {
            return string.Empty;
        }

        var nameTokens = Path.GetFileNameWithoutExtension(fileName)
            .Split(new[] { '-', '_', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (nameTokens.Any(token => SensitiveNameTokens.Contains(token)))
        {
            return "sensitive-name-token";
        }

        return string.Empty;
    }
}

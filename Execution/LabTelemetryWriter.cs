using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace zavod.Execution;

public sealed class LabTelemetryWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string Write(
        string projectRoot,
        string role,
        string callId,
        object requestEnvelope,
        string rawResponse,
        object? parsedResult,
        object metadata)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return string.Empty;
        }

        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
            var safeCall = string.IsNullOrWhiteSpace(callId) ? "call" : SanitizeSegment(callId);
            var safeRole = string.IsNullOrWhiteSpace(role) ? "role" : SanitizeSegment(role);
            var directory = Path.Combine(
                Path.GetFullPath(projectRoot),
                ".zavod",
                "lab",
                $"{timestamp}-{safeRole}-{safeCall}");
            Directory.CreateDirectory(directory);

            File.WriteAllText(Path.Combine(directory, "request.json"), Serialize(requestEnvelope));
            File.WriteAllText(Path.Combine(directory, "response.txt"), rawResponse ?? string.Empty);
            File.WriteAllText(Path.Combine(directory, "parsed.json"), Serialize(parsedResult));
            File.WriteAllText(Path.Combine(directory, "meta.json"), Serialize(metadata));

            return directory;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string Serialize(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        try
        {
            return JsonSerializer.Serialize(value, JsonOptions);
        }
        catch
        {
            return JsonSerializer.Serialize(new { unserializable = value.ToString() }, JsonOptions);
        }
    }

    private static string SanitizeSegment(string value)
    {
        var trimmed = value.Trim();
        var span = trimmed.AsSpan();
        Span<char> buffer = stackalloc char[Math.Min(span.Length, 64)];
        var length = 0;
        for (var i = 0; i < span.Length && length < buffer.Length; i++)
        {
            var ch = span[i];
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
            {
                buffer[length++] = ch;
            }
            else if (length > 0 && buffer[length - 1] != '-')
            {
                buffer[length++] = '-';
            }
        }

        if (length == 0)
        {
            return "x";
        }

        return new string(buffer[..length]);
    }
}

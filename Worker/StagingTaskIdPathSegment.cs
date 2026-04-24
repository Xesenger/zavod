using System;

namespace zavod.Worker;

internal static class StagingTaskIdPathSegment
{
    public static string Normalize(string taskId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        var trimmed = taskId.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Staging task id cannot be empty.", nameof(taskId));
        }

        foreach (var ch in trimmed)
        {
            var allowed = char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_';
            if (!allowed)
            {
                throw new ArgumentException("Staging task id may contain only ASCII letters, digits, '-' and '_'.", nameof(taskId));
            }
        }

        return trimmed;
    }

    public static bool TryNormalize(string? taskId, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return false;
        }

        try
        {
            normalized = Normalize(taskId);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace zavod.Diagnostics;

internal static class RootCauseTrace
{
    private static readonly Stopwatch Stopwatch = Stopwatch.StartNew();
    private static readonly object Gate = new();
    private static readonly List<string> Pending = new();
    private static string? _path;

    public static void Initialize(string projectRoot)
    {
        ArgumentNullException.ThrowIfNull(projectRoot);

        lock (Gate)
        {
            var artifactsDirectory = Path.Combine(projectRoot, "artifacts");
            Directory.CreateDirectory(artifactsDirectory);
            _path = Path.Combine(artifactsDirectory, "root-cause-trace.log");
            File.WriteAllText(_path, string.Empty);

            foreach (var line in Pending)
            {
                File.AppendAllLines(_path, new[] { line });
            }

            Pending.Clear();
        }
    }

    public static void Mark(string stage, string? detail = null)
    {
        var line = $"[{DateTimeOffset.Now:O}] +{Stopwatch.ElapsedMilliseconds,6}ms | {stage}{(string.IsNullOrWhiteSpace(detail) ? string.Empty : $" | {detail}")}";

        lock (Gate)
        {
            if (string.IsNullOrWhiteSpace(_path))
            {
                Pending.Add(line);
                return;
            }

            File.AppendAllLines(_path, new[] { line });
        }
    }
}

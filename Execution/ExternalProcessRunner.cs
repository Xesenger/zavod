using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace zavod.Execution;

public sealed class ExternalProcessRunner : IExternalProcessRunner
{
    public ExternalProcessResult Run(ExternalProcessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var resolvedFileName = ResolveExecutableForHost(request.FileName)
            ?? throw new System.IO.FileNotFoundException(BuildMissingExecutableMessage(request.FileName));

        var startInfo = new ProcessStartInfo
        {
            FileName = resolvedFileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        if (!string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            startInfo.WorkingDirectory = request.WorkingDirectory;
        }

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit((int)request.Timeout.TotalMilliseconds))
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(1000);
            }
            catch (InvalidOperationException)
            {
            }

            stdout.Append(ReadCompletedOutput(stdoutTask));
            stderr.Append(ReadCompletedOutput(stderrTask));
            return new ExternalProcessResult(-1, stdout.ToString(), stderr.ToString(), TimedOut: true);
        }

        stdout.Append(ReadCompletedOutput(stdoutTask));
        stderr.Append(ReadCompletedOutput(stderrTask));
        return new ExternalProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString(), TimedOut: false);
    }

    internal static string? ResolveExecutableForHost(
        string fileName,
        string? pathEnvironment = null,
        string? pathExtEnvironment = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var trimmed = fileName.Trim();
        if (trimmed.Contains(System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || trimmed.Contains(System.IO.Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            || System.IO.Path.IsPathRooted(trimmed))
        {
            return System.IO.File.Exists(trimmed) ? System.IO.Path.GetFullPath(trimmed) : null;
        }

        var path = pathEnvironment ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var separator = OperatingSystem.IsWindows() ? ';' : ':';
        var candidates = BuildExecutableCandidates(trimmed, pathExtEnvironment);
        foreach (var rawDirectory in path.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var directory = rawDirectory.Trim('"');
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            foreach (var candidateName in candidates)
            {
                var candidatePath = System.IO.Path.Combine(directory, candidateName);
                if (System.IO.File.Exists(candidatePath))
                {
                    return System.IO.Path.GetFullPath(candidatePath);
                }
            }
        }

        return null;
    }

    private static IReadOnlyList<string> BuildExecutableCandidates(string fileName, string? pathExtEnvironment)
    {
        if (!OperatingSystem.IsWindows() || !string.IsNullOrEmpty(System.IO.Path.GetExtension(fileName)))
        {
            return new[] { fileName };
        }

        var pathExt = string.IsNullOrWhiteSpace(pathExtEnvironment)
            ? ".COM;.EXE;.BAT;.CMD"
            : pathExtEnvironment;
        var values = new List<string> { fileName };
        foreach (var extension in pathExt.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = extension.StartsWith(".", StringComparison.Ordinal) ? extension : "." + extension;
            values.Add(fileName + normalized.ToLowerInvariant());
            values.Add(fileName + normalized.ToUpperInvariant());
        }

        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string BuildMissingExecutableMessage(string fileName)
    {
        var executable = string.IsNullOrWhiteSpace(fileName) ? "(empty)" : fileName.Trim();
        return $"Executable `{executable}` was not found on PATH. Install or configure `{executable}` for this host, or choose another scanner run profile.";
    }

    private static string ReadCompletedOutput(Task<string> outputTask)
    {
        try
        {
            return outputTask.Wait(TimeSpan.FromSeconds(1)) ? outputTask.Result : string.Empty;
        }
        catch (AggregateException)
        {
            return string.Empty;
        }
    }
}

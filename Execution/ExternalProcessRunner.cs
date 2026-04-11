using System;
using System.Diagnostics;
using System.Text;

namespace zavod.Execution;

public sealed class ExternalProcessRunner : IExternalProcessRunner
{
    public ExternalProcessResult Run(ExternalProcessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.Start();
        stdout.Append(process.StandardOutput.ReadToEnd());
        stderr.Append(process.StandardError.ReadToEnd());

        if (!process.WaitForExit((int)request.Timeout.TotalMilliseconds))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            return new ExternalProcessResult(-1, stdout.ToString(), stderr.ToString(), TimedOut: true);
        }

        return new ExternalProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString(), TimedOut: false);
    }
}

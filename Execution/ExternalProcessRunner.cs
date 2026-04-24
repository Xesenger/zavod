using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

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

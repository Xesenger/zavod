using System;
using System.ComponentModel;
using System.IO;
using zavod.Workspace;

namespace zavod.Execution;

public sealed class ArchiveInspectionRuntimeService(IExternalProcessRunner? processRunner = null)
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);
    private readonly IExternalProcessRunner _processRunner = processRunner ?? new ExternalProcessRunner();
    private readonly string _archiveBackend = BundledToolLocator.ResolveOrFallback(Path.Combine("tools", "7za.exe"), "7z");

    public MaterialRuntimeResult Prepare(MaterialRuntimeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Kind != WorkspaceMaterialKind.ArchiveArtifact)
        {
            return BuildFailure(request, MaterialRuntimeStatus.UnsupportedKind, "7z", "ARCHIVE_KIND_UNSUPPORTED", "Archive runtime service supports only archive materials.");
        }

        if (string.IsNullOrWhiteSpace(request.FullPath) || !File.Exists(request.FullPath))
        {
            return BuildFailure(request, MaterialRuntimeStatus.MissingFile, "7z", "ARCHIVE_FILE_MISSING", "Archive material path is missing or unavailable.");
        }

        ExternalProcessResult processResult;
        try
        {
            processResult = _processRunner.Run(new ExternalProcessRequest(_archiveBackend, new[] { "l", "-ba", request.FullPath! }, DefaultTimeout, "archive_list"));
        }
        catch (Exception exception) when (exception is Win32Exception or FileNotFoundException)
        {
            return BuildFailure(request, MaterialRuntimeStatus.BackendUnavailable, "7z", "SEVENZIP_UNAVAILABLE", $"7z backend is unavailable: {exception.Message}");
        }

        if (processResult.TimedOut)
        {
            return BuildFailure(request, MaterialRuntimeStatus.Failed, "7z", "ARCHIVE_TIMEOUT", "Archive inspection backend timed out.");
        }

        if (processResult.ExitCode != 0)
        {
            var combined = $"{processResult.StdErr} {processResult.StdOut}".Trim();
            var status = combined.Contains("password", StringComparison.OrdinalIgnoreCase)
                ? MaterialRuntimeStatus.Encrypted
                : combined.Contains("Can not open the file as archive", StringComparison.OrdinalIgnoreCase)
                    ? MaterialRuntimeStatus.Corrupt
                    : MaterialRuntimeStatus.Failed;

            return BuildFailure(request, status, "7z", "ARCHIVE_LIST_FAILED", string.IsNullOrWhiteSpace(combined) ? "Archive inspection failed." : combined);
        }

        var normalized = TextMaterialRuntimeService.NormalizeText(processResult.StdOut);
        var wasTruncated = normalized.Length > request.MaxChars;
        var preview = wasTruncated ? normalized[..request.MaxChars] : normalized;
        var warnings = wasTruncated ? new[] { "archive_listing_truncated" } : Array.Empty<string>();

        return new MaterialRuntimeResult(
            request.DisplayPath,
            request.Kind,
            request.SelectionReason,
            MaterialRuntimeStatus.Prepared,
            "7z",
            false,
            preview,
            wasTruncated,
            warnings,
            null,
            $"Material evidence: path={request.DisplayPath}, backend=7z, status=Prepared, fallback=False, truncated={wasTruncated}.",
            $"Prepared bounded archive listing preview from '{request.DisplayPath}'.").Normalize();
    }

    private static MaterialRuntimeResult BuildFailure(MaterialRuntimeRequest request, MaterialRuntimeStatus status, string backendId, string code, string message)
    {
        return new MaterialRuntimeResult(
            request.DisplayPath,
            request.Kind,
            request.SelectionReason,
            status,
            backendId,
            false,
            string.Empty,
            false,
            Array.Empty<string>(),
            new MaterialRuntimeDiagnostic(code, message),
            $"Material evidence: path={request.DisplayPath}, backend={backendId}, status={status}, fallback=False.",
            message).Normalize();
    }
}

using System;
using System.ComponentModel;
using System.IO;
using zavod.Workspace;

namespace zavod.Execution;

public sealed class PdfExtractionRuntimeService(IExternalProcessRunner? processRunner = null)
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);
    private readonly IExternalProcessRunner _processRunner = processRunner ?? new ExternalProcessRunner();
    private readonly string _pdfToTextBackend = BundledToolLocator.ResolveOrFallback(
        Path.Combine("tools", "pdf-tools", "poppler-24.07.0", "Library", "bin", "pdftotext.exe"),
        "pdftotext");

    public MaterialRuntimeResult Prepare(MaterialRuntimeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Kind != WorkspaceMaterialKind.PdfDocument)
        {
            return BuildFailure(request, MaterialRuntimeStatus.UnsupportedKind, "pdftotext", false, "PDF_KIND_UNSUPPORTED", "PDF runtime service supports only PDF materials.");
        }

        if (string.IsNullOrWhiteSpace(request.FullPath) || !File.Exists(request.FullPath))
        {
            return BuildFailure(request, MaterialRuntimeStatus.MissingFile, "pdftotext", false, "PDF_FILE_MISSING", "PDF material path is missing or unavailable.");
        }

        return ExecutePdfToTextBackend(request);
    }

    private MaterialRuntimeResult ExecutePdfToTextBackend(MaterialRuntimeRequest request, bool fallbackUsed = false)
    {
        const string backendId = "pdftotext";
        ExternalProcessResult processResult;

        try
        {
            processResult = _processRunner.Run(new ExternalProcessRequest(
                _pdfToTextBackend,
                new[] { "-enc", "UTF-8", request.FullPath!, "-" },
                DefaultTimeout,
                $"pdf_extract:{backendId}"));
        }
        catch (Exception exception) when (exception is Win32Exception or FileNotFoundException)
        {
            return BuildFailure(request, MaterialRuntimeStatus.BackendUnavailable, backendId, fallbackUsed, "PDFTOTEXT_UNAVAILABLE", $"PDF text extraction backend is unavailable: {exception.Message}");
        }

        if (processResult.TimedOut)
        {
            return BuildFailure(request, MaterialRuntimeStatus.Failed, backendId, fallbackUsed, "PDF_TIMEOUT", "PDF extraction backend timed out.");
        }

        if (processResult.ExitCode != 0)
        {
            return BuildFailure(
                request,
                ClassifyPdfFailure(processResult.StdErr),
                backendId,
                fallbackUsed,
                "PDF_EXTRACTION_FAILED",
                string.IsNullOrWhiteSpace(processResult.StdErr) ? "PDF extraction backend failed." : processResult.StdErr.Trim());
        }

        var normalizedText = TextMaterialRuntimeService.NormalizeText(processResult.StdOut ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(normalizedText))
        {
            return BuildPrepared(request, backendId, fallbackUsed, normalizedText);
        }

        return new MaterialRuntimeResult(
            request.DisplayPath,
            request.Kind,
            request.SelectionReason,
            MaterialRuntimeStatus.OcrRequired,
            backendId,
            fallbackUsed,
            string.Empty,
            false,
            Array.Empty<string>(),
            new MaterialRuntimeDiagnostic("PDF_OCR_REQUIRED", "No extractable PDF text was found."),
            $"Material evidence: path={request.DisplayPath}, backend={backendId}, status=OcrRequired, fallback={fallbackUsed}.",
            "No extractable PDF text was found.").Normalize();
    }

    private static MaterialRuntimeStatus ClassifyPdfFailure(string message)
    {
        if (message.Contains("password", StringComparison.OrdinalIgnoreCase) || message.Contains("encrypted", StringComparison.OrdinalIgnoreCase))
        {
            return MaterialRuntimeStatus.Encrypted;
        }

        if (message.Contains("corrupt", StringComparison.OrdinalIgnoreCase) || message.Contains("malformed", StringComparison.OrdinalIgnoreCase))
        {
            return MaterialRuntimeStatus.Corrupt;
        }

        return MaterialRuntimeStatus.Failed;
    }

    private static MaterialRuntimeResult BuildPrepared(MaterialRuntimeRequest request, string backendId, bool fallbackUsed, string normalizedText)
    {
        var wasTruncated = normalizedText.Length > request.MaxChars;
        var prepared = wasTruncated ? normalizedText[..request.MaxChars] : normalizedText;
        var warnings = wasTruncated ? new[] { "pdf_text_truncated" } : Array.Empty<string>();
        var summary = fallbackUsed
            ? $"Prepared bounded PDF text preview from '{request.DisplayPath}' using fallback backend."
            : $"Prepared bounded PDF text preview from '{request.DisplayPath}'.";

        return new MaterialRuntimeResult(
            request.DisplayPath,
            request.Kind,
            request.SelectionReason,
            MaterialRuntimeStatus.Prepared,
            backendId,
            fallbackUsed,
            prepared,
            wasTruncated,
            warnings,
            null,
            $"Material evidence: path={request.DisplayPath}, backend={backendId}, status=Prepared, fallback={fallbackUsed}, truncated={wasTruncated}.",
            summary).Normalize();
    }

    private static MaterialRuntimeResult BuildFailure(MaterialRuntimeRequest request, MaterialRuntimeStatus status, string backendId, bool fallbackUsed, string code, string message)
    {
        return new MaterialRuntimeResult(
            request.DisplayPath,
            request.Kind,
            request.SelectionReason,
            status,
            backendId,
            fallbackUsed,
            string.Empty,
            false,
            Array.Empty<string>(),
            new MaterialRuntimeDiagnostic(code, message),
            $"Material evidence: path={request.DisplayPath}, backend={backendId}, status={status}, fallback={fallbackUsed}.",
            message).Normalize();
    }
}

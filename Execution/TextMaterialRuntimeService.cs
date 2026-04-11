using System;
using System.IO;
using zavod.Workspace;

namespace zavod.Execution;

public sealed class TextMaterialRuntimeService
{
    public MaterialRuntimeResult Prepare(MaterialRuntimeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Kind != WorkspaceMaterialKind.TextDocument)
        {
            return BuildFailure(
                request,
                MaterialRuntimeStatus.UnsupportedKind,
                "native-text",
                "TEXT_KIND_UNSUPPORTED",
                "Text runtime service supports only text materials.");
        }

        var rawText = request.InlineText;
        if (string.IsNullOrWhiteSpace(rawText))
        {
            if (string.IsNullOrWhiteSpace(request.FullPath) || !File.Exists(request.FullPath))
            {
                return BuildFailure(
                    request,
                    MaterialRuntimeStatus.MissingFile,
                    "native-text",
                    "TEXT_FILE_MISSING",
                    "Text material path is missing or unavailable.");
            }

            try
            {
                rawText = File.ReadAllText(request.FullPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return BuildFailure(
                    request,
                    MaterialRuntimeStatus.Unreadable,
                    "native-text",
                    "TEXT_READ_FAILED",
                    $"Text material could not be read: {exception.Message}");
            }
        }

        var normalized = NormalizeText(rawText ?? string.Empty);
        var wasTruncated = normalized.Length > request.MaxChars;
        var prepared = wasTruncated ? normalized[..request.MaxChars] : normalized;
        var warnings = wasTruncated ? new[] { "text_truncated" } : Array.Empty<string>();
        var summary = wasTruncated
            ? $"Prepared bounded text preview from '{request.DisplayPath}' with truncation."
            : $"Prepared bounded text preview from '{request.DisplayPath}'.";

        return new MaterialRuntimeResult(
            request.DisplayPath,
            request.Kind,
            request.SelectionReason,
            MaterialRuntimeStatus.Prepared,
            "native-text",
            FallbackUsed: false,
            prepared,
            wasTruncated,
            warnings,
            null,
            $"Material evidence: path={request.DisplayPath}, backend=native-text, status=Prepared, fallback=False, truncated={wasTruncated}.",
            summary).Normalize();
    }

    internal static string NormalizeText(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        return string.Join(
            ' ',
            content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static MaterialRuntimeResult BuildFailure(
        MaterialRuntimeRequest request,
        MaterialRuntimeStatus status,
        string backendId,
        string code,
        string message)
    {
        return new MaterialRuntimeResult(
            request.DisplayPath,
            request.Kind,
            request.SelectionReason,
            status,
            backendId,
            FallbackUsed: false,
            string.Empty,
            WasTruncated: false,
            Array.Empty<string>(),
            new MaterialRuntimeDiagnostic(code, message),
            $"Material evidence: path={request.DisplayPath}, backend={backendId}, status={status}, fallback=False.",
            message).Normalize();
    }
}

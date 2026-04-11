using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using zavod.Workspace;

namespace zavod.Execution;

public sealed class ImageInspectionRuntimeService(IExternalProcessRunner? processRunner = null)
{
    private readonly IExternalProcessRunner? _processRunner = processRunner;

    public MaterialRuntimeResult Prepare(MaterialRuntimeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Kind != WorkspaceMaterialKind.ImageAsset)
        {
            return BuildFailure(request, MaterialRuntimeStatus.UnsupportedKind, "windows-image", "IMAGE_KIND_UNSUPPORTED", "Image runtime service supports only image materials.");
        }

        if (string.IsNullOrWhiteSpace(request.FullPath) || !File.Exists(request.FullPath))
        {
            return BuildFailure(request, MaterialRuntimeStatus.MissingFile, "windows-image", "IMAGE_FILE_MISSING", "Image material path is missing or unavailable.");
        }

        if (_processRunner is not null)
        {
            return PrepareFromRunner(request);
        }

        try
        {
            using var image = Image.FromFile(request.FullPath!);
            var formatName = DetectFormatName(image, request.FullPath!);
            var summary = $"format={formatName}; size={image.Width}x{image.Height}; mode={image.PixelFormat}";

            return new MaterialRuntimeResult(
                request.DisplayPath,
                request.Kind,
                request.SelectionReason,
                MaterialRuntimeStatus.Prepared,
                "windows-image",
                false,
                summary,
                false,
                Array.Empty<string>(),
                null,
                $"Material evidence: path={request.DisplayPath}, backend=windows-image, status=Prepared, fallback=False.",
                $"Prepared bounded image metadata summary from '{request.DisplayPath}'.").Normalize();
        }
        catch (OutOfMemoryException exception)
        {
            return BuildFailure(request, MaterialRuntimeStatus.Corrupt, "windows-image", "IMAGE_FAILED", exception.Message);
        }
        catch (Exception exception)
        {
            return BuildFailure(request, MaterialRuntimeStatus.Failed, "windows-image", "IMAGE_FAILED", exception.Message);
        }
    }

    private MaterialRuntimeResult PrepareFromRunner(MaterialRuntimeRequest request)
    {
        var processResult = _processRunner!.Run(new ExternalProcessRequest("windows-image", new[] { request.FullPath! }, TimeSpan.FromSeconds(20), "image_inspect"));
        if (processResult.TimedOut)
        {
            return BuildFailure(request, MaterialRuntimeStatus.Failed, "windows-image", "IMAGE_TIMEOUT", "Image inspection backend timed out.");
        }

        if (processResult.ExitCode != 0)
        {
            return BuildFailure(
                request,
                MaterialRuntimeStatus.Failed,
                "windows-image",
                "IMAGE_FAILED",
                string.IsNullOrWhiteSpace(processResult.StdErr) ? "Image inspection failed." : processResult.StdErr.Trim());
        }

        var summary = TextMaterialRuntimeService.NormalizeText(processResult.StdOut ?? string.Empty);
        return new MaterialRuntimeResult(
            request.DisplayPath,
            request.Kind,
            request.SelectionReason,
            MaterialRuntimeStatus.Prepared,
            "windows-image",
            false,
            summary,
            false,
            Array.Empty<string>(),
            null,
            $"Material evidence: path={request.DisplayPath}, backend=windows-image, status=Prepared, fallback=False.",
            $"Prepared bounded image metadata summary from '{request.DisplayPath}'.").Normalize();
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

    private static string DetectFormatName(Image image, string fullPath)
    {
        if (image.RawFormat.Equals(ImageFormat.Png))
        {
            return "PNG";
        }

        if (image.RawFormat.Equals(ImageFormat.Jpeg))
        {
            return "JPEG";
        }

        if (image.RawFormat.Equals(ImageFormat.Gif))
        {
            return "GIF";
        }

        if (image.RawFormat.Equals(ImageFormat.Bmp))
        {
            return "BMP";
        }

        if (image.RawFormat.Equals(ImageFormat.Icon))
        {
            return "ICO";
        }

        return Path.GetExtension(fullPath).TrimStart('.').ToUpperInvariant();
    }
}

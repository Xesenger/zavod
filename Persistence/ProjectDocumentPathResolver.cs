using System;
using System.IO;

namespace zavod.Persistence;

public static class ProjectDocumentPathResolver
{
    public static string GetPreviewDocsRoot(string projectRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);
        return Path.Combine(Path.GetFullPath(projectRootPath), ".zavod", "preview_docs");
    }

    public static string GetPreviewProjectPath(string projectRootPath) => Path.Combine(GetPreviewDocsRoot(projectRootPath), "preview_project.md");

    public static string GetPreviewDirectionPath(string projectRootPath) => Path.Combine(GetPreviewDocsRoot(projectRootPath), "preview_direction.md");

    public static string GetPreviewRoadmapPath(string projectRootPath) => Path.Combine(GetPreviewDocsRoot(projectRootPath), "preview_roadmap.md");

    public static string GetPreviewCanonPath(string projectRootPath) => Path.Combine(GetPreviewDocsRoot(projectRootPath), "preview_canon.md");

    public static string GetPreviewCapsulePath(string projectRootPath) => Path.Combine(GetPreviewDocsRoot(projectRootPath), "preview_capsule.md");

    public static string GetImportReportPath(string projectRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);
        return Path.Combine(Path.GetFullPath(projectRootPath), ".zavod", "import_evidence_bundle", "project_report.md");
    }

    public static void EnsurePreviewDocsRoot(string projectRootPath)
    {
        Directory.CreateDirectory(GetPreviewDocsRoot(projectRootPath));
    }
}

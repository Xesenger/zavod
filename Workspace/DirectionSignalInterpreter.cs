using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace zavod.Workspace;

public static class DirectionSignalInterpreter
{
    public static DirectionSignalInterpretation Interpret(WorkspaceImportMaterialInterpreterRunResult runResult)
    {
        ArgumentNullException.ThrowIfNull(runResult);

        var interpretation = runResult.Interpretation;
        var materials = interpretation.Materials ?? Array.Empty<WorkspaceMaterialPreviewInterpretation>();
        var readmeMaterials = materials
            .Where(static material => IsReadmeMaterial(material.RelativePath))
            .Take(3)
            .ToArray();

        if (readmeMaterials.Length == 0)
        {
            return new DirectionSignalInterpretation(
                Array.Empty<DirectionCandidateSignal>(),
                new[]
                {
                    "No README/overview material was imported for direction evidence.",
                    "Contributor-authored direction is required before canonical promotion.",
                    "Evidence that would unblock derivation: README/overview material, explicit direction note, or contributor-authored project direction."
                },
                HasDirectionEvidence: false);
        }

        var candidates = new List<DirectionCandidateSignal>();
        foreach (var material in readmeMaterials)
        {
            candidates.Add(new DirectionCandidateSignal(
                "Imported README/overview material may contain direction evidence; contributor must confirm or rewrite it.",
                material.Confidence == WorkspaceEvidenceConfidenceLevel.Unknown ? WorkspaceEvidenceConfidenceLevel.Likely : material.Confidence,
                $"material `{material.RelativePath}` [{material.Confidence}]"));
        }

        foreach (var entry in (interpretation.EntryPoints ?? Array.Empty<WorkspaceImportMaterialEntryPointInterpretation>()).Take(3))
        {
            candidates.Add(new DirectionCandidateSignal(
                $"Observed entry surface may inform direction: `{entry.RelativePath}`.",
                entry.Confidence == WorkspaceEvidenceConfidenceLevel.Unknown ? WorkspaceEvidenceConfidenceLevel.Likely : entry.Confidence,
                $"entry point `{entry.RelativePath}` [{entry.Confidence}]"));
        }

        foreach (var module in (interpretation.Modules ?? Array.Empty<WorkspaceImportMaterialModuleInterpretation>()).Take(3))
        {
            candidates.Add(new DirectionCandidateSignal(
                $"Observed module surface may inform direction: `{module.Name}`.",
                module.Confidence == WorkspaceEvidenceConfidenceLevel.Unknown ? WorkspaceEvidenceConfidenceLevel.Likely : module.Confidence,
                $"module `{module.Name}` [{module.Confidence}]"));
        }

        return new DirectionSignalInterpretation(
            candidates,
            new[]
            {
                "No contributor-confirmed direction statement exists in preview output.",
                "README-derived intent remains candidate-level until contributor review.",
                "Out-of-direction boundaries are not established."
            },
            HasDirectionEvidence: true);
    }

    private static bool IsReadmeMaterial(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var fileName = Path.GetFileNameWithoutExtension(relativePath);
        return string.Equals(fileName, "README", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record DirectionSignalInterpretation(
    IReadOnlyList<DirectionCandidateSignal> Candidates,
    IReadOnlyList<string> Unknowns,
    bool HasDirectionEvidence);

public sealed record DirectionCandidateSignal(
    string Text,
    WorkspaceEvidenceConfidenceLevel Confidence,
    string Evidence);

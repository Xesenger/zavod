using zavod.Persistence;

namespace zavod.Welcoming;

// Structured input for WelcomeSurfaceSelector per project_welcome_surface_v1.md.
// Caller is responsible for providing honest flags; selector does not probe state itself.
public sealed record WelcomeStateInput(
    ProjectDocumentSourceSelection DocumentSelection,
    bool HasActiveShift,
    bool HasActiveTask,
    bool HasStaleSections,
    bool HasImportFailure);

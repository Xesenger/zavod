namespace zavod.Welcoming;

// Welcome action vocabulary per project_welcome_surface_v1.md.
// New actions require a canon edit; do not extend here ad-hoc.
public enum WelcomeAction
{
    ReviewPreviewDocs = 0,
    PromotePreviewToCanonical = 1,
    AuthorCanonicalDoc = 2,
    StartWorkCycle = 3,
    ContinueWorkCycle = 4,
    ReviewProjectAudit = 5,
    ReviewStaleSections = 6,
    ImportRetry = 7,
    RejectPreview = 8,
    OpenRoadmap = 9,
    OpenDirection = 10
}

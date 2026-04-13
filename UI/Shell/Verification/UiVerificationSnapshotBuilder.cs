using System.Text.Json;
using zavod.Flow;
using zavod.UI.Modes.Projects.Projections;

namespace zavod.UI.Shell.Verification;

internal sealed record UiVerificationSnapshot(
    string AppMode,
    string ProjectsScreen,
    string SurfacePhase,
    string DiscussionSubphase,
    string ExecutionSubphase,
    string ResultSubphase,
    bool ShowChatSurface,
    bool ShowExecutionSurface,
    bool ShowResultSurface,
    bool CanStartIntentValidation,
    bool CanConfirmPreflight,
    bool CanClarifyPreflight,
    bool CanReturnToChatFromPreflight);

internal static class UiVerificationSnapshotBuilder
{
    internal static UiVerificationSnapshot Build(
        string appMode,
        ProjectsScreenProjection projectsScreenProjection,
        ProjectWorkCycleProjection workCycleProjection)
    {
        var phaseState = workCycleProjection.PhaseState;
        var phaseProjection = workCycleProjection.Projection;

        return new UiVerificationSnapshot(
            AppMode: appMode,
            ProjectsScreen: ResolveProjectsScreen(projectsScreenProjection),
            SurfacePhase: phaseState.Phase.ToString(),
            DiscussionSubphase: phaseState.DiscussionSubphase.ToString(),
            ExecutionSubphase: phaseState.ExecutionSubphase.ToString(),
            ResultSubphase: phaseState.ResultSubphase.ToString(),
            ShowChatSurface: phaseProjection.ShowChat,
            ShowExecutionSurface: phaseProjection.ShowExecution,
            ShowResultSurface: phaseProjection.ShowResult,
            CanStartIntentValidation: phaseProjection.CanStartIntentValidation,
            CanConfirmPreflight: phaseProjection.CanConfirmPreflight,
            CanClarifyPreflight: phaseProjection.CanClarifyPreflight,
            CanReturnToChatFromPreflight: phaseState.Phase == SurfacePhase.Execution
                && phaseState.ExecutionSubphase == ExecutionSubphase.Preflight);
    }

    internal static string BuildJson(
        string appMode,
        ProjectsScreenProjection projectsScreenProjection,
        ProjectWorkCycleProjection workCycleProjection)
    {
        return JsonSerializer.Serialize(Build(appMode, projectsScreenProjection, workCycleProjection));
    }

    private static string ResolveProjectsScreen(ProjectsScreenProjection projection)
    {
        if (projection.ShowWorkCycle)
        {
            return "ProjectWorkCycle";
        }

        if (projection.ShowHome)
        {
            return "ProjectHome";
        }

        return "ProjectList";
    }
}

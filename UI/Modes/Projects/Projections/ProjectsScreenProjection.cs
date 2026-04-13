namespace zavod.UI.Modes.Projects.Projections;

public sealed record ProjectsScreenProjection(
    string TitleText,
    string SummaryText,
    bool ShowBack,
    bool ShowList,
    bool ShowHome,
    bool ShowWorkCycle)
{
    public static ProjectsScreenProjection Build(string screenKey)
    {
        return screenKey switch
        {
            "home" => new ProjectsScreenProjection(
                "Project Home",
                "Project Home host is restored as a simple read-only overview layer.",
                ShowBack: true,
                ShowList: false,
                ShowHome: true,
                ShowWorkCycle: false),
            "work_cycle" => new ProjectsScreenProjection(
                "Project Work Cycle",
                "Project Work Cycle host is restored; discussion and preflight semantics are partially reconnected on the proven owner path.",
                ShowBack: true,
                ShowList: false,
                ShowHome: false,
                ShowWorkCycle: true),
            _ => new ProjectsScreenProjection(
                "Project List",
                "Project List host is restored as the non-mutating entry layer for Projects.",
                ShowBack: false,
                ShowList: true,
                ShowHome: false,
                ShowWorkCycle: false)
        };
    }
}

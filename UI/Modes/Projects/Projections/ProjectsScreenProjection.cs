using zavod.UI.Text;

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
                AppText.Current.Get("projects.screen.home.title"),
                AppText.Current.Get("projects.screen.home.summary"),
                ShowBack: true,
                ShowList: false,
                ShowHome: true,
                ShowWorkCycle: false),
            "work_cycle" => new ProjectsScreenProjection(
                AppText.Current.Get("projects.screen.work_cycle.title"),
                AppText.Current.Get("projects.screen.work_cycle.summary"),
                ShowBack: true,
                ShowList: false,
                ShowHome: false,
                ShowWorkCycle: true),
            _ => new ProjectsScreenProjection(
                AppText.Current.Get("projects.screen.list.title"),
                AppText.Current.Get("projects.screen.list.summary"),
                ShowBack: false,
                ShowList: true,
                ShowHome: false,
                ShowWorkCycle: false)
        };
    }
}

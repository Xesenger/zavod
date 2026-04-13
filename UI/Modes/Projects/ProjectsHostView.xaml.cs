using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using zavod.UI.Modes.Projects.Screens;

namespace zavod.UI.Modes.Projects;

public sealed partial class ProjectsHostView : UserControl
{
    public ProjectsHostView()
    {
        InitializeComponent();
    }

    public ProjectListView ListView => ProjectListView;
    public ProjectHomeView HomeView => ProjectHomeView;
    public ProjectWorkCycleView WorkCycleView => ProjectWorkCycleView;

    public void ApplyChrome(string title, string summary)
    {
        ScreenTitleText.Text = title;
        ModeSummaryText.Text = summary;
    }

    public void SetScreenVisibility(bool showList, bool showHome, bool showWorkCycle)
    {
        ProjectListView.Visibility = showList ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        ProjectHomeView.Visibility = showHome ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        ProjectWorkCycleView.Visibility = showWorkCycle ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    }
}

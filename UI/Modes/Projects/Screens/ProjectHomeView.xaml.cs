using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using zavod.UI.Text;

namespace zavod.UI.Modes.Projects.Screens;

public sealed partial class ProjectHomeView : UserControl
{
    public ProjectHomeView()
    {
        InitializeComponent();
        AutomationProperties.SetAutomationId(OpenWorkCycleButton, "Projects.Home.OpenWorkCycle");
        ApplyLocalization();
    }

    public Grid RootView => Root;
    public Button OpenProjectHtmlAction => OpenProjectHtmlButton;
    public Button OpenProjectDocumentAction => OpenProjectDocumentButton;
    public Button OpenWorkCycleAction => OpenWorkCycleButton;

    public void ApplyLocalization()
    {
        var text = AppText.Current;
        TitleText.Text = text.Get("projects.home.title");
        OpenProjectHtmlButton.Content = text.Get("projects.home.open_html");
        OpenProjectDocumentButton.Content = text.Get("projects.home.open_project_doc");
        OpenWorkCycleButton.Content = text.Get("projects.home.open_work_cycle");
        OverviewTitleText.Text = text.Get("projects.home.overview");
        MaterialsTitleText.Text = text.Get("projects.home.materials");
    }

    public void ApplyContent(string summary, string status, string stage, string activity, string materials, string notes)
    {
        SummaryText.Text = summary;
        StatusText.Text = status;
        StageText.Text = stage;
        ActivityText.Text = activity;
        MaterialsText.Text = materials;
        NotesText.Text = notes;
    }

    public void SetActionState(bool hasProjectHtml, string? projectHtmlPath, bool hasProjectDocument, string? projectDocumentPath)
    {
        OpenProjectHtmlButton.IsEnabled = hasProjectHtml;
        OpenProjectHtmlButton.Tag = projectHtmlPath;
        OpenProjectDocumentButton.IsEnabled = hasProjectDocument;
        OpenProjectDocumentButton.Tag = projectDocumentPath;
    }
}

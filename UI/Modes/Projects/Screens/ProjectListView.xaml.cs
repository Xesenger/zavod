using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using zavod.UI.Text;

namespace zavod.UI.Modes.Projects.Screens;

public sealed partial class ProjectListView : UserControl
{
    public ProjectListView()
    {
        InitializeComponent();
        AutomationProperties.SetAutomationId(OpenCurrentProjectButton, "Projects.List.OpenCurrentProject");
        ApplyLocalization();
    }

    public Grid RootView => Root;
    public Button OpenCurrentProjectAction => OpenCurrentProjectButton;

    public void ApplyLocalization()
    {
        var text = AppText.Current;
        TitleText.Text = text.Get("projects.list.title");
        StartHereTitleText.Text = text.Get("projects.list.start_here.title");
        StartHereBodyText.Text = text.Get("projects.list.start_here.body");
        NewProjectButton.Content = text.Get("projects.list.start_here.new_project");
        ImportButton.Content = text.Get("projects.list.start_here.import");
        CurrentProjectTitleText.Text = text.Get("projects.list.current_project.title");
        OpenCurrentProjectButton.Content = text.Get("projects.list.current_project.open");
    }

    public void ApplyContent(string summary, string currentProject, string currentStage, string currentDetails, string notes)
    {
        SummaryText.Text = summary;
        CurrentProjectText.Text = currentProject;
        CurrentStageText.Text = currentStage;
        CurrentDetailsText.Text = currentDetails;
        NotesText.Text = notes;
    }
}

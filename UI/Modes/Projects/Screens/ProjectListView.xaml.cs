using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;

namespace zavod.UI.Modes.Projects.Screens;

public sealed partial class ProjectListView : UserControl
{
    public ProjectListView()
    {
        InitializeComponent();
        AutomationProperties.SetAutomationId(OpenCurrentProjectButton, "Projects.List.OpenCurrentProject");
    }

    public Grid RootView => Root;
    public Button OpenCurrentProjectAction => OpenCurrentProjectButton;

    public void ApplyContent(string summary, string currentProject, string currentStage, string currentDetails, string notes)
    {
        SummaryText.Text = summary;
        CurrentProjectText.Text = currentProject;
        CurrentStageText.Text = currentStage;
        CurrentDetailsText.Text = currentDetails;
        NotesText.Text = notes;
    }
}

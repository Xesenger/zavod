using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace zavod.UI.Shell.Header;

public sealed partial class ModeSwitchView : UserControl
{
    public ModeSwitchView()
    {
        InitializeComponent();
    }

    public Button ChatsButton => ChatsModeButton;

    public Button ProjectsButton => ProjectsModeButton;

    public Border SwitchBorder => ModeSwitchBorder;

    public event RoutedEventHandler? ChatsClicked;

    public event RoutedEventHandler? ProjectsClicked;

    private void ChatsModeButton_Click(object sender, RoutedEventArgs e)
    {
        ChatsClicked?.Invoke(sender, e);
    }

    private void ProjectsModeButton_Click(object sender, RoutedEventArgs e)
    {
        ProjectsClicked?.Invoke(sender, e);
    }
}

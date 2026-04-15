using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using zavod.UI.Text;

namespace zavod.UI.Shell.Header;

public sealed partial class ModeSwitchView : UserControl
{
    public ModeSwitchView()
    {
        InitializeComponent();
        ApplyLocalization();
    }

    public Button ChatsButton => ChatsModeButton;

    public Button ProjectsButton => ProjectsModeButton;

    public Border SwitchBorder => ModeSwitchBorder;

    public event RoutedEventHandler? ChatsClicked;

    public event RoutedEventHandler? ProjectsClicked;

    public void ApplyLocalization()
    {
        var text = AppText.Current;
        ChatsModeButton.Content = text.Get("mode.chats");
        ProjectsModeButton.Content = text.Get("mode.projects");
    }

    private void ChatsModeButton_Click(object sender, RoutedEventArgs e)
    {
        ChatsClicked?.Invoke(sender, e);
    }

    private void ProjectsModeButton_Click(object sender, RoutedEventArgs e)
    {
        ProjectsClicked?.Invoke(sender, e);
    }
}

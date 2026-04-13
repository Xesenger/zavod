using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using zavod.UI.Rendering.Conversation;

namespace zavod.UI.Modes.Chats;

public sealed class ChatsSidebarEntry
{
    public ChatsSidebarEntry(string id, string title)
    {
        Id = id;
        Title = title;
    }

    public string Id { get; }

    public string Title { get; }
}

public sealed partial class ChatsHostView : UserControl
{
    public ChatsHostView()
    {
        InitializeComponent();
        ChatsComposerAttachButton.Content = BuildPlusIcon();
        ChatsComposerSendButton.Content = BuildSendIcon();
        WireChromeButton(ChatsNewChatButton, selected: false);
        WireChromeButton(ChatsComposerAttachButton, selected: false);
        WireChromeButton(ChatsComposerSendButton, selected: false);
    }

    public ConversationView ConversationView => ChatsConversationView;

    public TextBox ComposerTextBox => ChatsComposerTextBox;

    public event RoutedEventHandler? ComposerSendClicked;

    public event RoutedEventHandler? NewChatClicked;

    public event EventHandler<string>? ChatRowClicked;

    public void SetSummaryVisibility(Visibility visibility)
    {
        ChatsSummaryHost.Visibility = visibility;
    }

    public void SetConversationVisible(bool visible)
    {
        ChatsConversationCard.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetSidebarState(bool visible, string title, string meta, string note, double width)
    {
        ChatsSidebarCard.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        ChatsDivider.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        ChatsSidebarColumn.Width = visible ? new GridLength(width) : new GridLength(0);
        ChatsSidebarNoteText.Text = note;
        ChatsSidebarNoteText.Visibility = Visibility.Collapsed;
    }

    public void SetSidebarEntries(IReadOnlyList<ChatsSidebarEntry> entries, string? activeId)
    {
        ChatsListPanel.Children.Clear();

        foreach (var entry in entries)
        {
            var isActive = string.Equals(entry.Id, activeId, StringComparison.Ordinal);
            var button = new Button
            {
                Content = entry.Title,
                Tag = entry.Id,
                Style = (Style)Application.Current.Resources["ChatsSidebarRowButtonStyle"],
                Foreground = isActive
                    ? (Brush)Application.Current.Resources["Ui.Chat.TextPrimaryBrush"]
                    : (Brush)Application.Current.Resources["Ui.Chat.TextSecondaryBrush"],
                Background = isActive
                    ? (Brush)Application.Current.Resources["Ui.Chat.SurfaceActiveBrush"]
                    : new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0))
            };

            AutomationProperties.SetAutomationId(button, $"Chats.Row.{entry.Id}");
            AutomationProperties.SetName(button, entry.Title);
            WireChromeButton(button, isActive);
            button.Click += SidebarEntryButton_Click;
            ChatsListPanel.Children.Add(button);
        }
    }

    public void SetEmptyState(bool visible, string headline, string subtitle)
    {
        ChatsEmptyStatePanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        ChatsEmptyHeadlineText.Text = headline;
        ChatsEmptySubtitleText.Text = subtitle;
    }

    public void SetPersistenceState(string text, bool visible)
    {
        ChatsPersistenceText.Text = text;
        ChatsPersistenceText.Visibility = Visibility.Collapsed;
    }

    public void SetAttachVisible(bool visible)
    {
        ChatsComposerAttachButton.Visibility = Visibility.Visible;
    }

    public void SetComposerPlacement(bool hasConversation)
    {
        Grid.SetRow(ChatsComposerHost, hasConversation ? 2 : 1);
        ChatsComposerHost.VerticalAlignment = hasConversation ? VerticalAlignment.Bottom : VerticalAlignment.Center;
        ChatsComposerHost.Margin = hasConversation
            ? new Thickness(0, 24, 0, 8)
            : new Thickness(0, 0, 0, 120);
    }

    private void ChatsComposerSendButton_Click(object sender, RoutedEventArgs e)
    {
        ComposerSendClicked?.Invoke(sender, e);
    }

    private void ChatsNewChatButton_Click(object sender, RoutedEventArgs e)
    {
        NewChatClicked?.Invoke(sender, e);
    }

    private void SidebarEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string id && !string.IsNullOrWhiteSpace(id))
        {
            ChatRowClicked?.Invoke(this, id);
        }
    }

    private static void WireChromeButton(Button button, bool selected)
    {
        ApplyChromeState(button, selected, pointer: false, focus: false);
        button.PointerEntered += (_, _) => ApplyChromeState(button, selected, pointer: true, focus: false);
        button.PointerExited += (_, _) => ApplyChromeState(button, selected, pointer: false, focus: false);
        button.GotFocus += (_, _) => ApplyChromeState(button, selected, pointer: false, focus: true);
        button.LostFocus += (_, _) => ApplyChromeState(button, selected, pointer: false, focus: false);
        button.PointerPressed += (_, _) => ApplyChromeState(button, selected, pointer: true, focus: true);
        button.PointerReleased += (_, _) => ApplyChromeState(button, selected, pointer: true, focus: false);
    }

    private static void ApplyChromeState(Button button, bool selected, bool pointer, bool focus)
    {
        var transparent = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        var soft = (Brush)Application.Current.Resources["Ui.Chat.SurfaceSoftBrush"];
        var active = (Brush)Application.Current.Resources["Ui.Chat.SurfaceActiveBrush"];
        var strong = (Brush)Application.Current.Resources["Ui.Chat.AccentSoftStrongBrush"];
        var border = (Brush)Application.Current.Resources["Ui.Chat.ChromeBorderBrush"];
        var ink = (Brush)Application.Current.Resources["Ui.Chat.TextPrimaryBrush"];
        var inkSoft = (Brush)Application.Current.Resources["Ui.Chat.TextSecondaryBrush"];

        if (selected)
        {
            button.Background = active;
            button.BorderBrush = transparent;
            button.Foreground = ink;
            ApplyIconBrush(button, ink);
            return;
        }

        if (focus)
        {
            button.Background = strong;
            button.BorderBrush = border;
            button.Foreground = ink;
            ApplyIconBrush(button, ink);
            return;
        }

        if (pointer)
        {
            button.Background = soft;
            button.BorderBrush = border;
            button.Foreground = ink;
            ApplyIconBrush(button, ink);
            return;
        }

        button.Background = transparent;
        button.BorderBrush = transparent;
        button.Foreground = inkSoft;
        ApplyIconBrush(button, inkSoft);
    }

    private static void ApplyIconBrush(Button button, Brush brush)
    {
        if (button.Content is Viewbox { Child: Canvas canvas })
        {
            foreach (var child in canvas.Children)
            {
                if (child is Line line)
                {
                    line.Stroke = brush;
                }
            }
        }
    }

    private static Viewbox BuildPlusIcon()
    {
        return new Viewbox
        {
            Width = 20,
            Height = 20,
            Child = new Canvas
            {
                Width = 24,
                Height = 24,
                Children =
                {
                    CreateLine(12, 7, 12, 17),
                    CreateLine(7, 12, 17, 12)
                }
            }
        };
    }

    private static Viewbox BuildSendIcon()
    {
        return new Viewbox
        {
            Width = 20,
            Height = 20,
            Child = new Canvas
            {
                Width = 24,
                Height = 24,
                Children =
                {
                    CreateLine(12, 17, 12, 7),
                    CreateLine(8, 11, 12, 7),
                    CreateLine(16, 11, 12, 7)
                }
            }
        };
    }

    private static Line CreateLine(double x1, double y1, double x2, double y2)
    {
        return new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = (Brush)Application.Current.Resources["Ui.Chat.TextSecondaryBrush"],
            StrokeThickness = 1,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
    }
}

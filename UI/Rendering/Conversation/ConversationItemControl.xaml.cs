using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.ComponentModel;
using zavod.UI.Rendering.Markdown;

namespace zavod.UI.Rendering.Conversation;

public sealed partial class ConversationItemControl : UserControl
{
    private enum ConversationPresentationVariant
    {
        UserBubble,
        ChatLightDocument,
        ProjectFullDocument
    }

    private readonly BlockRendererRegistry _registry = new();
    private ConversationItemViewModel? _currentItem;
    private bool _lastRenderedIsUser;
    private IReadOnlyList<MarkdownBlock>? _lastRenderedBlocks;
    private string? _lastRenderedText;
    private IReadOnlyList<ConversationMetadataAction>? _lastRenderedMetadataActions;
    private bool _lastRenderedProjectActionsState;

    public ConversationItemControl()
    {
        InitializeComponent();
        DataContextChanged += ConversationItemControl_DataContextChanged;
    }

    private void ConversationItemControl_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (_currentItem is not null)
        {
            _currentItem.PropertyChanged -= CurrentItem_PropertyChanged;
        }

        _currentItem = args.NewValue as ConversationItemViewModel;
        if (_currentItem is null)
        {
            return;
        }

        _currentItem.PropertyChanged += CurrentItem_PropertyChanged;
        ApplyItem(_currentItem);
    }

    private void CurrentItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_currentItem is null)
        {
            return;
        }

        if (e.PropertyName is nameof(ConversationItemViewModel.Blocks)
            or nameof(ConversationItemViewModel.MetadataActions))
        {
            DispatcherQueue.TryEnqueue(() => ApplyItem(_currentItem));
        }
    }

    private void ApplyItem(ConversationItemViewModel item)
    {
        var variant = DeterminePresentationVariant(item);
        var typographyMode = DetermineTypographyMode(item);
        var isUser = variant == ConversationPresentationVariant.UserBubble;
        var isProjectDocument = variant == ConversationPresentationVariant.ProjectFullDocument;
        var showProjectChrome = false;
        var metadataText = ConversationMetadataPresenter.BuildSummary(item.Metadata);

        UserBubbleBorder.Visibility = isUser ? Visibility.Visible : Visibility.Collapsed;
        DocumentSurfaceBorder.Visibility = isUser ? Visibility.Collapsed : Visibility.Visible;
        ApplyTypography(typographyMode);
        ApplyUserBubbleTone(item);

        if (isUser)
        {
            UpdateMessageContent(item, isUser: true, typographyMode);
            UserAuthorTextBlock.Visibility = Visibility.Collapsed;
            DocumentMetadataSection.Visibility = Visibility.Collapsed;
            DocumentActionsSection.Visibility = Visibility.Collapsed;
            return;
        }

        DocumentSurfaceBorder.Style = (Style)Resources[
            isProjectDocument
                ? "ProjectFullDocumentSurfaceStyle"
                : "ChatLightDocumentSurfaceStyle"];
        DocumentSurfaceBorder.HorizontalAlignment = isProjectDocument
            ? HorizontalAlignment.Stretch
            : HorizontalAlignment.Center;
        DocumentAuthorTextBlock.Text = item.AuthorLabel;
        DocumentHeaderSection.Visibility = isProjectDocument && showProjectChrome ? Visibility.Visible : Visibility.Collapsed;
        DocumentAuthorTextBlock.Visibility = isProjectDocument && showProjectChrome ? Visibility.Visible : Visibility.Collapsed;
        DocumentHeaderDivider.Visibility = isProjectDocument && showProjectChrome ? Visibility.Visible : Visibility.Collapsed;
        DocumentContentSection.Margin = isProjectDocument && showProjectChrome
            ? new Thickness(0, 16, 0, 0)
            : new Thickness(0);
        UpdateMessageContent(item, isUser: false, typographyMode);
        DocumentMetadataTextBlock.Text = metadataText;
        DocumentMetadataSection.Visibility = !isProjectDocument || !showProjectChrome || string.IsNullOrWhiteSpace(metadataText)
            ? Visibility.Collapsed
            : Visibility.Visible;

        RenderMetadataActions(item, isProjectDocument && showProjectChrome);
    }

    private void RenderMetadataActions(ConversationItemViewModel item, bool isProjectDocument)
    {
        if (ReferenceEquals(_lastRenderedMetadataActions, item.MetadataActions)
            && _lastRenderedProjectActionsState == isProjectDocument)
        {
            return;
        }

        DocumentMetadataActionsPanel.Children.Clear();

        if (!isProjectDocument || !ConversationActionVisibility.ShouldShow(item))
        {
            DocumentActionsSection.Visibility = Visibility.Collapsed;
            _lastRenderedMetadataActions = item.MetadataActions;
            _lastRenderedProjectActionsState = isProjectDocument;
            return;
        }

        foreach (var action in item.MetadataActions)
        {
            var button = new Button
            {
                Content = action.Label,
                IsEnabled = action.IsEnabled,
                Style = action.IsPrimary
                    ? (Style)Application.Current.Resources["AccentButtonStyle"]
                    : (Style)Resources["ConversationSecondaryActionButtonStyle"]
            };

            button.Click += async (_, _) => await action.InvokeAsync(item);
            DocumentMetadataActionsPanel.Children.Add(button);
        }

        DocumentActionsSection.Visibility = Visibility.Visible;
        _lastRenderedMetadataActions = item.MetadataActions;
        _lastRenderedProjectActionsState = isProjectDocument;
    }

    private UIElement BuildMessageContent(
        ConversationItemViewModel item,
        BlockRendererRegistry.ConversationTypographyMode typographyMode)
    {
        if (item.IsStreaming && string.IsNullOrWhiteSpace(item.Text) && item.Blocks.Count == 0)
        {
            return BuildStreamingPlaceholder(typographyMode);
        }

        if (IsProjectDocument(item))
        {
            return BuildPlainProjectText(item.Text, typographyMode);
        }

        return _registry.Render(
            item.Blocks.Count > 0 ? item.Blocks : new MarkdownBlock[] { new ParagraphBlock(new[] { item.Text }) },
            typographyMode);
    }

    private void UpdateMessageContent(
        ConversationItemViewModel item,
        bool isUser,
        BlockRendererRegistry.ConversationTypographyMode typographyMode)
    {
        var targetChanged = _lastRenderedIsUser != isUser;
        var blockReferenceChanged = !ReferenceEquals(_lastRenderedBlocks, item.Blocks);
        var fallbackTextChanged = !string.Equals(_lastRenderedText, item.Text, System.StringComparison.Ordinal);

        if (!targetChanged && !blockReferenceChanged && !(item.Blocks.Count == 0 && fallbackTextChanged))
        {
            return;
        }

        var content = BuildMessageContent(item, typographyMode);
        if (isUser)
        {
            UserMessageContentPresenter.Content = content;
            ApplyForegroundRecursive(content, GetUserBubbleTextBrush(_currentItem));
            ApplyTypographyRecursive(
                content,
                GetFontFamily("Ui.FontFamily.Chats.Regular"),
                GetDouble("Ui.Typography.Chats.UserBubble.FontSize"),
                GetDouble("Ui.Typography.Chats.UserBubble.LineHeight"));
        }
        else
        {
            DocumentMessageContentPresenter.Content = content;
        }

        _lastRenderedIsUser = isUser;
        _lastRenderedBlocks = item.Blocks;
        _lastRenderedText = item.Text;
    }

    private void ApplyTypography(BlockRendererRegistry.ConversationTypographyMode typographyMode)
    {
        var chromeRegularFont = GetFontFamily(typographyMode == BlockRendererRegistry.ConversationTypographyMode.Chats
            ? "Ui.FontFamily.Chrome.Regular"
            : "Ui.FontFamily.Projects.Regular");
        var chromeSemiBoldFont = GetFontFamily(typographyMode == BlockRendererRegistry.ConversationTypographyMode.Chats
            ? "Ui.FontFamily.Chrome.SemiBold"
            : "Ui.FontFamily.Projects.SemiBold");
        var bodyFontSize = GetDouble(typographyMode == BlockRendererRegistry.ConversationTypographyMode.Chats
            ? "Ui.Typography.Chats.FontSize"
            : "Ui.Typography.Projects.FontSize");

        UserAuthorTextBlock.FontFamily = chromeSemiBoldFont;
        UserAuthorTextBlock.FontSize = 12;
        DocumentAuthorTextBlock.FontFamily = chromeSemiBoldFont;
        DocumentAuthorTextBlock.Foreground = GetBrush(typographyMode == BlockRendererRegistry.ConversationTypographyMode.Chats
            ? "Ui.Chat.TextSecondaryBrush"
            : "Ui.Project.TextSecondaryBrush");
        DocumentMetadataTextBlock.FontFamily = chromeRegularFont;
        DocumentMetadataTextBlock.FontSize = typographyMode == BlockRendererRegistry.ConversationTypographyMode.Chats ? 12.5 : 11.5;
        DocumentMetadataTextBlock.Foreground = GetBrush(typographyMode == BlockRendererRegistry.ConversationTypographyMode.Chats
            ? "Ui.Chat.TextSecondaryBrush"
            : "Ui.Project.TextSecondaryBrush");
    }

    private static UIElement BuildStreamingPlaceholder(BlockRendererRegistry.ConversationTypographyMode typographyMode)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };

        panel.Children.Add(new ProgressRing
        {
            IsActive = true,
            Width = 16,
            Height = 16
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Generating response...",
            FontFamily = GetFontFamily(typographyMode == BlockRendererRegistry.ConversationTypographyMode.Chats
                ? "Ui.FontFamily.Chats.Regular"
                : "Ui.FontFamily.Projects.Regular"),
            FontSize = GetDouble(typographyMode == BlockRendererRegistry.ConversationTypographyMode.Chats
                ? "Ui.Typography.Chats.FontSize"
                : "Ui.Typography.Projects.FontSize"),
            Opacity = 0.62,
            VerticalAlignment = VerticalAlignment.Center
        });

        return panel;
    }

    private static UIElement BuildPlainProjectText(string text, BlockRendererRegistry.ConversationTypographyMode typographyMode)
    {
        return new TextBlock
        {
            Text = text ?? string.Empty,
            TextWrapping = TextWrapping.WrapWholeWords,
            FontFamily = GetFontFamily(typographyMode == BlockRendererRegistry.ConversationTypographyMode.Chats
                ? "Ui.FontFamily.Chats.Regular"
                : "Ui.FontFamily.Projects.Regular"),
            FontSize = GetDouble(typographyMode == BlockRendererRegistry.ConversationTypographyMode.Chats
                ? "Ui.Typography.Chats.FontSize"
                : "Ui.Typography.Projects.FontSize"),
            Foreground = GetBrush(typographyMode == BlockRendererRegistry.ConversationTypographyMode.Chats
                ? "Ui.Chat.TextPrimaryBrush"
                : "Ui.Project.TextPrimaryBrush"),
            LineHeight = typographyMode == BlockRendererRegistry.ConversationTypographyMode.Chats
                ? (double)Application.Current.Resources["Ui.Typography.Chats.LineHeight"]
                : (double)Application.Current.Resources["Ui.Typography.Projects.LineHeight"]
        };
    }

    private static ConversationPresentationVariant DeterminePresentationVariant(ConversationItemViewModel item)
    {
        if (item.Kind == ConversationItemKind.User)
        {
            return ConversationPresentationVariant.UserBubble;
        }

        return IsProjectDocument(item)
            ? ConversationPresentationVariant.ProjectFullDocument
            : ConversationPresentationVariant.ChatLightDocument;
    }

    private static BlockRendererRegistry.ConversationTypographyMode DetermineTypographyMode(ConversationItemViewModel item)
    {
        return IsProjectDocument(item)
            ? BlockRendererRegistry.ConversationTypographyMode.Projects
            : BlockRendererRegistry.ConversationTypographyMode.Chats;
    }

    private static bool IsProjectDocument(ConversationItemViewModel item)
    {
        return item.Metadata is not null
            && item.Metadata.TryGetValue("mode", out var mode)
            && string.Equals(mode, "project", System.StringComparison.OrdinalIgnoreCase);
    }

        private void ApplyUserBubbleTone(ConversationItemViewModel item)
        {
            var isProjectUser = item.Kind == ConversationItemKind.User && IsProjectDocument(item);
            UserBubbleBorder.Background = GetBrush(isProjectUser
                ? "Ui.Project.UserBubbleBackgroundBrush"
                : "Ui.Chat.UserBubbleBackgroundBrush");
            UserBubbleBorder.BorderBrush = GetBrush(isProjectUser
                ? "Ui.Project.UserBubbleBorderBrush"
                : "Ui.Chat.UserBubbleBorderBrush");
            UserBubbleBorder.BorderThickness = new Thickness(0);
        }

    private static Brush GetUserBubbleTextBrush(ConversationItemViewModel? item)
    {
        var isProjectUser = item is not null
            && item.Kind == ConversationItemKind.User
            && IsProjectDocument(item);
        return GetBrush(isProjectUser
            ? "Ui.Project.UserBubbleTextBrush"
            : "Ui.Chat.UserBubbleTextBrush");
    }

    private static void ApplyForegroundRecursive(object content, Brush foreground)
    {
        switch (content)
        {
            case RichTextBlock richText:
                richText.Foreground = foreground;
                break;
            case TextBlock textBlock:
                textBlock.Foreground = foreground;
                break;
            case Panel panel:
                foreach (var child in panel.Children)
                {
                    ApplyForegroundRecursive(child, foreground);
                }
                break;
            case Border border when border.Child is not null:
                ApplyForegroundRecursive(border.Child, foreground);
                break;
            case ContentPresenter presenter when presenter.Content is not null:
                ApplyForegroundRecursive(presenter.Content, foreground);
                break;
        }
    }

    private static void ApplyTypographyRecursive(object content, FontFamily fontFamily, double fontSize, double lineHeight)
    {
        switch (content)
        {
            case RichTextBlock richText:
                richText.FontFamily = fontFamily;
                richText.FontSize = fontSize;
                foreach (var block in richText.Blocks)
                {
                    if (block is Paragraph paragraph)
                    {
                        paragraph.LineHeight = lineHeight;
                    }
                }
                break;
            case TextBlock textBlock:
                textBlock.FontFamily = fontFamily;
                textBlock.FontSize = fontSize;
                textBlock.LineHeight = lineHeight;
                break;
            case Panel panel:
                foreach (var child in panel.Children)
                {
                    ApplyTypographyRecursive(child, fontFamily, fontSize, lineHeight);
                }
                break;
            case Border border when border.Child is not null:
                ApplyTypographyRecursive(border.Child, fontFamily, fontSize, lineHeight);
                break;
            case ContentPresenter presenter when presenter.Content is not null:
                ApplyTypographyRecursive(presenter.Content, fontFamily, fontSize, lineHeight);
                break;
        }
    }

    private static FontFamily GetFontFamily(string resourceKey)
    {
        return (FontFamily)Application.Current.Resources[resourceKey];
    }

    private static Brush GetBrush(string resourceKey)
    {
        return (Brush)Application.Current.Resources[resourceKey];
    }

    private static double GetDouble(string resourceKey)
    {
        return (double)Application.Current.Resources[resourceKey];
    }
}

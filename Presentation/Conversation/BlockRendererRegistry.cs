using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Windows.UI.Text;
using zavod.Presentation.Conversation.Markdown;

namespace zavod.Presentation.Conversation;

public sealed class BlockRendererRegistry
{
    public enum ConversationTypographyMode
    {
        Chats,
        Projects
    }

    private const double SpaceXs = 4;
    private const double SpaceSm = 10;
    private const double SpaceMd = 14;
    private const double SpaceLg = 18;
    private const double SpaceXl = 28;
    private const double RadiusSm = 10;
    private const double RadiusMd = 14;

    public UIElement Render(
        IReadOnlyList<MarkdownBlock> blocks,
        ConversationTypographyMode typographyMode = ConversationTypographyMode.Chats)
    {
        var panel = new StackPanel { Spacing = GetBlockSpacing(typographyMode) };
        foreach (var block in blocks)
        {
            panel.Children.Add(RenderBlock(block, typographyMode));
        }

        return panel;
    }

    private UIElement RenderBlock(MarkdownBlock block, ConversationTypographyMode typographyMode)
    {
        return block switch
        {
            ParagraphBlock paragraph => BuildParagraph(paragraph.Text, typographyMode),
            HeadingBlock heading => BuildHeading(heading, typographyMode),
            QuoteBlock quote => BuildQuote(quote, typographyMode),
            ListBlock list => BuildList(list, typographyMode),
            CodeBlock code => BuildCode(code, typographyMode),
            FormulaBlock formula => BuildFormula(formula, typographyMode),
            DividerBlock => BuildDivider(),
            ImageBlock image => BuildImage(image, typographyMode),
            FootnoteBlock footnote => BuildFootnote(footnote, typographyMode),
            ErrorBlock error => BuildError(error, typographyMode),
            TableBlock table => BuildTable(table, typographyMode),
            _ => BuildParagraph(block.ToString() ?? string.Empty, typographyMode)
        };
    }

    private UIElement BuildHeading(HeadingBlock block, ConversationTypographyMode typographyMode)
    {
        var fontSize = GetHeadingFontSize(block.Level, typographyMode);

        var richText = BuildRichText(
            block.Text,
            typographyMode,
            fontSize: fontSize,
            lineHeight: GetHeadingLineHeight(block.Level, typographyMode));
        richText.FontSize = fontSize;
        richText.FontWeight = FontWeights.SemiBold;
        richText.FontFamily = typographyMode == ConversationTypographyMode.Chats
            ? GetFontFamily("Ui.FontFamily.Chats.SemiBold")
            : GetFontFamily("Ui.FontFamily.Projects.SemiBold");
        richText.Margin = GetHeadingMargin(block.Level, typographyMode);
        return richText;
    }

    private UIElement BuildParagraph(string text, ConversationTypographyMode typographyMode)
    {
        var richText = BuildRichText(
            text,
            typographyMode,
            fontSize: GetBodyFontSize(typographyMode),
            lineHeight: GetBodyLineHeight(typographyMode));
        richText.Margin = new Thickness(0);
        return richText;
    }

    private UIElement BuildQuote(QuoteBlock block, ConversationTypographyMode typographyMode)
    {
        var content = new StackPanel { Spacing = typographyMode == ConversationTypographyMode.Chats ? 6 : 5 };
        foreach (var line in block.Lines)
        {
            var text = BuildRichText(
                line.Text,
                typographyMode,
                fontSize: GetBodyFontSize(typographyMode),
                lineHeight: GetBodyLineHeight(typographyMode));
            text.Margin = new Thickness((line.Depth - 1) * (typographyMode == ConversationTypographyMode.Chats ? 18 : 16), 0, 0, 0);
            text.Opacity = Math.Max(0.5, 0.82 - ((line.Depth - 1) * 0.14));
            content.Children.Add(text);
        }

        return new Border
        {
            Margin = new Thickness(0, typographyMode == ConversationTypographyMode.Chats ? 4 : 2, 0, typographyMode == ConversationTypographyMode.Chats ? 4 : 2),
            Padding = typographyMode == ConversationTypographyMode.Chats
                ? new Thickness(14, 4, 0, 4)
                : new Thickness(12, 4, 0, 4),
            CornerRadius = new CornerRadius(0),
            BorderThickness = new Thickness(1, 0, 0, 0),
            BorderBrush = GetModeBrush("AccentLineBrush", typographyMode),
            Child = content
        };
    }

    private UIElement BuildList(ListBlock block, ConversationTypographyMode typographyMode)
    {
        var panel = new StackPanel
        {
            Spacing = typographyMode == ConversationTypographyMode.Chats ? 10 : 8,
            Margin = new Thickness(0, typographyMode == ConversationTypographyMode.Chats ? 4 : 2, 0, typographyMode == ConversationTypographyMode.Chats ? 4 : 2)
        };
        var orderedCountersByDepth = new Dictionary<int, int>();
        for (var i = 0; i < block.Items.Count; i++)
        {
            var item = block.Items[i];
            IncrementOrderedCounters(item, orderedCountersByDepth);

            var row = new Grid
            {
                ColumnSpacing = typographyMode == ConversationTypographyMode.Chats ? 12 : 10,
                Margin = new Thickness(
                    GetListIndent(item.Depth, typographyMode),
                    0,
                    0,
                    0)
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            row.Children.Add(BuildListMarkerElement(item, orderedCountersByDepth, typographyMode));

            var content = BuildRichText(
                item.Text,
                typographyMode,
                fontSize: GetBodyFontSize(typographyMode),
                lineHeight: GetBodyLineHeight(typographyMode));
            if (item.IsChecked == true)
            {
                content.Opacity = 0.78;
            }

            Grid.SetColumn(content, 1);
            row.Children.Add(content);
            panel.Children.Add(row);
        }

        return panel;
    }

    private UIElement BuildCode(CodeBlock block, ConversationTypographyMode typographyMode)
    {
        var content = new StackPanel { Spacing = 10 };
        if (!string.IsNullOrWhiteSpace(block.Language))
        {
            content.Children.Add(new TextBlock
            {
                Text = block.Language.ToUpperInvariant(),
                Foreground = GetModeBrush("TextSecondaryBrush", typographyMode),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                CharacterSpacing = 24,
                Opacity = 0.6
            });
        }

        content.Children.Add(new TextBlock
        {
            Text = block.Code,
            TextWrapping = TextWrapping.Wrap,
            Foreground = GetModeBrush("TextPrimaryBrush", typographyMode),
            FontSize = 14,
            LineHeight = 23,
            FontFamily = new FontFamily("Consolas")
        });

        return new Border
        {
            Margin = new Thickness(0, typographyMode == ConversationTypographyMode.Chats ? SpaceMd : SpaceSm, 0, typographyMode == ConversationTypographyMode.Chats ? SpaceMd : SpaceSm),
            Padding = new Thickness(18, 16, 18, 16),
            CornerRadius = new CornerRadius(RadiusMd),
            Background = GetModeBrush("SurfaceCodeBrush", typographyMode),
            BorderBrush = GetModeBrush("BorderQuietBrush", typographyMode),
            BorderThickness = new Thickness(1),
            Child = content
        };
    }

    private UIElement BuildDivider()
    {
        return new Border
        {
            Margin = new Thickness(0, SpaceLg, 0, SpaceLg),
            Child = new Rectangle
            {
                Height = 1,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Fill = GetBrush("Ui.BorderSubtleBrush"),
                Opacity = 0.48
            }
        };
    }

    private UIElement BuildImage(ImageBlock block, ConversationTypographyMode typographyMode)
    {
        var panel = new StackPanel
        {
            Spacing = 8
        };

        var fallback = new Border
        {
            Padding = new Thickness(14, 12, 14, 12),
            CornerRadius = new CornerRadius(10),
            BorderBrush = GetModeBrush("BorderQuietBrush", typographyMode),
            BorderThickness = new Thickness(1),
            Background = GetModeBrush("SurfaceSubtleBrush", typographyMode)
        };

        var fallbackContent = new StackPanel { Spacing = 4 };
        fallbackContent.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(block.AltText) ? "Image" : block.AltText,
            Foreground = GetModeBrush("TextPrimaryBrush", typographyMode),
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.WrapWholeWords
        });
        fallbackContent.Children.Add(new TextBlock
        {
            Text = block.Url,
            Foreground = GetModeBrush("TextSecondaryBrush", typographyMode),
            Opacity = 0.68,
            TextWrapping = TextWrapping.WrapWholeWords
        });
        fallback.Child = fallbackContent;

        if (Uri.TryCreate(block.Url, UriKind.Absolute, out var uri))
        {
            var image = new Image
            {
                Stretch = Stretch.Uniform,
                MaxHeight = 360,
                HorizontalAlignment = HorizontalAlignment.Left,
                Visibility = Visibility.Collapsed
            };

            image.ImageOpened += (_, _) =>
            {
                image.Visibility = Visibility.Visible;
                fallback.Visibility = Visibility.Collapsed;
            };

            image.ImageFailed += (_, _) =>
            {
                image.Visibility = Visibility.Collapsed;
                fallback.Visibility = Visibility.Visible;
            };

            image.Source = new BitmapImage(uri);
            panel.Children.Add(image);
        }

        panel.Children.Add(fallback);

        return new Border
        {
            Margin = new Thickness(0, 6, 0, 6),
            Child = panel
        };
    }

    private UIElement BuildFormula(FormulaBlock block, ConversationTypographyMode typographyMode)
    {
        var content = new StackPanel { Spacing = 6 };
        content.Children.Add(new TextBlock
        {
            Text = "MATH",
            Foreground = GetModeBrush("TextSecondaryBrush", typographyMode),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            CharacterSpacing = 24,
            Opacity = 0.62
        });
        content.Children.Add(new TextBlock
        {
            Text = block.Formula,
            TextWrapping = TextWrapping.WrapWholeWords,
            Foreground = GetModeBrush("TextPrimaryBrush", typographyMode),
            FontFamily = new FontFamily("Consolas"),
            FontSize = block.IsBlock ? 15 : 14,
            LineHeight = block.IsBlock ? 24 : 22
        });

        return new Border
        {
            Margin = new Thickness(0, typographyMode == ConversationTypographyMode.Chats ? 8 : 6, 0, typographyMode == ConversationTypographyMode.Chats ? 8 : 6),
            Padding = new Thickness(16, 14, 16, 14),
            CornerRadius = new CornerRadius(RadiusMd),
            Background = GetModeBrush("SurfaceQuoteBrush", typographyMode),
            BorderBrush = GetModeBrush("BorderQuietBrush", typographyMode),
            BorderThickness = new Thickness(1),
            Child = content
        };
    }

    private UIElement BuildFootnote(FootnoteBlock block, ConversationTypographyMode typographyMode)
    {
        var border = new Border
        {
            Margin = new Thickness(0, typographyMode == ConversationTypographyMode.Chats ? 6 : 4, 0, 2),
            Padding = new Thickness(12, 10, 12, 10),
            CornerRadius = new CornerRadius(8),
            Background = GetModeBrush("SurfaceSubtleBrush", typographyMode)
        };

        var content = new StackPanel { Spacing = 4 };
        content.Children.Add(new TextBlock
        {
            Text = $"[^{block.Label}]",
            FontFamily = GetFontFamily(typographyMode == ConversationTypographyMode.Chats
                ? "Ui.FontFamily.Chats.SemiBold"
                : "Ui.FontFamily.Projects.SemiBold"),
            Foreground = GetModeBrush("TextSecondaryBrush", typographyMode),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Opacity = 0.72
        });
        content.Children.Add(BuildRichText(block.Text, typographyMode, fontSize: 13, lineHeight: 20));
        border.Child = content;
        return border;
    }

    private UIElement BuildError(ErrorBlock block, ConversationTypographyMode typographyMode)
    {
        var container = new Border
        {
            Margin = new Thickness(0, typographyMode == ConversationTypographyMode.Chats ? SpaceMd : SpaceSm, 0, typographyMode == ConversationTypographyMode.Chats ? SpaceMd : SpaceSm),
            Padding = new Thickness(SpaceLg, SpaceMd, SpaceLg, SpaceMd),
            CornerRadius = new CornerRadius(RadiusMd),
            Background = GetBrush("Ui.SurfaceErrorBrush"),
            BorderBrush = GetBrush("Ui.ErrorBorderBrush"),
            BorderThickness = new Thickness(1)
        };

        var content = new StackPanel { Spacing = SpaceSm };
        content.Children.Add(new TextBlock
        {
            Text = block.Title,
            FontFamily = GetFontFamily(typographyMode == ConversationTypographyMode.Chats
                ? "Ui.FontFamily.Chats.SemiBold"
                : "Ui.FontFamily.Projects.SemiBold"),
            Foreground = GetModeBrush("TextPrimaryBrush", typographyMode),
            FontSize = 15,
            FontWeight = FontWeights.SemiBold
        });
        content.Children.Add(new TextBlock
        {
            Text = block.Message,
            FontFamily = GetFontFamily(typographyMode == ConversationTypographyMode.Chats
                ? "Ui.FontFamily.Chats.Regular"
                : "Ui.FontFamily.Projects.Regular"),
            Foreground = GetModeBrush("TextPrimaryBrush", typographyMode),
            TextWrapping = TextWrapping.WrapWholeWords,
            FontSize = GetBodyFontSize(typographyMode),
            Opacity = 0.82
        });

        if (!string.IsNullOrWhiteSpace(block.Hint))
        {
            content.Children.Add(new TextBlock
            {
                Text = block.Hint,
                FontFamily = GetFontFamily(typographyMode == ConversationTypographyMode.Chats
                    ? "Ui.FontFamily.Chats.Regular"
                    : "Ui.FontFamily.Projects.Regular"),
                Foreground = GetModeBrush("TextSecondaryBrush", typographyMode),
                TextWrapping = TextWrapping.WrapWholeWords,
                FontSize = 12,
                Opacity = 0.66
            });
        }

        container.Child = content;
        return container;
    }

    private UIElement BuildTable(TableBlock block, ConversationTypographyMode typographyMode)
    {
        var grid = new Grid
        {
            Margin = new Thickness(0, typographyMode == ConversationTypographyMode.Chats ? SpaceMd : SpaceSm, 0, typographyMode == ConversationTypographyMode.Chats ? SpaceMd : SpaceSm),
            BorderBrush = GetModeBrush("BorderQuietBrush", typographyMode),
            BorderThickness = new Thickness(1)
        };

        var columnCount = Math.Max(block.Headers.Count, 1);
        for (var i = 0; i < columnCount; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddTableRow(grid, 0, block.Headers, isHeader: true, typographyMode);

        for (var rowIndex = 0; rowIndex < block.Rows.Count; rowIndex++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddTableRow(grid, rowIndex + 1, block.Rows[rowIndex], isHeader: false, typographyMode);
        }

        return grid;
    }

    private void AddTableRow(
        Grid grid,
        int rowIndex,
        IReadOnlyList<string> cells,
        bool isHeader,
        ConversationTypographyMode typographyMode)
    {
        for (var columnIndex = 0; columnIndex < grid.ColumnDefinitions.Count; columnIndex++)
        {
            var value = columnIndex < cells.Count ? cells[columnIndex] : string.Empty;
            var border = new Border
            {
                Padding = typographyMode == ConversationTypographyMode.Chats
                    ? new Thickness(11, 9, 11, 9)
                    : new Thickness(10, 8, 10, 8),
                BorderBrush = GetModeBrush("BorderQuietBrush", typographyMode),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Background = isHeader
                    ? GetModeBrush("SurfaceSubtleBrush", typographyMode)
                    : rowIndex % 2 == 0
                        ? GetModeBrush("SurfaceQuoteBrush", typographyMode)
                        : null,
                Child = BuildRichText(
                    value,
                    typographyMode,
                    fontSize: typographyMode == ConversationTypographyMode.Chats ? 14.5 : 14,
                    lineHeight: typographyMode == ConversationTypographyMode.Chats ? 23 : 22)
            };

            if (border.Child is RichTextBlock richText && isHeader)
            {
                richText.FontWeight = FontWeights.SemiBold;
            }

            Grid.SetRow(border, rowIndex);
            Grid.SetColumn(border, columnIndex);
            grid.Children.Add(border);
        }
    }

    private RichTextBlock BuildRichText(
        string text,
        ConversationTypographyMode typographyMode,
        double? fontSize = null,
        double? lineHeight = null)
    {
        var block = new RichTextBlock
        {
            TextWrapping = TextWrapping.WrapWholeWords,
            FontFamily = GetFontFamily(typographyMode == ConversationTypographyMode.Chats
                ? "Ui.FontFamily.Chats.Regular"
                : "Ui.FontFamily.Projects.Regular"),
            FontSize = fontSize ?? GetBodyFontSize(typographyMode),
            Foreground = GetModeBrush("TextPrimaryBrush", typographyMode),
            IsTextSelectionEnabled = true,
            CharacterSpacing = typographyMode == ConversationTypographyMode.Projects ? 4 : 0
        };

        var paragraph = new Paragraph();
        paragraph.LineHeight = lineHeight ?? GetBodyLineHeight(typographyMode);
        foreach (var inline in BuildInlines(text, typographyMode))
        {
            paragraph.Inlines.Add(inline);
        }

        block.Blocks.Add(paragraph);
        return block;
    }

    private static IEnumerable<Inline> BuildInlines(string text, ConversationTypographyMode typographyMode)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield return new Run { Text = string.Empty };
            yield break;
        }

        var literalBuffer = new System.Text.StringBuilder();

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\\' && index + 1 < text.Length)
            {
                literalBuffer.Append(text[index + 1]);
                index++;
                continue;
            }

            if (TryReadDelimited(text, index, "~~", out var strikeText, out var strikeEndIndex))
            {
                FlushLiteralBuffer(literalBuffer, out var literal);
                if (literal is not null)
                {
                    yield return literal;
                }

                var run = new Run { Text = strikeText };
                run.TextDecorations = TextDecorations.Strikethrough;
                yield return run;
                index = strikeEndIndex;
            }
            else if (TryReadDelimited(text, index, "***", out var boldItalicText, out var boldItalicEndIndex))
            {
                FlushLiteralBuffer(literalBuffer, out var literal);
                if (literal is not null)
                {
                    yield return literal;
                }

                yield return new Run
                {
                    Text = boldItalicText,
                    FontWeight = FontWeights.SemiBold,
                    FontStyle = FontStyle.Italic
                };
                index = boldItalicEndIndex;
            }
            else if (TryReadDelimited(text, index, "**", out var boldText, out var boldEndIndex))
            {
                FlushLiteralBuffer(literalBuffer, out var literal);
                if (literal is not null)
                {
                    yield return literal;
                }

                yield return new Run
                {
                    Text = boldText,
                    FontWeight = FontWeights.SemiBold
                };
                index = boldEndIndex;
            }
            else if (TryReadDelimited(text, index, "*", out var italicText, out var italicEndIndex))
            {
                FlushLiteralBuffer(literalBuffer, out var literal);
                if (literal is not null)
                {
                    yield return literal;
                }

                yield return new Run
                {
                    Text = italicText,
                    FontStyle = FontStyle.Italic
                };
                index = italicEndIndex;
            }
            else if (TryReadDelimited(text, index, "`", out var codeText, out var codeEndIndex))
            {
                FlushLiteralBuffer(literalBuffer, out var literal);
                if (literal is not null)
                {
                    yield return literal;
                }

                var span = new Span
                {
                    FontFamily = new FontFamily("Consolas")
                };
                span.Inlines.Add(new Run { Text = codeText });
                yield return span;
                index = codeEndIndex;
            }
            else if (TryReadLink(text, index, out var linkLabel, out var linkUri, out var linkEndIndex))
            {
                FlushLiteralBuffer(literalBuffer, out var literal);
                if (literal is not null)
                {
                    yield return literal;
                }

                var hyperlink = new Hyperlink();
                hyperlink.Foreground = GetModeBrush("AccentFillBrush", typographyMode);
                hyperlink.Inlines.Add(new Run { Text = linkLabel });
                if (Uri.TryCreate(linkUri, UriKind.Absolute, out var uri))
                {
                    hyperlink.NavigateUri = uri;
                }

                yield return hyperlink;
                index = linkEndIndex;
            }
            else if (TryReadInlineFormula(text, index, out var formulaText, out var formulaEndIndex))
            {
                FlushLiteralBuffer(literalBuffer, out var literal);
                if (literal is not null)
                {
                    yield return literal;
                }

                var span = new Span
                {
                    FontFamily = new FontFamily("Consolas")
                };
                span.Inlines.Add(new Run { Text = formulaText, FontStyle = FontStyle.Italic });
                yield return span;
                index = formulaEndIndex;
            }
            else if (TryReadAutolink(text, index, out var autolinkValue, out var autolinkEndIndex))
            {
                FlushLiteralBuffer(literalBuffer, out var literal);
                if (literal is not null)
                {
                    yield return literal;
                }

                var hyperlink = new Hyperlink();
                hyperlink.Foreground = GetModeBrush("AccentFillBrush", typographyMode);
                hyperlink.Inlines.Add(new Run { Text = autolinkValue });
                if (Uri.TryCreate(autolinkValue, UriKind.Absolute, out var uri))
                {
                    hyperlink.NavigateUri = uri;
                }

                yield return hyperlink;
                index = autolinkEndIndex;
            }
            else if (TryReadFootnoteReference(text, index, out var footnoteLabel, out var footnoteEndIndex))
            {
                FlushLiteralBuffer(literalBuffer, out var literal);
                if (literal is not null)
                {
                    yield return literal;
                }

                var span = new Span
                {
                    FontSize = 11
                };
                span.Inlines.Add(new Run
                {
                    Text = $"[{footnoteLabel}]",
                    Foreground = GetModeBrush("TextSecondaryBrush", typographyMode)
                });
                yield return span;
                index = footnoteEndIndex;
            }
            else if (TryReadHtmlTag(text, index, out var htmlTag, out var htmlEndIndex))
            {
                FlushLiteralBuffer(literalBuffer, out var literal);
                if (literal is not null)
                {
                    yield return literal;
                }

                var span = new Span
                {
                    FontFamily = new FontFamily("Consolas")
                };
                span.Inlines.Add(new Run
                {
                    Text = htmlTag,
                    Foreground = GetModeBrush("TextSecondaryBrush", typographyMode)
                });
                yield return span;
                index = htmlEndIndex;
            }
            else
            {
                literalBuffer.Append(text[index]);
            }
        }

        FlushLiteralBuffer(literalBuffer, out var trailingLiteral);
        if (trailingLiteral is not null)
        {
            yield return trailingLiteral;
        }
    }

    private static void FlushLiteralBuffer(System.Text.StringBuilder buffer, out Run? literal)
    {
        if (buffer.Length == 0)
        {
            literal = null;
            return;
        }

        literal = new Run { Text = ReplaceEmojiAliases(buffer.ToString()) };
        buffer.Clear();
    }

    private static bool TryReadDelimited(
        string text,
        int startIndex,
        string delimiter,
        out string innerText,
        out int endIndex)
    {
        innerText = string.Empty;
        endIndex = startIndex;

        if (!text.AsSpan(startIndex).StartsWith(delimiter, StringComparison.Ordinal))
        {
            return false;
        }

        var searchIndex = startIndex + delimiter.Length;
        while (searchIndex <= text.Length - delimiter.Length)
        {
            if (text[searchIndex] == '\\')
            {
                searchIndex += 2;
                continue;
            }

            if (text.AsSpan(searchIndex).StartsWith(delimiter, StringComparison.Ordinal))
            {
                innerText = UnescapeMarkdownText(text[(startIndex + delimiter.Length)..searchIndex]);
                endIndex = searchIndex + delimiter.Length - 1;
                return true;
            }

            searchIndex++;
        }

        return false;
    }

    private static bool TryReadLink(
        string text,
        int startIndex,
        out string label,
        out string uri,
        out int endIndex)
    {
        label = string.Empty;
        uri = string.Empty;
        endIndex = startIndex;

        if (text[startIndex] != '[')
        {
            return false;
        }

        var labelEnd = FindUnescapedCharacter(text, ']', startIndex + 1);
        if (labelEnd < 0 || labelEnd + 1 >= text.Length || text[labelEnd + 1] != '(')
        {
            return false;
        }

        var uriEnd = FindUnescapedCharacter(text, ')', labelEnd + 2);
        if (uriEnd < 0)
        {
            return false;
        }

        label = UnescapeMarkdownText(text[(startIndex + 1)..labelEnd]);
        uri = UnescapeMarkdownText(text[(labelEnd + 2)..uriEnd]);
        endIndex = uriEnd;
        return true;
    }

    private static bool TryReadInlineFormula(
        string text,
        int startIndex,
        out string formula,
        out int endIndex)
    {
        formula = string.Empty;
        endIndex = startIndex;

        if (text[startIndex] != '$' ||
            (startIndex > 0 && text[startIndex - 1] == '\\') ||
            (startIndex + 1 < text.Length && text[startIndex + 1] == '$'))
        {
            return false;
        }

        for (var index = startIndex + 1; index < text.Length; index++)
        {
            if (text[index] == '\\')
            {
                index++;
                continue;
            }

            if (text[index] == '$')
            {
                formula = UnescapeMarkdownText(text[(startIndex + 1)..index]);
                endIndex = index;
                return true;
            }
        }

        return false;
    }

    private static bool TryReadAutolink(
        string text,
        int startIndex,
        out string value,
        out int endIndex)
    {
        value = string.Empty;
        endIndex = startIndex;

        if (!text.AsSpan(startIndex).StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !text.AsSpan(startIndex).StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var cursor = startIndex;
        while (cursor < text.Length && !char.IsWhiteSpace(text[cursor]))
        {
            cursor++;
        }

        value = text[startIndex..cursor].TrimEnd('.', ',', ';', ':', '!', '?');
        endIndex = startIndex + value.Length - 1;
        return Uri.TryCreate(value, UriKind.Absolute, out _);
    }

    private static bool TryReadFootnoteReference(
        string text,
        int startIndex,
        out string label,
        out int endIndex)
    {
        label = string.Empty;
        endIndex = startIndex;

        if (!(text.AsSpan(startIndex).StartsWith("[^", StringComparison.Ordinal)))
        {
            return false;
        }

        var closingBracket = FindUnescapedCharacter(text, ']', startIndex + 2);
        if (closingBracket < 0)
        {
            return false;
        }

        label = UnescapeMarkdownText(text[(startIndex + 2)..closingBracket]);
        endIndex = closingBracket;
        return true;
    }

    private static bool TryReadHtmlTag(
        string text,
        int startIndex,
        out string tag,
        out int endIndex)
    {
        tag = string.Empty;
        endIndex = startIndex;

        if (text[startIndex] != '<')
        {
            return false;
        }

        var closingBracket = FindUnescapedCharacter(text, '>', startIndex + 1);
        if (closingBracket < 0)
        {
            return false;
        }

        tag = text[startIndex..(closingBracket + 1)];
        endIndex = closingBracket;
        return true;
    }

    private static int FindUnescapedCharacter(string text, char value, int startIndex)
    {
        for (var index = startIndex; index < text.Length; index++)
        {
            if (text[index] == '\\')
            {
                index++;
                continue;
            }

            if (text[index] == value)
            {
                return index;
            }
        }

        return -1;
    }

    private static string UnescapeMarkdownText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var builder = new System.Text.StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\\' && index + 1 < text.Length)
            {
                builder.Append(text[index + 1]);
                index++;
                continue;
            }

            builder.Append(text[index]);
        }

        return builder.ToString();
    }

    private static string ReplaceEmojiAliases(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return text
            .Replace(":rocket:", "🚀", StringComparison.Ordinal)
            .Replace(":white_check_mark:", "✅", StringComparison.Ordinal)
            .Replace(":warning:", "⚠️", StringComparison.Ordinal)
            .Replace(":beetle:", "🐞", StringComparison.Ordinal)
            .Replace(":crab:", "🦀", StringComparison.Ordinal)
            .Replace(":bug:", "🐛", StringComparison.Ordinal)
            .Replace(":fire:", "🔥", StringComparison.Ordinal)
            .Replace(":sparkles:", "✨", StringComparison.Ordinal);
    }

    private static Brush GetBrush(string key)
    {
        return (Brush)Application.Current.Resources[key];
    }

    private static Brush GetModeBrush(string suffix, ConversationTypographyMode typographyMode)
    {
        var prefix = typographyMode == ConversationTypographyMode.Chats ? "Ui.Chat." : "Ui.Project.";
        return (Brush)Application.Current.Resources[$"{prefix}{suffix}"];
    }

    private static FontFamily GetFontFamily(string key)
    {
        return (FontFamily)Application.Current.Resources[key];
    }

    private static double GetBodyFontSize(ConversationTypographyMode typographyMode)
    {
        return typographyMode == ConversationTypographyMode.Chats
            ? (double)Application.Current.Resources["Ui.Typography.Chats.FontSize"]
            : (double)Application.Current.Resources["Ui.Typography.Projects.FontSize"];
    }

    private static double GetBodyLineHeight(ConversationTypographyMode typographyMode)
    {
        return typographyMode == ConversationTypographyMode.Chats
            ? (double)Application.Current.Resources["Ui.Typography.Chats.LineHeight"]
            : (double)Application.Current.Resources["Ui.Typography.Projects.LineHeight"];
    }

    private static double GetBlockSpacing(ConversationTypographyMode typographyMode)
    {
        return typographyMode == ConversationTypographyMode.Chats ? 12 : 10;
    }

    private static double GetHeadingFontSize(int level, ConversationTypographyMode typographyMode)
    {
        if (typographyMode == ConversationTypographyMode.Chats)
        {
            return level switch
            {
                1 => 31d,
                2 => 25d,
                3 => 20d,
                4 => 18d,
                5 => 17d,
                _ => 16d
            };
        }

        return level switch
        {
            1 => 28d,
            2 => 23d,
            3 => 19d,
            4 => 17d,
            5 => 16d,
            _ => 15.5d
        };
    }

    private static double GetHeadingLineHeight(int level, ConversationTypographyMode typographyMode)
    {
        var fontSize = GetHeadingFontSize(level, typographyMode);
        return typographyMode == ConversationTypographyMode.Chats
            ? (level <= 2 ? fontSize * 1.16 : fontSize * 1.22)
            : (level <= 2 ? fontSize * 1.14 : fontSize * 1.2);
    }

    private static Thickness GetHeadingMargin(int level, ConversationTypographyMode typographyMode)
    {
        if (typographyMode == ConversationTypographyMode.Chats)
        {
            return level switch
            {
                1 => new Thickness(0, 20, 0, 6),
                2 => new Thickness(0, 16, 0, 5),
                3 => new Thickness(0, 12, 0, 4),
                _ => new Thickness(0, 10, 0, 3)
            };
        }

        return level switch
        {
            1 => new Thickness(0, 16, 0, 5),
            2 => new Thickness(0, 12, 0, 4),
            3 => new Thickness(0, 10, 0, 3),
            _ => new Thickness(0, 8, 0, 2)
        };
    }

    private static double GetListIndent(int depth, ConversationTypographyMode typographyMode)
    {
        var baseIndent = typographyMode == ConversationTypographyMode.Chats ? 18d : 16d;
        var nestedStep = typographyMode == ConversationTypographyMode.Chats ? 20d : 18d;
        return baseIndent + (Math.Max(0, depth) * nestedStep);
    }

    private static void IncrementOrderedCounters(MarkdownListItem item, Dictionary<int, int> counters)
    {
        var keysToReset = new List<int>();
        foreach (var depth in counters.Keys)
        {
            if (depth > item.Depth)
            {
                keysToReset.Add(depth);
            }
        }

        foreach (var depth in keysToReset)
        {
            counters.Remove(depth);
        }

        if (!item.Ordered)
        {
            return;
        }

        counters[item.Depth] = counters.TryGetValue(item.Depth, out var current) ? current + 1 : 1;
    }

    private static UIElement BuildListMarkerElement(
        MarkdownListItem item,
        Dictionary<int, int> orderedCountersByDepth,
        ConversationTypographyMode typographyMode)
    {
        if (item.IsChecked == true)
        {
            return BuildTaskListMarker(true, typographyMode);
        }

        if (item.IsChecked == false)
        {
            return BuildTaskListMarker(false, typographyMode);
        }

        return new TextBlock
        {
            Text = item.Ordered
                ? $"{orderedCountersByDepth.GetValueOrDefault(item.Depth, 1)}."
                : item.Depth == 0
                    ? "\u2022"
                    : "\u25E6",
            FontFamily = GetFontFamily(typographyMode == ConversationTypographyMode.Chats
                ? "Ui.FontFamily.Chats.Regular"
                : "Ui.FontFamily.Projects.Regular"),
            Foreground = GetModeBrush("TextSecondaryBrush", typographyMode),
            FontSize = item.Depth == 0
                ? (typographyMode == ConversationTypographyMode.Chats ? 16 : 15)
                : (typographyMode == ConversationTypographyMode.Chats ? 14.5 : 13.5),
            Margin = new Thickness(0, 1, 0, 0),
            Opacity = item.Depth == 0 ? 0.88 : 0.74,
            VerticalAlignment = VerticalAlignment.Top
        };
    }

    private static UIElement BuildTaskListMarker(bool isChecked, ConversationTypographyMode typographyMode)
    {
        var size = typographyMode == ConversationTypographyMode.Chats ? 14d : 13d;
        var border = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1),
            BorderBrush = isChecked
                ? GetModeBrush("AccentFillBrush", typographyMode)
                : GetModeBrush("TextSecondaryBrush", typographyMode),
            Background = isChecked
                ? GetModeBrush("AccentFillBrush", typographyMode)
                : new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 3, 0, 0)
        };

        if (!isChecked)
        {
            return border;
        }

        border.Child = new TextBlock
        {
            Text = "✓",
            FontSize = typographyMode == ConversationTypographyMode.Chats ? 10.5 : 10,
            Foreground = GetModeBrush("AccentForegroundBrush", typographyMode),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, -1, 0, 0)
        };

        return border;
    }
}

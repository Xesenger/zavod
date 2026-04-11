using System.Collections.Generic;
using System.Linq;

namespace zavod.Presentation.Conversation.Markdown;

public abstract record MarkdownBlock;

public sealed record MarkdownListItem(
    string Text,
    int Depth = 0,
    bool? IsChecked = null,
    bool Ordered = false);

public sealed record QuoteLine(int Depth, string Text);

public sealed record ParagraphBlock(IReadOnlyList<string> Lines) : MarkdownBlock
{
    public string Text => string.Join("\n", Lines);
}

public sealed record HeadingBlock(int Level, string Text) : MarkdownBlock;

public sealed record QuoteBlock(IReadOnlyList<QuoteLine> Lines) : MarkdownBlock
{
    public string Text => string.Join("\n", Lines.Select(line => line.Text));
}

public sealed record ListBlock(IReadOnlyList<MarkdownListItem> Items, bool Ordered) : MarkdownBlock;

public sealed record CodeBlock(string Code, string? Language) : MarkdownBlock;

public sealed record FormulaBlock(string Formula, bool IsBlock) : MarkdownBlock;

public sealed record DividerBlock : MarkdownBlock;

public sealed record ImageBlock(string AltText, string Url) : MarkdownBlock;

public sealed record FootnoteBlock(string Label, string Text) : MarkdownBlock;

public sealed record ErrorBlock(string Title, string Message, string? Hint = null) : MarkdownBlock;

public sealed record TableBlock(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) : MarkdownBlock;

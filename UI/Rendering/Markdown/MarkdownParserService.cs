using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace zavod.UI.Rendering.Markdown;

public sealed class MarkdownParserService
{
    public IReadOnlyList<MarkdownBlock> Parse(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return Array.Empty<MarkdownBlock>();
        }

        var normalized = markdown.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        var blocks = new List<MarkdownBlock>();
        var paragraph = new List<string>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                FlushParagraph(blocks, paragraph);
                continue;
            }

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph(blocks, paragraph);
                var language = trimmed.Length > 3 ? trimmed[3..].Trim() : null;
                var code = new StringBuilder();
                i++;
                while (i < lines.Length && !lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    if (code.Length > 0)
                    {
                        code.Append('\n');
                    }

                    code.Append(lines[i]);
                    i++;
                }

                blocks.Add(new CodeBlock(code.ToString(), string.IsNullOrWhiteSpace(language) ? null : language));
                continue;
            }

            if (TryCollectFormula(lines, ref i, out var formula))
            {
                FlushParagraph(blocks, paragraph);
                blocks.Add(formula);
                continue;
            }

            if (TryParseImage(trimmed, out var image))
            {
                FlushParagraph(blocks, paragraph);
                blocks.Add(image);
                continue;
            }

            if (TryParseFootnote(trimmed, out var footnote))
            {
                FlushParagraph(blocks, paragraph);
                blocks.Add(footnote);
                continue;
            }

            if (IsDivider(trimmed))
            {
                FlushParagraph(blocks, paragraph);
                blocks.Add(new DividerBlock());
                continue;
            }

            if (trimmed[0] == '#')
            {
                FlushParagraph(blocks, paragraph);
                var level = trimmed.TakeWhile(c => c == '#').Count();
                var headingText = trimmed[level..].Trim();
                if (headingText.Length > 0)
                {
                    blocks.Add(new HeadingBlock(Math.Min(level, 6), headingText));
                }

                continue;
            }

            if (trimmed.StartsWith(">", StringComparison.Ordinal))
            {
                FlushParagraph(blocks, paragraph);
                var quoteLines = new List<QuoteLine>();
                while (i < lines.Length)
                {
                    var quoteLine = lines[i];
                    var quoteTrimmed = quoteLine.Trim();
                    if (!quoteTrimmed.StartsWith(">", StringComparison.Ordinal))
                    {
                        break;
                    }

                    var depth = quoteTrimmed.TakeWhile(c => c == '>').Count();
                    var content = quoteTrimmed[depth..].TrimStart();
                    quoteLines.Add(new QuoteLine(Math.Max(depth, 1), content));
                    i++;
                }

                i--;
                blocks.Add(new QuoteBlock(quoteLines));
                continue;
            }

            if (TryParseTable(lines, ref i, out var table))
            {
                FlushParagraph(blocks, paragraph);
                blocks.Add(table);
                continue;
            }

            if (TryCollectList(lines, ref i, out var list))
            {
                FlushParagraph(blocks, paragraph);
                blocks.Add(list);
                continue;
            }

            paragraph.Add(line.TrimEnd());
        }

        FlushParagraph(blocks, paragraph);
        return blocks;
    }

    private static void FlushParagraph(List<MarkdownBlock> blocks, List<string> paragraph)
    {
        if (paragraph.Count == 0)
        {
            return;
        }

        blocks.Add(new ParagraphBlock(paragraph.ToArray()));
        paragraph.Clear();
    }

    private static bool IsDivider(string line)
    {
        return line is "---" or "***";
    }

    private static bool TryCollectList(string[] lines, ref int index, out ListBlock list)
    {
        var items = new List<MarkdownListItem>();
        var ordered = false;
        var startIndex = index;

        while (index < lines.Length)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                break;
            }

            var depth = CalculateListDepth(line);

            if (TryParseUnorderedListItem(trimmed, depth, out var unorderedItem))
            {
                items.Add(unorderedItem);
                index++;
                continue;
            }

            var dotIndex = trimmed.IndexOf(". ", StringComparison.Ordinal);
            if (dotIndex > 0 && int.TryParse(trimmed[..dotIndex], out _))
            {
                ordered = true;
                items.Add(new MarkdownListItem(trimmed[(dotIndex + 2)..].Trim(), depth, Ordered: true));
                index++;
                continue;
            }

            break;
        }

        if (items.Count == 0)
        {
            list = null!;
            index = startIndex;
            return false;
        }

        index--;
        list = new ListBlock(items, ordered);
        return true;
    }

    private static int CalculateListDepth(string line)
    {
        var indent = 0;
        foreach (var character in line)
        {
            if (character == ' ')
            {
                indent++;
                continue;
            }

            if (character == '\t')
            {
                indent += 4;
                continue;
            }

            break;
        }

        return Math.Max(0, indent / 2);
    }

    private static bool TryParseUnorderedListItem(string text, int depth, out MarkdownListItem item)
    {
        item = null!;
        if (!(text.StartsWith("- ", StringComparison.Ordinal)
            || text.StartsWith("* ", StringComparison.Ordinal)
            || text.StartsWith("+ ", StringComparison.Ordinal)))
        {
            return false;
        }

        var content = text[2..].Trim();
        if (content.StartsWith("[ ] ", StringComparison.Ordinal))
        {
            item = new MarkdownListItem(content[4..].Trim(), depth, false, false);
            return true;
        }

        if (content.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase))
        {
            item = new MarkdownListItem(content[4..].Trim(), depth, true, false);
            return true;
        }

        item = new MarkdownListItem(content, depth, null, false);
        return true;
    }

    private static bool TryParseTable(string[] lines, ref int index, out TableBlock table)
    {
        table = null!;
        if (index + 1 >= lines.Length)
        {
            return false;
        }

        var headerLine = lines[index].Trim();
        var separatorLine = lines[index + 1].Trim();
        if (!headerLine.Contains('|') || !LooksLikeTableSeparator(separatorLine))
        {
            return false;
        }

        var headers = SplitTableCells(headerLine);
        var rows = new List<IReadOnlyList<string>>();
        var cursor = index + 2;
        while (cursor < lines.Length)
        {
            var current = lines[cursor].Trim();
            if (current.Length == 0 || !current.Contains('|'))
            {
                break;
            }

            rows.Add(SplitTableCells(current));
            cursor++;
        }

        index = cursor - 1;
        table = new TableBlock(headers, rows);
        return true;
    }

    private static bool LooksLikeTableSeparator(string line)
    {
        if (!line.Contains('|'))
        {
            return false;
        }

        foreach (var cell in SplitTableCells(line))
        {
            var trimmed = cell.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (trimmed.Any(c => c != '-' && c != ':' && c != ' '))
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<string> SplitTableCells(string line)
    {
        return line
            .Trim()
            .Trim('|')
            .Split('|')
            .Select(cell => cell.Trim())
            .ToArray();
    }

    private static bool TryParseImage(string line, out ImageBlock image)
    {
        var match = Regex.Match(line, @"^!\[(.*?)\]\((.*?)\)$");
        if (match.Success)
        {
            image = new ImageBlock(match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim());
            return true;
        }

        image = null!;
        return false;
    }

    private static bool TryParseFootnote(string line, out FootnoteBlock footnote)
    {
        var match = Regex.Match(line, @"^\[\^([^\]]+)\]:\s*(.+)$");
        if (match.Success)
        {
            footnote = new FootnoteBlock(match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim());
            return true;
        }

        footnote = null!;
        return false;
    }

    private static bool TryCollectFormula(string[] lines, ref int index, out FormulaBlock formula)
    {
        var trimmed = lines[index].Trim();
        if (trimmed.StartsWith("$$", StringComparison.Ordinal) &&
            trimmed.EndsWith("$$", StringComparison.Ordinal) &&
            trimmed.Length > 4)
        {
            formula = new FormulaBlock(trimmed[2..^2].Trim(), true);
            return true;
        }

        if (!string.Equals(trimmed, "$$", StringComparison.Ordinal))
        {
            formula = null!;
            return false;
        }

        var builder = new StringBuilder();
        index++;
        while (index < lines.Length)
        {
            var current = lines[index].Trim();
            if (string.Equals(current, "$$", StringComparison.Ordinal))
            {
                formula = new FormulaBlock(builder.ToString().Trim(), true);
                return true;
            }

            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(lines[index]);
            index++;
        }

        formula = new FormulaBlock(builder.ToString().Trim(), true);
        return true;
    }
}

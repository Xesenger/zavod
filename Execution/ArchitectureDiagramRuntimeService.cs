using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using zavod.Workspace;

namespace zavod.Execution;

public sealed class ArchitectureDiagramRuntimeService
{
    public MaterialRuntimeDiagnostic? RenderPng(ArchitectureDiagramSpec spec, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

            var groups = BuildRenderableGroups(spec);
            var layout = BuildLayout(spec, groups);

            using var bitmap = new Bitmap(layout.CanvasWidth, layout.CanvasHeight);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.FromArgb(244, 243, 238));

            using var titleFont = new Font("Segoe UI", 30, FontStyle.Bold);
            using var subtitleFont = new Font("Segoe UI", 13, FontStyle.Regular);
            using var groupFont = new Font("Segoe UI", 16, FontStyle.Bold);
            using var nodeFont = new Font("Segoe UI", 14, FontStyle.Bold);
            using var metaFont = new Font("Segoe UI", 11, FontStyle.Regular);
            using var noteFont = new Font("Segoe UI", 12, FontStyle.Regular);
            using var edgePen = new Pen(Color.FromArgb(86, 96, 110), 3f)
            {
                EndCap = LineCap.ArrowAnchor
            };
            using var borderPen = new Pen(Color.FromArgb(176, 178, 171), 2f);
            using var textBrush = new SolidBrush(Color.FromArgb(29, 33, 41));
            using var mutedBrush = new SolidBrush(Color.FromArgb(92, 98, 112));
            using var groupBrush = new SolidBrush(Color.FromArgb(232, 235, 229));
            using var layerGroupHeaderBrush = new SolidBrush(Color.FromArgb(207, 226, 255));
            using var entryGroupHeaderBrush = new SolidBrush(Color.FromArgb(255, 226, 199));
            using var genericGroupHeaderBrush = new SolidBrush(Color.FromArgb(220, 227, 214));
            using var nodeBrush = new SolidBrush(Color.FromArgb(255, 255, 255));
            using var emphasisBrush = new SolidBrush(Color.FromArgb(255, 245, 206));
            using var entryBrush = new SolidBrush(Color.FromArgb(240, 248, 255));

            graphics.DrawString(spec.Title, titleFont, textBrush, new RectangleF(48, 30, layout.CanvasWidth - 96, 52));
            graphics.DrawString(
                "ZAVOD architecture snapshot: evidence-backed layers, entry points, and observed flow.",
                subtitleFont,
                mutedBrush,
                new RectangleF(50, 78, layout.CanvasWidth - 100, 28));

            foreach (var group in groups)
            {
                var groupRect = CalculateGroupBounds(group, layout.Positions);
                var headerRect = new RectangleF(groupRect.X, groupRect.Y, groupRect.Width, 38);
                graphics.FillRectangle(groupBrush, groupRect);
                graphics.DrawRectangle(borderPen, Rectangle.Round(groupRect));
                graphics.FillRectangle(GetGroupHeaderBrush(group, layerGroupHeaderBrush, entryGroupHeaderBrush, genericGroupHeaderBrush), headerRect);
                graphics.DrawRectangle(borderPen, Rectangle.Round(groupRect));
                graphics.DrawString(group.Label, groupFont, textBrush, new PointF(groupRect.X + 14, groupRect.Y + 9));
            }

            foreach (var edge in spec.Edges)
            {
                if (!layout.Positions.TryGetValue(edge.From, out var fromRect) || !layout.Positions.TryGetValue(edge.To, out var toRect))
                {
                    continue;
                }

                var start = new PointF(fromRect.X + fromRect.Width / 2f, fromRect.Bottom);
                var end = new PointF(toRect.X + toRect.Width / 2f, toRect.Y);
                var controlOffset = Math.Max(36f, Math.Abs(end.Y - start.Y) / 2f);
                var control1 = new PointF(start.X, start.Y + controlOffset);
                var control2 = new PointF(end.X, end.Y - controlOffset);
                graphics.DrawBezier(edgePen, start, control1, control2, end);
                if (!string.IsNullOrWhiteSpace(edge.Label))
                {
                    var labelRect = new RectangleF(
                        Math.Min(start.X, end.X) - 32,
                        ((start.Y + end.Y) / 2f) - 18,
                        Math.Abs(end.X - start.X) + 64,
                        24);
                    graphics.DrawString(edge.Label, metaFont, mutedBrush, labelRect, BuildCenteredStringFormat());
                }
            }

            foreach (var node in spec.Nodes)
            {
                if (!layout.Positions.TryGetValue(node.Id, out var rect))
                {
                    continue;
                }

                var isEntryNode = string.Equals(node.GroupId, "entries", StringComparison.OrdinalIgnoreCase) ||
                                  node.Kind.Contains("entry", StringComparison.OrdinalIgnoreCase) ||
                                  node.Kind.Contains("main", StringComparison.OrdinalIgnoreCase) ||
                                  node.Kind.Contains("cli", StringComparison.OrdinalIgnoreCase) ||
                                  node.Kind.Contains("test", StringComparison.OrdinalIgnoreCase);
                var fillBrush = spec.RenderHints.EmphasisNodes.Contains(node.Id, StringComparer.OrdinalIgnoreCase)
                    ? emphasisBrush
                    : isEntryNode
                        ? entryBrush
                        : nodeBrush;
                graphics.FillRectangle(fillBrush, rect);
                graphics.DrawRectangle(borderPen, Rectangle.Round(rect));
                graphics.DrawString(
                    node.Label,
                    nodeFont,
                    textBrush,
                    new RectangleF(rect.X + 14, rect.Y + 12, rect.Width - 28, rect.Height - 48),
                    BuildTopLeftStringFormat());
                graphics.DrawString(
                    node.Kind,
                    metaFont,
                    mutedBrush,
                    new RectangleF(rect.X + 14, rect.Bottom - 28, rect.Width - 28, 16),
                    BuildTopLeftStringFormat());
            }

            if (spec.Notes.Count > 0)
            {
                var notesBox = new RectangleF(48, layout.NotesTop, layout.CanvasWidth - 96, layout.CanvasHeight - layout.NotesTop - 32);
                graphics.FillRectangle(nodeBrush, notesBox);
                graphics.DrawRectangle(borderPen, Rectangle.Round(notesBox));
                graphics.DrawString("Interpretation Notes", groupFont, textBrush, new PointF(notesBox.X + 14, notesBox.Y + 10));
                var notesText = string.Join(Environment.NewLine, spec.Notes.Take(5));
                graphics.DrawString(notesText, noteFont, textBrush, new RectangleF(notesBox.X + 14, notesBox.Y + 42, notesBox.Width - 28, notesBox.Height - 56));
            }

            bitmap.Save(outputPath, ImageFormat.Png);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ExternalException or ArgumentException)
        {
            return new MaterialRuntimeDiagnostic("DIAGRAM_RENDER_FAILED", exception.Message);
        }
    }

    private static IReadOnlyList<RenderableGroup> BuildRenderableGroups(ArchitectureDiagramSpec spec)
    {
        var groups = spec.Groups
            .Select(group => new RenderableGroup(group.Id, group.Label, spec.Nodes.Where(node => string.Equals(node.GroupId, group.Id, StringComparison.OrdinalIgnoreCase) || group.Members.Contains(node.Id, StringComparer.OrdinalIgnoreCase)).ToArray()))
            .Where(static group => group.Nodes.Count > 0)
            .ToList();

        var ungroupedNodes = spec.Nodes.Where(node => groups.All(group => group.Nodes.All(existing => !string.Equals(existing.Id, node.Id, StringComparison.OrdinalIgnoreCase)))).ToArray();
        if (ungroupedNodes.Length > 0)
        {
            groups.Add(new RenderableGroup("ungrouped", "Observed Components", ungroupedNodes));
        }

        if (groups.Count == 0 && spec.Nodes.Count > 0)
        {
            groups.Add(new RenderableGroup("all", "Project", spec.Nodes.ToArray()));
        }

        return groups;
    }

    private static Dictionary<string, RectangleF> LayoutNodes(IReadOnlyList<RenderableGroup> groups)
    {
        var positions = new Dictionary<string, RectangleF>(StringComparer.OrdinalIgnoreCase);
        return positions;
    }

    private static RectangleF CalculateGroupBounds(RenderableGroup group, IReadOnlyDictionary<string, RectangleF> positions)
    {
        var nodeRects = group.Nodes
            .Where(node => positions.ContainsKey(node.Id))
            .Select(node => positions[node.Id])
            .ToArray();
        if (nodeRects.Length == 0)
        {
            return new RectangleF(40, 100, 300, 160);
        }

        var minX = nodeRects.Min(static rect => rect.X) - 18;
        var minY = nodeRects.Min(static rect => rect.Y) - 34;
        var maxX = nodeRects.Max(static rect => rect.Right) + 18;
        var maxY = nodeRects.Max(static rect => rect.Bottom) + 18;
        return new RectangleF(minX, minY, maxX - minX, maxY - minY);
    }

    private static DiagramLayout BuildLayout(ArchitectureDiagramSpec spec, IReadOnlyList<RenderableGroup> groups)
    {
        const float left = 48f;
        const float top = 126f;
        const float groupGap = 26f;
        const float rowGap = 26f;
        const float groupWidth = 420f;
        const float nodeWidth = 380f;
        const float nodeHeight = 112f;
        const float nodeGap = 18f;
        const float groupInnerLeft = 20f;
        const float groupHeaderHeight = 38f;
        const float groupTopPadding = 54f;
        const float groupBottomPadding = 18f;

        var positions = new Dictionary<string, RectangleF>(StringComparer.OrdinalIgnoreCase);
        var layerGroups = groups.Where(IsLayerGroup).ToArray();
        var otherGroups = groups.Where(group => !IsLayerGroup(group)).ToArray();
        var rowDefinitions = new List<IReadOnlyList<RenderableGroup>>();
        if (layerGroups.Length > 0)
        {
            rowDefinitions.Add(layerGroups);
        }

        if (otherGroups.Length > 0)
        {
            rowDefinitions.Add(otherGroups);
        }

        if (rowDefinitions.Count == 0)
        {
            rowDefinitions.Add(groups);
        }

        var currentTop = top;
        var widestRowWidth = 0f;
        foreach (var rowGroups in rowDefinitions)
        {
            var rowHeight = 0f;
            for (var groupIndex = 0; groupIndex < rowGroups.Count; groupIndex++)
            {
                var group = rowGroups[groupIndex];
                var groupLeft = left + groupIndex * (groupWidth + groupGap);
                var currentNodeTop = currentTop + groupTopPadding;
                foreach (var node in group.Nodes)
                {
                    positions[node.Id] = new RectangleF(groupLeft + groupInnerLeft, currentNodeTop, nodeWidth, nodeHeight);
                    currentNodeTop += nodeHeight + nodeGap;
                }

                var nodeAreaHeight = group.Nodes.Count == 0
                    ? nodeHeight
                    : (group.Nodes.Count * nodeHeight) + ((group.Nodes.Count - 1) * nodeGap);
                var groupHeight = groupHeaderHeight + (groupTopPadding - groupHeaderHeight) + nodeAreaHeight + groupBottomPadding;
                rowHeight = Math.Max(rowHeight, groupHeight);
            }

            var rowWidth = (rowGroups.Count * groupWidth) + ((Math.Max(0, rowGroups.Count - 1)) * groupGap);
            widestRowWidth = Math.Max(widestRowWidth, rowWidth);
            currentTop += rowHeight + rowGap;
        }

        var notesTop = currentTop + 8f;
        var canvasWidth = (int)Math.Ceiling(Math.Max(1600f, left + widestRowWidth + 48f));
        var canvasHeight = (int)Math.Ceiling(Math.Max(1100f, notesTop + 160f));
        return new DiagramLayout(positions, canvasWidth, canvasHeight, notesTop);
    }

    private static Brush GetGroupHeaderBrush(RenderableGroup group, Brush layerBrush, Brush entryBrush, Brush genericBrush)
    {
        if (IsLayerGroup(group))
        {
            return layerBrush;
        }

        if (string.Equals(group.Id, "entries", StringComparison.OrdinalIgnoreCase) ||
            group.Label.Contains("Entry", StringComparison.OrdinalIgnoreCase))
        {
            return entryBrush;
        }

        return genericBrush;
    }

    private static bool IsLayerGroup(RenderableGroup group)
    {
        return string.Equals(group.Id, "layers", StringComparison.OrdinalIgnoreCase) ||
               group.Label.Contains("Layer", StringComparison.OrdinalIgnoreCase);
    }

    private static StringFormat BuildTopLeftStringFormat()
    {
        return new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Near,
            Trimming = StringTrimming.EllipsisWord
        };
    }

    private static StringFormat BuildCenteredStringFormat()
    {
        return new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisWord
        };
    }

    private sealed record RenderableGroup(string Id, string Label, IReadOnlyList<ArchitectureDiagramNode> Nodes);
    private sealed record DiagramLayout(IReadOnlyDictionary<string, RectangleF> Positions, int CanvasWidth, int CanvasHeight, float NotesTop);
}

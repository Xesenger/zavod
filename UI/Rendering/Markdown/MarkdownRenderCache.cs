using System;
using System.Collections.Generic;

namespace zavod.UI.Rendering.Markdown;

public sealed class MarkdownRenderCache
{
    private readonly int _capacity;
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _lru = new();

    public MarkdownRenderCache(int capacity = 100)
    {
        _capacity = capacity;
    }

    public bool TryGet(string markdown, out IReadOnlyList<MarkdownBlock> blocks)
    {
        if (_cache.TryGetValue(markdown, out var entry))
        {
            Touch(entry.Node);
            blocks = entry.Blocks;
            return true;
        }

        blocks = Array.Empty<MarkdownBlock>();
        return false;
    }

    public void Store(string markdown, IReadOnlyList<MarkdownBlock> blocks)
    {
        if (_cache.TryGetValue(markdown, out var existing))
        {
            existing.Blocks = blocks;
            Touch(existing.Node);
            return;
        }

        if (_cache.Count >= _capacity && _lru.Last is not null)
        {
            var oldest = _lru.Last.Value;
            _lru.RemoveLast();
            _cache.Remove(oldest);
        }

        var node = _lru.AddFirst(markdown);
        _cache[markdown] = new CacheEntry(node, blocks);
    }

    private void Touch(LinkedListNode<string> node)
    {
        if (node.List is null || node == _lru.First)
        {
            return;
        }

        _lru.Remove(node);
        _lru.AddFirst(node);
    }

    private sealed class CacheEntry
    {
        public CacheEntry(LinkedListNode<string> node, IReadOnlyList<MarkdownBlock> blocks)
        {
            Node = node;
            Blocks = blocks;
        }

        public LinkedListNode<string> Node { get; }

        public IReadOnlyList<MarkdownBlock> Blocks { get; set; }
    }
}

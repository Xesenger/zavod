using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using zavod.UI.Rendering.Markdown;

namespace zavod.UI.Rendering.Conversation;

public sealed class MessageRenderPipeline
{
    private const int StreamingThrottleMs = 50;
    private readonly MarkdownParserService _parser = new();
    private readonly MarkdownRenderCache _cache = new(64);
    private readonly Dictionary<string, StreamingBuffer> _buffers = new(StringComparer.Ordinal);
    private readonly object _buffersGate = new();
    private readonly object _cacheGate = new();

    public Task RenderAsync(ConversationItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return RenderCoreAsync(item, item.Text, item.IsStreaming ? MessageRenderState.Streaming : MessageRenderState.Final);
    }

    public async Task AppendStreamingAsync(ConversationItemViewModel item, string chunk)
    {
        ArgumentNullException.ThrowIfNull(item);

        StreamingBuffer buffer;
        lock (_buffersGate)
        {
            if (!_buffers.TryGetValue(item.Id, out buffer!))
            {
                buffer = new StreamingBuffer();
                _buffers[item.Id] = buffer;
                if (!string.IsNullOrEmpty(item.Text))
                {
                    buffer.Append(item.Text);
                }
            }
        }

        buffer.Append(chunk);
        item.IsStreaming = true;
        item.Text = buffer.CurrentText;

        if (buffer.TryFlush(StreamingThrottleMs, out var current))
        {
            item.Text = current;
            await RenderCoreAsync(item, current, MessageRenderState.Streaming);
        }
    }

    public async Task CompleteStreamingAsync(ConversationItemViewModel item, string? authoritativeText = null)
    {
        ArgumentNullException.ThrowIfNull(item);

        StreamingBuffer buffer;
        lock (_buffersGate)
        {
            if (!_buffers.TryGetValue(item.Id, out buffer!))
            {
                buffer = new StreamingBuffer();
            }
            else
            {
                _buffers.Remove(item.Id);
            }
        }

        var finalText = buffer.Complete(authoritativeText ?? item.Text);
        item.Text = finalText;
        item.IsStreaming = false;
        await RenderCoreAsync(item, finalText, MessageRenderState.Final);
    }

    private async Task RenderCoreAsync(ConversationItemViewModel item, string text, MessageRenderState renderState)
    {
        IReadOnlyList<MarkdownBlock>? cached = null;
        lock (_cacheGate)
        {
            if (_cache.TryGet(text, out var blocks))
            {
                cached = blocks;
            }
        }

        var resolvedBlocks = cached;
        if (resolvedBlocks is null)
        {
            resolvedBlocks = await Task.Run(() => _parser.Parse(text));

            lock (_cacheGate)
            {
                if (!_cache.TryGet(text, out var existing))
                {
                    _cache.Store(text, resolvedBlocks);
                }
                else
                {
                    resolvedBlocks = existing;
                }
            }
        }

        item.Blocks = resolvedBlocks;
        item.RenderState = renderState;
    }
}

using System;
using System.Text;

namespace zavod.UI.Rendering.Conversation;

public sealed class StreamingBuffer
{
    private readonly StringBuilder _buffer = new();
    private string _lastFlushedText = string.Empty;
    private long _lastFlushTicks = Environment.TickCount64;

    public string CurrentText => _buffer.ToString();

    public void Append(string chunk)
    {
        if (!string.IsNullOrEmpty(chunk))
        {
            _buffer.Append(chunk);
        }
    }

    public bool TryFlush(int throttleMs, out string text)
    {
        text = string.Empty;
        var currentText = _buffer.ToString();
        if (string.IsNullOrEmpty(currentText) || string.Equals(currentText, _lastFlushedText, StringComparison.Ordinal))
        {
            return false;
        }

        if (!ShouldFlush(currentText, throttleMs))
        {
            return false;
        }

        _lastFlushedText = currentText;
        _lastFlushTicks = Environment.TickCount64;
        text = currentText;
        return true;
    }

    public string Complete(string? authoritativeText = null)
    {
        if (authoritativeText is not null)
        {
            _buffer.Clear();
            _buffer.Append(authoritativeText);
        }

        var finalText = _buffer.ToString();
        _lastFlushedText = finalText;
        _lastFlushTicks = Environment.TickCount64;
        return finalText;
    }

    private bool ShouldFlush(string currentText, int throttleMs)
    {
        if (currentText.EndsWith('\n'))
        {
            return true;
        }

        if (currentText.EndsWith("```", StringComparison.Ordinal) && CountCodeFenceMarkers(currentText) % 2 == 0)
        {
            return true;
        }

        return Environment.TickCount64 - _lastFlushTicks >= throttleMs;
    }

    private static int CountCodeFenceMarkers(string text)
    {
        var count = 0;
        var index = 0;
        while (index < text.Length)
        {
            var markerIndex = text.IndexOf("```", index, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                break;
            }

            count++;
            index = markerIndex + 3;
        }

        return count;
    }
}

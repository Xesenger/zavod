using System;
using System.Linq;

namespace zavod.Contexting;

public static class OrientationIntentDetector
{
    private static readonly string[] OrientationSignals =
    {
        "\u043a\u0442\u043e \u0442\u044b",
        "\u0447\u0442\u043e \u044d\u0442\u043e",
        "\u0447\u0442\u043e \u0442\u044b \u0443\u043c\u0435\u0435\u0448\u044c",
        "\u0433\u0434\u0435 \u044f",
        "\u0447\u0442\u043e \u0437\u0430 \u043f\u0440\u043e\u0433\u0440\u0430\u043c\u043c\u0430",
        "who are you",
        "what are you",
        "what can you do",
        "what is this",
        "where am i",
        "what does this do"
    };

    public static bool IsOrientationRequest(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var normalized = ProductIntentClassifier.NormalizeInput(text);
        return OrientationSignals.Any(signal => normalized.Contains(signal, StringComparison.Ordinal));
    }

    public static bool ShouldHandleAsOrientation(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (!IsOrientationRequest(text))
        {
            return false;
        }

        var productIntent = ProductIntentClassifier.Classify(text);
        return !productIntent.DetectedAction.Detected && !productIntent.DetectedTarget.Detected;
    }
}

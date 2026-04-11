using System;
using System.Linq;

namespace zavod.Contexting;

public static class OrientationIntentResponder
{
    public const string RussianIdentityResponse = "\u042f \u0432\u0435\u0434\u0443 \u0440\u0430\u0431\u043e\u0442\u0443 \u0432 ZAVOD \u0438 \u043f\u043e\u043c\u043e\u0433\u0430\u044e \u0444\u043e\u0440\u043c\u0438\u0440\u043e\u0432\u0430\u0442\u044c \u0438 \u0437\u0430\u043f\u0443\u0441\u043a\u0430\u0442\u044c \u0437\u0430\u0434\u0430\u0447\u0438.";
    public const string RussianCapabilityResponse = "\u041c\u044b \u0441\u0435\u0439\u0447\u0430\u0441 \u0440\u0430\u0431\u043e\u0442\u0430\u0435\u043c \u0432 ZAVOD. \u041c\u043e\u0433\u0443 \u043f\u043e\u043c\u043e\u0447\u044c \u0441\u0444\u043e\u0440\u043c\u0443\u043b\u0438\u0440\u043e\u0432\u0430\u0442\u044c \u0437\u0430\u0434\u0430\u0447\u0443 \u0438\u043b\u0438 \u0443\u0442\u043e\u0447\u043d\u0438\u0442\u044c \u0442\u0435\u043a\u0443\u0449\u0438\u0439 \u0448\u0430\u0433.";
    public const string EnglishIdentityResponse = "I work in ZAVOD and help shape and launch tasks.";
    public const string EnglishCapabilityResponse = "We are working in ZAVOD. I can help frame a task or clarify the current step.";

    private static readonly string[] EnglishSignals =
    {
        "who are you",
        "what are you",
        "what can you do",
        "what is this",
        "where am i",
        "what does this do"
    };

    private static readonly string[] CapabilitySignals =
    {
        "\u0447\u0442\u043e \u0442\u044b \u0443\u043c\u0435\u0435\u0448\u044c",
        "what can you do",
        "what does this do"
    };

    public static string Respond(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var normalized = ProductIntentClassifier.NormalizeInput(text);
        var isEnglish = EnglishSignals.Any(signal => normalized.Contains(signal, StringComparison.Ordinal));
        var isCapability = CapabilitySignals.Any(signal => normalized.Contains(signal, StringComparison.Ordinal));

        if (isEnglish)
        {
            return isCapability
                ? EnglishCapabilityResponse
                : EnglishIdentityResponse;
        }

        return isCapability
            ? RussianCapabilityResponse
            : RussianIdentityResponse;
    }
}

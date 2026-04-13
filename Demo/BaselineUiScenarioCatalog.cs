using System;
using System.Collections.Generic;
using System.Linq;
using zavod.UI.Rendering.Conversation;
using zavod.UI.Rendering.Markdown;

namespace zavod.Demo;

public static class BaselineUiScenarioCatalog
{
    public static BaselineUiScenario Resolve(string? scenarioName)
    {
        var key = scenarioName?.Trim().ToLowerInvariant();
        return key switch
        {
            "empty-chat" => BuildEmptyChat(),
            "short-answer" => BuildShortAnswer(),
            "long-markdown" => BuildLongMarkdown(),
            "rich-markdown" => BuildRichMarkdown(),
            "429" or "rate-limit" => BuildRateLimit(),
            _ => BuildRichMarkdown()
        };
    }

    private static BaselineUiScenario BuildEmptyChat()
    {
        return new BaselineUiScenario(
            "empty-chat",
            Array.Empty<BaselineConversationSeed>(),
            BuildProjectsIntro());
    }

    private static BaselineUiScenario BuildShortAnswer()
    {
        const string user = "Привет, это baseline short answer?";
        const string assistant = "Да. Это короткий ответ для проверки пустого и компактного состояния.";
        return new BaselineUiScenario(
            "short-answer",
            new[]
            {
                BaselineConversationSeed.User(user),
                BaselineConversationSeed.Assistant(assistant)
            },
            BuildProjectsConversation(user, assistant));
    }

    private static BaselineUiScenario BuildLongMarkdown()
    {
        const string user = "Покажи длинный markdown baseline.";
        var assistant =
            "# Baseline markdown\n\n" +
            "Это длинный документ для проверки вертикального ритма и ширины колонки.\n\n" +
            "## Основные секции\n\n" +
            "- Первый пункт с пояснением\n" +
            "- Второй пункт с чуть большим текстом\n" +
            "- Третий пункт для проверки списка\n\n" +
            "> Это многострочная цитата.\n" +
            "> Она должна рендериться единым блоком.\n\n" +
            "```csharp\n" +
            "var sum = 0;\n" +
            "for (var i = 0; i < 5; i++)\n" +
            "{\n" +
            "    sum += i;\n" +
            "}\n" +
            "```\n\n" +
            "---\n\n" +
            "Завершающий параграф после divider.";

        return new BaselineUiScenario(
            "long-markdown",
            new[]
            {
                BaselineConversationSeed.User(user),
                BaselineConversationSeed.Assistant(assistant)
            },
            BuildProjectsConversation(user, assistant));
    }

    private static BaselineUiScenario BuildRichMarkdown()
    {
        const string user = "Покажи rich markdown baseline.";
        var assistant =
            "# Rich baseline\n\n" +
            "Документ содержит **bold**, *italic*, `inline code` и ~~strikethrough~~.\n\n" +
            "## List\n\n" +
            "- [ ] Проверить пустой чат\n" +
            "- [x] Проверить ответ\n" +
            "- Обычный список\n\n" +
            "## Quote\n\n" +
            "> Базовая цитата\n" +
            ">> Вложенная цитата\n\n" +
            "## Code\n\n" +
            "```json\n" +
            "{\n" +
            "  \"mode\": \"baseline\",\n" +
            "  \"stable\": true\n" +
            "}\n" +
            "```\n\n" +
            "## Table\n\n" +
            "| Feature | State |\n" +
            "| --- | --- |\n" +
            "| Width | Stable |\n" +
            "| Streaming | Visible |\n" +
            "| Composer | Anchored |\n\n" +
            "![Demo image](https://example.invalid/baseline.png)\n\n" +
            "[^1]: Это baseline footnote fallback.";

        return new BaselineUiScenario(
            "rich-markdown",
            new[]
            {
                BaselineConversationSeed.User(user),
                BaselineConversationSeed.Assistant(assistant)
            },
            BuildProjectsConversation(user, assistant));
    }

    private static BaselineUiScenario BuildRateLimit()
    {
        const string user = "Сымитируй 429 состояние.";
        var chatError = new ErrorBlock(
            "Request limit reached",
            "The provider temporarily rejected the request because the rate limit was reached.",
            "Wait a bit and retry the request.");
        var projectError = new ErrorBlock(
            "Request limit reached",
            "The provider temporarily rejected the request because the rate limit was reached.",
            "Retry later or switch to a mock baseline scenario.");

        return new BaselineUiScenario(
            "rate-limit",
            new[]
            {
                BaselineConversationSeed.User(user),
                BaselineConversationSeed.Assistant("Rate limit reached.", chatError)
            },
            new[]
            {
                BaselineConversationSeed.Lead("Rate limit reached.", projectError)
            });
    }

    private static IReadOnlyList<BaselineConversationSeed> BuildProjectsIntro()
    {
        return new[]
        {
            BaselineConversationSeed.Lead(
                "# Projects baseline\n\nВыберите сценарий baseline, чтобы воспроизводимо тестировать UI без API.")
        };
    }

    private static IReadOnlyList<BaselineConversationSeed> BuildProjectsConversation(string user, string assistant)
    {
        return new[]
        {
            BaselineConversationSeed.User(user),
            BaselineConversationSeed.Lead(assistant)
        };
    }
}

public sealed record BaselineUiScenario(
    string Name,
    IReadOnlyList<BaselineConversationSeed> Chats,
    IReadOnlyList<BaselineConversationSeed> Projects);

public sealed record BaselineConversationSeed(
    ConversationItemKind Kind,
    string AuthorLabel,
    string Text,
    IReadOnlyList<MarkdownBlock>? Blocks = null)
{
    public static BaselineConversationSeed User(string text)
        => new(ConversationItemKind.User, "User", text);

    public static BaselineConversationSeed Assistant(string text, params MarkdownBlock[] blocks)
        => new(ConversationItemKind.Assistant, "Assistant", text, blocks.Length == 0 ? null : blocks);

    public static BaselineConversationSeed Lead(string text, params MarkdownBlock[] blocks)
        => new(ConversationItemKind.Lead, "Shift Lead", text, blocks.Length == 0 ? null : blocks);
}

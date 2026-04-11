using System.Globalization;

namespace zavod.Workspace;

public sealed record WorkspaceDocumentationLanguagePolicy(
    string LanguageTag,
    string TwoLetterIsoCode,
    string EnglishName,
    string NativeName)
{
    public bool IsRussian => string.Equals(TwoLetterIsoCode, "ru", System.StringComparison.OrdinalIgnoreCase);

    public static WorkspaceDocumentationLanguagePolicy ResolveCurrent()
    {
        var culture = CultureInfo.CurrentUICulture;
        if (string.IsNullOrWhiteSpace(culture.Name))
        {
            culture = CultureInfo.CurrentCulture;
        }

        if (string.IsNullOrWhiteSpace(culture.Name))
        {
            culture = CultureInfo.GetCultureInfo("en-US");
        }

        return new WorkspaceDocumentationLanguagePolicy(
            culture.Name,
            culture.TwoLetterISOLanguageName,
            culture.EnglishName,
            culture.NativeName);
    }
}

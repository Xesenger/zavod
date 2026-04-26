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
        var overrideValue = System.Environment.GetEnvironmentVariable("ZAVOD_UI_LANG")?.Trim();
        if (!string.IsNullOrWhiteSpace(overrideValue))
        {
            if (string.Equals(overrideValue, "ru", System.StringComparison.OrdinalIgnoreCase))
            {
                overrideValue = "ru-RU";
            }
            else if (string.Equals(overrideValue, "en", System.StringComparison.OrdinalIgnoreCase))
            {
                overrideValue = "en-US";
            }

            try
            {
                return FromCulture(CultureInfo.GetCultureInfo(overrideValue));
            }
            catch (CultureNotFoundException)
            {
                // Invalid overrides fall back to the OS/user culture below.
            }
        }

        var culture = CultureInfo.CurrentUICulture;
        if (string.IsNullOrWhiteSpace(culture.Name))
        {
            culture = CultureInfo.CurrentCulture;
        }

        if (string.IsNullOrWhiteSpace(culture.Name))
        {
            culture = CultureInfo.GetCultureInfo("en-US");
        }

        return FromCulture(culture);
    }

    private static WorkspaceDocumentationLanguagePolicy FromCulture(CultureInfo culture)
    {
        return new WorkspaceDocumentationLanguagePolicy(
            culture.Name,
            culture.TwoLetterISOLanguageName,
            culture.EnglishName,
            culture.NativeName);
    }
}

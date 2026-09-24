using System.Globalization;

namespace OllamaManager.Core.Interfaces;

public interface ILocalizationService
{
    CultureInfo CurrentCulture { get; }
    IReadOnlyList<LanguageInfo> SupportedLanguages { get; }
    event EventHandler<CultureInfo>? LanguageChanged;

    string GetString(string key);
    string GetString(string key, params object[] args);
    string this[string key] { get; }

    void SetLanguage(string cultureName);
    void SetLanguage(CultureInfo culture);

    bool IsRtl { get; }
}

public class LanguageInfo
{
    public string CultureName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string NativeName { get; set; } = string.Empty;
    public string EnglishName { get; set; } = string.Empty;
    public bool IsRtl { get; set; }
}

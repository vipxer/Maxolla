using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaManager.Core.Interfaces;

namespace OllamaManager.Infrastructure.Localization;

public class JsonLocalizationService : ILocalizationService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<JsonLocalizationService> _logger;
    private readonly string _localesDir;
    private readonly Dictionary<string, Dictionary<string, string>> _translations = new(StringComparer.OrdinalIgnoreCase);
    private CultureInfo _current;
    public CultureInfo CurrentCulture => _current;
    public event EventHandler<CultureInfo>? LanguageChanged;

    private static readonly List<LanguageInfo> _languages = new()
    {
        new LanguageInfo { CultureName = "zh-CN", DisplayName = "简体中文", NativeName = "简体中文", EnglishName = "Simplified Chinese", IsRtl = false },
        new LanguageInfo { CultureName = "zh-TW", DisplayName = "繁體中文", NativeName = "繁體中文", EnglishName = "Traditional Chinese", IsRtl = false },
        new LanguageInfo { CultureName = "en-US", DisplayName = "English", NativeName = "English", EnglishName = "English", IsRtl = false },
        new LanguageInfo { CultureName = "ja-JP", DisplayName = "日本語", NativeName = "日本語", EnglishName = "Japanese", IsRtl = false },
        new LanguageInfo { CultureName = "ko-KR", DisplayName = "한국어", NativeName = "한국어", EnglishName = "Korean", IsRtl = false },
        new LanguageInfo { CultureName = "ru-RU", DisplayName = "Русский", NativeName = "Русский", EnglishName = "Russian", IsRtl = false },
        new LanguageInfo { CultureName = "es-ES", DisplayName = "Español", NativeName = "Español", EnglishName = "Spanish", IsRtl = false },
        new LanguageInfo { CultureName = "ar-SA", DisplayName = "العربية", NativeName = "العربية", EnglishName = "Arabic", IsRtl = true }
    };

    public IReadOnlyList<LanguageInfo> SupportedLanguages => _languages;
    public bool IsRtl => _current.TextInfo.IsRightToLeft;
    public string this[string key] => GetString(key);

    public JsonLocalizationService(ISettingsService settingsService, ILogger<JsonLocalizationService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
        _localesDir = Path.Combine(AppContext.BaseDirectory, "Locales");
        LoadAllTranslations();

        var requested = _settingsService.Current.Language;
        _current = ResolveCulture(requested);
    }

    private void LoadAllTranslations()
    {
        try
        {
            if (Directory.Exists(_localesDir))
            {
                foreach (var file in Directory.GetFiles(_localesDir, "*.json"))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    try
                    {
                        var json = File.ReadAllText(file);
                        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                        if (dict != null)
                        {
                            _translations[name] = dict;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load locale {File}", file);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load translations");
        }
    }

    public string GetString(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        if (_translations.TryGetValue(_current.Name, out var currentDict) && currentDict.TryGetValue(key, out var v))
        {
            return v;
        }
        var fallbackName = "en-US";
        if (_translations.TryGetValue(fallbackName, out var en) && en.TryGetValue(key, out var ev))
        {
            return ev;
        }
        return key;
    }

    public string GetString(string key, params object[] args)
    {
        var fmt = GetString(key);
        try
        {
            return string.Format(_current, fmt, args);
        }
        catch
        {
            return fmt;
        }
    }

    public void SetLanguage(string cultureName)
    {
        var ci = ResolveCulture(cultureName);
        SetLanguage(ci);
    }

    public void SetLanguage(CultureInfo culture)
    {
        _current = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        try { _ = _settingsService.UpdateAsync(s => s.Language = culture.Name); } catch { }
        LanguageChanged?.Invoke(this, culture);
    }

    private static CultureInfo ResolveCulture(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return CultureInfo.GetCultureInfo("en-US");
        try { return CultureInfo.GetCultureInfo(name); }
        catch { return CultureInfo.GetCultureInfo("en-US"); }
    }
}

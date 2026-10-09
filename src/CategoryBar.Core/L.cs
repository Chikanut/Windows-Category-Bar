using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace CategoryBar.Core;

/// <summary>
/// Interface texts. The source language is Ukrainian: the text in the code is the key, and each other
/// language is a table from that text to its translation in Languages/&lt;code&gt;.json.
/// </summary>
public static class L
{
    public const string Source = "uk";

    public static readonly (string Code, string Name)[] Languages =
    [
        ("uk", "Українська"), ("en", "English"), ("de", "Deutsch"), ("es", "Español"),
        ("fr", "Français"), ("pt", "Português"), ("zh", "简体中文"), ("ja", "日本語")
    ];

    private static Dictionary<string, string> table = [];

    /// <summary>The language in use, as a code from <see cref="Languages"/>.</summary>
    public static string Current { get; private set; } = Source;

    /// <summary>Turns a setting ("System" or a language code) into a supported language code.</summary>
    public static string Resolve(string? setting)
    {
        if (Languages.Any(l => l.Code == setting)) return setting!;
        var system = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return Languages.Any(l => l.Code == system) ? system : "en";
    }

    public static void Use(string? setting)
    {
        Current = Resolve(setting);
        table = Current == Source ? [] : Load(Current);
    }

    /// <summary>The translation table of a language; empty when it has none.</summary>
    public static Dictionary<string, string> Load(string code)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CategoryBar.Core.Languages." + code + ".json");
        return stream is null ? [] : JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
    }

    /// <summary>
    /// The text in the current language. A text without a translation is shown as written. Two meanings that
    /// share one Ukrainian word are told apart by a mark after "##", which is never shown.
    /// </summary>
    public static string T(string text)
    {
        if (table.TryGetValue(text, out var translated)) return translated;
        var mark = text.IndexOf("##", StringComparison.Ordinal);
        return mark < 0 ? text : text[..mark];
    }

    /// <summary>A text with {0}, {1}… placeholders.</summary>
    public static string F(string text, params object?[] values) => string.Format(T(text), values);

    /// <summary>
    /// A text that depends on a number. The forms are separated by "|" in the order the language needs:
    /// one|few|many for Ukrainian, one|other for most languages, a single form for Chinese and Japanese.
    /// {0} is the number; further placeholders take <paramref name="values"/>.
    /// </summary>
    public static string N(int count, string forms, params object?[] values)
    {
        var options = T(forms).Split('|');
        return string.Format(options[Math.Min(PluralForm(Current, count), options.Length - 1)], [count, .. values]);
    }

    public static int PluralForm(string language, int count)
    {
        var n = Math.Abs(count);
        switch (language)
        {
            case "uk":
                var tens = n % 100; var ones = n % 10;
                return tens is > 10 and < 20 ? 2 : ones == 1 ? 0 : ones is >= 2 and <= 4 ? 1 : 2;
            case "zh" or "ja": return 0;
            case "fr" or "pt": return n <= 1 ? 0 : 1;
            default: return n == 1 ? 0 : 1;
        }
    }
}

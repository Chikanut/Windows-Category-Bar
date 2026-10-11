using System.Text.Json;
using System.Text.Json.Serialization;

namespace CategoryBar.Core;

/// <summary>Portable visual package: palettes, layout templates, transitions and compiled material shaders.</summary>
public sealed class ThemeDefinition
{
    public int Version { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string Material { get; set; } = "Solid";
    public string Backdrop { get; set; } = "None";
    public string FontName { get; set; } = Settings.DefaultFont;
    public int CornerRadius { get; set; } = 10;
    /// <summary>Multiplies every radius of the stylesheet; an export stores the user's corner setting here.</summary>
    public double RadiusScale { get; set; } = 1;
    public double SurfaceOpacity { get; set; } = .96;
    public double BorderWidth { get; set; } = 1;
    public string StyleSheet { get; set; } = "";
    public string StyleSheetFile { get; set; } = "";
    public string StyleResources { get; set; } = "";
    public string StyleResourcesFile { get; set; } = "";
    public Dictionary<string, string> Shaders { get; set; } = [];
    public Dictionary<string, string> ShaderFiles { get; set; } = [];
    public Dictionary<string, string> Dark { get; set; } = [];
    public Dictionary<string, string> Light { get; set; } = [];
    public List<ThemeParameter> Parameters { get; set; } = [];

    public static readonly string[] Tokens =
        ["Bar.Bg", "Menu.Bg", "Popup.Bg", "Window.Bg", "Sidebar.Bg", "Card.Bg", "Control.Bg", "Text", "Text.Muted", "Text.Faint", "Surface.Border", "Accent"];

    public static bool IsValidId(string? id) => id is { Length: > 0 and <= 64 }
        && id.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')
        && id[0] != '-' && id[^1] != '-';

    /// <summary>The stylesheet with the theme's parameters resolved: the user's values, or the author's defaults.</summary>
    public ThemeStyleSheet Sheet(Settings? settings = null, string extraRules = "") => ThemeStyleSheet.Parse(
        extraRules.Length == 0 ? StyleSheet : StyleSheet + "\n" + extraRules,
        Parameters.ToDictionary(p => p.Id, p => settings is null ? p.Default : p.Resolve(settings, Id)));

    public void Validate()
    {
        if (Version != 1 || !IsValidId(Id) || string.IsNullOrWhiteSpace(Name) || Name.Length > 80
            || Author is null || Author.Length > 100 || Description is null || Description.Length > 300
            || string.IsNullOrWhiteSpace(FontName) || FontName.Length > 100
            || FontName.Any(c => char.IsControl(c) || c is ':' or '/' or '\\' or '#' or ',')
            || Material is not ("Solid" or "Glass") || Backdrop is not ("None" or "Acrylic" or "Mica") || CornerRadius is < 0 or > 16
            || !double.IsFinite(RadiusScale) || RadiusScale is < 0 or > 4
            || !double.IsFinite(SurfaceOpacity) || SurfaceOpacity is < .5 or > 1
            || !double.IsFinite(BorderWidth) || BorderWidth is < 0 or > 4 || StyleSheet is null)
            throw new InvalidDataException(L.T("Некоректні властивості теми."));
        if (Parameters is null || Parameters.Count > ThemeParameter.MaximumCount || Parameters.Any(p => p is null)
            || Parameters.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != Parameters.Count)
            throw new InvalidDataException("Invalid or duplicate theme parameters.");
        foreach (var parameter in Parameters) parameter.Validate();
        // A parameter used by the stylesheet must stay valid across its whole range, not only at its default.
        var defaults = Parameters.ToDictionary(p => p.Id, p => p.Default);
        _ = ThemeStyleSheet.Parse(StyleSheet, defaults);
        foreach (var parameter in Parameters.Where(p => StyleSheet.Contains("var(--p-" + p.Id + ")", StringComparison.Ordinal)))
            foreach (var extreme in parameter.Extremes())
                _ = ThemeStyleSheet.Parse(StyleSheet, new Dictionary<string, string>(defaults) { [parameter.Id] = extreme });
        ThemeMarkup.Validate(StyleResources);
        if (!ThemeMarkup.IsRelativeFile(StyleSheetFile, ".css", allowEmpty: true)) throw new InvalidDataException("Invalid CSS file.");
        if (Shaders is null || ShaderFiles is null || Shaders.Count + ShaderFiles.Count > 8 || !ThemeMarkup.IsRelativeFile(StyleResourcesFile, ".xaml", allowEmpty: true)) throw new InvalidDataException("Invalid theme resource files.");
        foreach (var (id, bytes) in Shaders)
        {
            if (!IsValidId(id) || bytes is null || bytes.Length > ThemeMarkup.MaximumShaderBytes * 2) throw new InvalidDataException("Invalid shader ID or bytecode.");
            try { ThemeMarkup.ValidateShader(Convert.FromBase64String(bytes)); }
            catch (FormatException ex) { throw new InvalidDataException("Invalid shader encoding.", ex); }
        }
        foreach (var (id, file) in ShaderFiles)
            if (!IsValidId(id) || !ThemeMarkup.IsRelativeFile(file, ".ps")) throw new InvalidDataException("Invalid shader file.");
        foreach (var palette in new[] { Dark, Light })
            if (palette is null || palette.Count != Tokens.Length || Tokens.Any(t => !palette.TryGetValue(t, out var c) || !Settings.IsHexColor(c)))
                throw new InvalidDataException(L.T("Тема має містити світлу й темну палітри з кольорами #RRGGBB."));
    }

    public ThemeDefinition Copy() => ThemeFiles.Parse(ThemeFiles.Serialize(this));
}

/// <summary>
/// The four designs that ship with the application. Each one is an ordinary file theme embedded from
/// ThemeStyles/&lt;id&gt;: a manifest, a stylesheet, templates and optional shaders, exactly what an author writes.
/// </summary>
public static class BuiltInThemes
{
    public static readonly string[] Ids = ["fluent", "glass", "expressive", "brutal"];
    public static IReadOnlyList<ThemeDefinition> All { get; } = Ids.Select(Load).ToArray();

    private static ThemeDefinition Load(string id)
    {
        var assembly = typeof(BuiltInThemes).Assembly;
        byte[] Bytes(string file)
        {
            // MSBuild names the resource with the platform's directory separator.
            using var stream = assembly.GetManifestResourceStream("ThemeStyles/" + id + "/" + file) ?? assembly.GetManifestResourceStream("ThemeStyles/" + id + "\\" + file)
                ?? throw new InvalidDataException("Missing built-in theme file: " + id + "/" + file);
            using var buffer = new MemoryStream(); stream.CopyTo(buffer); return buffer.ToArray();
        }
        var theme = ThemeFiles.Resolve(System.Text.Encoding.UTF8.GetString(Bytes("theme.json")).TrimStart('﻿'), Bytes);
        if (theme.Id != id) throw new InvalidDataException("Built-in theme ID does not match its folder: " + id);
        return theme;
    }
}

public static class ThemeFiles
{
    public const int MaximumBytes = 1024 * 1024;
    public const string Extension = ".categorybar-theme.json";
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 8,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static ThemeDefinition Parse(string json) { var theme = Deserialize(json); theme.Validate(); return theme; }

    private static ThemeDefinition Deserialize(string json)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaximumBytes)
            throw new InvalidDataException(L.T("Файл теми завеликий. Максимум — 1 МБ."));
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            void UniqueProperties(JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Array) { foreach (var item in element.EnumerateArray()) UniqueProperties(item); return; }
                if (element.ValueKind != JsonValueKind.Object) return;
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name)) throw new InvalidDataException(L.T("Некоректний JSON або непідтримуваний формат теми."));
                    UniqueProperties(property.Value);
                }
            }
            UniqueProperties(document.RootElement);
            return JsonSerializer.Deserialize<ThemeDefinition>(json, Options) ?? throw new InvalidDataException(L.T("Порожній файл теми."));
        }
        catch (JsonException ex) { throw new InvalidDataException(L.T("Некоректний JSON або непідтримуваний формат теми."), ex); }
    }

    public static string Serialize(ThemeDefinition theme) { theme.Validate(); return JsonSerializer.Serialize(theme, Options); }

    /// <summary>Turns a manifest that points at files into one self-contained theme. <paramref name="read"/> returns a file next to it.</summary>
    public static ThemeDefinition Resolve(string json, Func<string, byte[]> read)
    {
        // The stylesheet may use parameters and files that are only complete once everything is inlined.
        var theme = Deserialize(json);
        if (theme.StyleSheetFile is { Length: > 0 } sheet)
        {
            if (!string.IsNullOrEmpty(theme.StyleSheet)) throw new InvalidDataException("Use StyleSheet or StyleSheetFile, not both.");
            if (!ThemeMarkup.IsRelativeFile(sheet, ".css")) throw new InvalidDataException("Invalid CSS file.");
            theme.StyleSheet = System.Text.Encoding.UTF8.GetString(read(sheet)).TrimStart('﻿'); theme.StyleSheetFile = "";
        }
        if (theme.StyleResourcesFile is { Length: > 0 } markup)
        {
            if (!string.IsNullOrEmpty(theme.StyleResources)) throw new InvalidDataException("Use StyleResources or StyleResourcesFile, not both.");
            if (!ThemeMarkup.IsRelativeFile(markup, ".xaml")) throw new InvalidDataException("Invalid theme resource files.");
            theme.StyleResources = System.Text.Encoding.UTF8.GetString(read(markup)).TrimStart('﻿'); theme.StyleResourcesFile = "";
        }
        theme.Shaders ??= [];
        foreach (var (id, relative) in theme.ShaderFiles ?? [])
        {
            if (!ThemeMarkup.IsRelativeFile(relative, ".ps")) throw new InvalidDataException("Invalid shader file.");
            if (!theme.Shaders.TryAdd(id, Convert.ToBase64String(read(relative)))) throw new InvalidDataException("Duplicate shader ID.");
        }
        theme.ShaderFiles = [];
        theme.Validate(); return theme;
    }

    public static ThemeDefinition Read(string path)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        byte[] Load(string relative, int maximum)
        {
            var file = new FileInfo(Path.Combine(folder, relative));
            if (!file.Exists) throw new InvalidDataException("Theme file not found: " + relative);
            if (file.Length > maximum) throw new InvalidDataException(L.T("Файл теми завеликий. Максимум — 1 МБ."));
            return File.ReadAllBytes(file.FullName);
        }
        return Resolve(System.Text.Encoding.UTF8.GetString(Load(Path.GetFileName(path), MaximumBytes)).TrimStart('﻿'), relative => Load(relative,
            relative.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ? ThemeStyleSheet.MaximumLength * 4
            : relative.EndsWith(".ps", StringComparison.OrdinalIgnoreCase) ? ThemeMarkup.MaximumShaderBytes : ThemeMarkup.MaximumMarkupBytes));
    }

    public static void Write(string path, ThemeDefinition theme)
    {
        var json = Serialize(theme);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using var writer = new StreamWriter(stream, leaveOpen: true);
                writer.Write(json); writer.Flush(); stream.Flush(true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

/// <summary>The theme library belongs to the configuration directory, including portable installs.</summary>
public sealed class ThemeStore(string directory)
{
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    public List<string> LoadErrors { get; } = [];
    public List<ThemeDefinition> Custom { get; } = [];
    private readonly Dictionary<string, string> installedPaths = [];
    public IEnumerable<ThemeDefinition> All => BuiltInThemes.All.Concat(Custom);
    public ThemeDefinition Resolve(string id) => All.FirstOrDefault(t => t.Id == id) ?? BuiltInThemes.All[0];
    public bool IsBuiltIn(string id) => BuiltInThemes.All.Any(t => t.Id == id);

    public void Load()
    {
        Custom.Clear(); LoadErrors.Clear(); installedPaths.Clear();
        if (!Directory.Exists(DirectoryPath)) return;
        string[] files;
        try { files = Directory.GetFiles(DirectoryPath, "*" + ThemeFiles.Extension); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { LoadErrors.Add(ex.Message); return; }
        foreach (var file in files.Order(StringComparer.Ordinal))
        {
            try
            {
                var theme = ThemeFiles.Read(file);
                if (All.Any(t => t.Id == theme.Id)) throw new InvalidDataException(L.T("Ідентифікатор теми вже використовується."));
                Custom.Add(theme);
                installedPaths[theme.Id] = file;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { LoadErrors.Add(Path.GetFileName(file) + ": " + ex.Message); }
        }
    }

    public void Install(ThemeDefinition theme)
    {
        theme.Validate();
        if (All.Any(t => t.Id == theme.Id)) throw new InvalidDataException(L.T("Ідентифікатор теми вже використовується."));
        var copy = theme.Copy();
        Directory.CreateDirectory(DirectoryPath);
        ThemeFiles.Write(Path.Combine(DirectoryPath, copy.Id + ThemeFiles.Extension), copy);
        Custom.Add(copy);
        installedPaths[copy.Id] = Path.Combine(DirectoryPath, copy.Id + ThemeFiles.Extension);
    }

    public void Remove(string id)
    {
        if (IsBuiltIn(id)) throw new InvalidOperationException("Built-in themes cannot be removed.");
        var theme = Custom.FirstOrDefault(t => t.Id == id);
        if (theme is null) return;
        // Imported IDs cannot escape the owned theme directory.
        File.Delete(installedPaths[theme.Id]);
        Custom.Remove(theme);
        installedPaths.Remove(theme.Id);
    }
}

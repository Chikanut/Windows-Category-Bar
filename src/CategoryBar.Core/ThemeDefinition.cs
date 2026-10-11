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

    public void Validate()
    {
        if (Version != 1 || !IsValidId(Id) || string.IsNullOrWhiteSpace(Name) || Name.Length > 80
            || Author is null || Author.Length > 100 || Description is null || Description.Length > 300
            || string.IsNullOrWhiteSpace(FontName) || FontName.Length > 100
            || FontName.Any(c => char.IsControl(c) || c is ':' or '/' or '\\' or '#' or ',')
            || Material is not ("Solid" or "Glass") || Backdrop is not ("None" or "Acrylic" or "Mica") || CornerRadius is < 0 or > 16
            || !double.IsFinite(SurfaceOpacity) || SurfaceOpacity is < .5 or > 1
            || !double.IsFinite(BorderWidth) || BorderWidth is < 0 or > 3)
            throw new InvalidDataException(L.T("Некоректні властивості теми."));
        _ = ThemeStyleSheet.Parse(StyleSheet);
        if (Parameters is null || Parameters.Count > 24 || Parameters.Any(p => p is null)
            || Parameters.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != Parameters.Count)
            throw new InvalidDataException("Invalid or duplicate theme parameters.");
        foreach (var parameter in Parameters) parameter.Validate();
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

public static class BuiltInThemes
{
    public static IReadOnlyList<ThemeDefinition> All { get; } = Create();
    private static ThemeDefinition[] Create()
    {
        static Dictionary<string, string> Palette(params string[] colors) => ThemeDefinition.Tokens.Zip(colors).ToDictionary(p => p.First, p => p.Second);
        ThemeDefinition[] themes =
        [
            new() { Id = "fluent", Name = "Fluent", Description = "Спокійні поверхні й знайомий стиль Windows.",
                Dark = Palette("#26262C", "#24242A", "#2C2C32", "#1B1B1E", "#202024", "#26262B", "#2E2E34", "#ECECEF", "#A6A6B0", "#9797A2", "#414148", "#8EA8FF"),
                Light = Palette("#F4F4F7", "#FAFAFC", "#FFFFFF", "#F3F3F5", "#E9E9EE", "#FFFFFF", "#FFFFFF", "#1C1C1F", "#55555E", "#686872", "#D1D1D9", "#365EC9") },
            new() { Id = "glass", Name = "Glass", Description = "Напівпрозоре скло, світлі контури й м’які відблиски.", Material = "Glass", Backdrop = "Acrylic", CornerRadius = 16, SurfaceOpacity = .64,
                Dark = Palette("#26313B", "#28333D", "#303B45", "#19232D", "#202B35", "#2A3540", "#34404C", "#F4F9FF", "#BBCDDF", "#A7BED4", "#68839D", "#80D8F7"),
                Light = Palette("#E6F3FC", "#EFF8FF", "#F6FBFF", "#EDF5FA", "#E2EEF6", "#F6FBFF", "#FFFFFF", "#172D42", "#405B73", "#526E86", "#A0B7CC", "#006899") },
            new() { Id = "expressive", Name = "Expressive", Description = "Виразні бузкові тони й округлі кольорові поверхні.", CornerRadius = 16, SurfaceOpacity = 1,
                Dark = Palette("#302A3D", "#292334", "#362D43", "#1D1825", "#251F30", "#30283C", "#3D334C", "#F2EAFB", "#C8BDD6", "#B5A7C7", "#62566F", "#D2B8FF"),
                Light = Palette("#EDE3FC", "#F4EDFC", "#FFFBFF", "#F7F2FC", "#EDE4F5", "#EEE5F8", "#FFFBFF", "#261C34", "#5D4D6D", "#6E5D7E", "#BDAFCC", "#7140AE") },
            new() { Id = "midnight", Name = "Midnight", Description = "Графітові поверхні, чіткі контури й м’ятний акцент.", CornerRadius = 6, SurfaceOpacity = 1, BorderWidth = 1.5,
                Dark = Palette("#191F24", "#171D22", "#222A31", "#101519", "#151B20", "#1B2329", "#232D35", "#EEF5F6", "#ADBFC5", "#9AACB5", "#455762", "#73E3C0"),
                Light = Palette("#E6EEEF", "#F5F9F9", "#FFFFFF", "#EDF3F3", "#E0EAEA", "#FFFFFF", "#F8FBFB", "#172C2C", "#435F60", "#536D6E", "#A0B7B8", "#006B51") }
        ];
        foreach (var theme in themes)
        {
            theme.StyleSheet = ThemeStyleSheet.BuiltIn(theme.Id);
            using var markup = typeof(BuiltInThemes).Assembly.GetManifestResourceStream("CategoryBar.Core.ThemeStyles." + theme.Id + ".xaml")!;
            using var reader = new StreamReader(markup);
            theme.StyleResources = reader.ReadToEnd();
            using var parameters = typeof(BuiltInThemes).Assembly.GetManifestResourceStream("CategoryBar.Core.ThemeStyles." + theme.Id + ".parameters.json")!;
            theme.Parameters = JsonSerializer.Deserialize<List<ThemeParameter>>(parameters)!;
            if (theme.Id == "glass")
            {
                using var shader = typeof(BuiltInThemes).Assembly.GetManifestResourceStream("CategoryBar.Core.ThemeStyles.liquid.ps")!;
                using var buffer = new MemoryStream(); shader.CopyTo(buffer);
                theme.Shaders["liquid"] = Convert.ToBase64String(buffer.ToArray());
            }
        }
        return themes;
    }
}

public static class ThemeFiles
{
    public const int MaximumBytes = 1024 * 1024;
    public const string Extension = ".categorybar-theme.json";
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 8
    };

    public static ThemeDefinition Parse(string json)
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
            var theme = JsonSerializer.Deserialize<ThemeDefinition>(json, Options)
                ?? throw new InvalidDataException(L.T("Порожній файл теми."));
            theme.Validate();
            return theme;
        }
        catch (JsonException ex) { throw new InvalidDataException(L.T("Некоректний JSON або непідтримуваний формат теми."), ex); }
    }

    public static string Serialize(ThemeDefinition theme) { theme.Validate(); return JsonSerializer.Serialize(theme, Options); }
    public static ThemeDefinition Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumBytes) throw new InvalidDataException(L.T("Файл теми завеликий. Максимум — 1 МБ."));
        using var reader = new StreamReader(stream);
        var theme = Parse(reader.ReadToEnd());
        var folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        if (theme.StyleSheetFile.Length > 0)
        {
            if (theme.StyleSheet.Length > 0) throw new InvalidDataException("Use StyleSheet or StyleSheetFile, not both.");
            var file = Path.Combine(folder, theme.StyleSheetFile);
            if (new FileInfo(file).Length > 12000) throw new InvalidDataException("Theme stylesheet is too large.");
            theme.StyleSheet = File.ReadAllText(file); theme.StyleSheetFile = "";
        }
        if (theme.StyleResourcesFile.Length > 0)
        {
            if (theme.StyleResources.Length > 0) throw new InvalidDataException("Use StyleResources or StyleResourcesFile, not both.");
            var file = Path.Combine(folder, theme.StyleResourcesFile);
            if (new FileInfo(file).Length > ThemeMarkup.MaximumMarkupBytes) throw new InvalidDataException("Theme markup is too large.");
            theme.StyleResources = File.ReadAllText(file); theme.StyleResourcesFile = "";
        }
        foreach (var (id, relative) in theme.ShaderFiles)
        {
            var file = Path.Combine(folder, relative);
            if (new FileInfo(file).Length > ThemeMarkup.MaximumShaderBytes) throw new InvalidDataException("Shader is too large.");
            if (!theme.Shaders.TryAdd(id, Convert.ToBase64String(File.ReadAllBytes(file)))) throw new InvalidDataException("Duplicate shader ID.");
        }
        theme.ShaderFiles.Clear(); theme.Validate(); return theme;
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

using System.Text.Json;
using System.Text.Json.Serialization;

namespace CategoryBar.Core;

public sealed class Configuration
{
    public int Version { get; set; } = 1;
    public List<Category> Categories { get; set; } = [];
    public Settings Settings { get; set; } = new();

    public void Validate()
    {
        if (Version != 1 || Categories is null || Settings is null)
            throw new InvalidDataException(L.T("Непідтримувана або пошкоджена конфігурація."));
        if (Categories.Count > 100 || Categories.Any(c => c is null || string.IsNullOrWhiteSpace(c.Name)
            || c.Name.Length > 100 || c.Shortcuts is null || c.Shortcuts.Any(s => !IsValid(s, nested: false))))
            throw new InvalidDataException(L.T("Некоректні категорії або ярлики."));
        if (Categories.Select(c => c.Id).Distinct().Count() != Categories.Count)
            throw new InvalidDataException(L.T("Повторювані ідентифікатори категорій."));
        var ids = Categories.SelectMany(c => c.Shortcuts).SelectMany(s => s.Steps.Prepend(s)).Select(s => s.Id).ToList();
        if (ids.Distinct().Count() != ids.Count)
            throw new InvalidDataException(L.T("Повторювані ідентифікатори ярликів."));
        Settings.Normalize();
    }

    /// <summary>A set has steps instead of a target; its steps are plain shortcuts.</summary>
    private static bool IsValid(Shortcut? s, bool nested) => s is not null && !string.IsNullOrWhiteSpace(s.Name)
        && s.Target is not null && s.Arguments is not null && s.WorkingDirectory is not null && s.Steps is not null
        && (s.Steps.Count == 0 ? !string.IsNullOrWhiteSpace(s.Target) : !nested && s.Steps.Count <= 30 && s.Steps.All(step => IsValid(step, nested: true)))
        && (s.Placement is null || s.Placement.Width > 0 && s.Placement.Height > 0);
}

public sealed class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public List<Shortcut> Shortcuts { get; set; } = [];
}

public sealed class Shortcut
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Target { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public bool RunAsAdministrator { get; set; }

    /// <summary>A set starts several applications at once. Its own target stays empty.</summary>
    public List<Shortcut> Steps { get; set; } = [];
    /// <summary>Where a set puts this application's window after starting it.</summary>
    public WindowPlacement? Placement { get; set; }
    [JsonIgnore] public bool IsSet => Steps.Count > 0;
}

/// <summary>A window rectangle in screen pixels.</summary>
public sealed class WindowPlacement
{
    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool Maximized { get; set; }
}

public sealed class Settings
{
    // First run
    public bool OnboardingCompleted { get; set; }
    public string Language { get; set; } = "";             // System | uk | en | de | es | fr | pt | zh | ja

    // Appearance
    public string ThemeId { get; set; } = "fluent";
    public Dictionary<string, Dictionary<string, string>> ThemeParameters { get; set; } = [];
    public Dictionary<string, Dictionary<string, string>> ThemeColors { get; set; } = [];
    public string ThemeMode { get; set; } = "System";      // System | Dark | Light
    public string AccentMode { get; set; } = "System";     // System | Theme | Custom
    public string AccentColor { get; set; } = "#8EA8FF";
    public bool TintSurfaces { get; set; }
    public string FontName { get; set; } = DefaultFont;
    public double InterfaceScale { get; set; } = 1;
    public double SurfaceOpacity { get; set; } = .96;
    public int CornerRadius { get; set; } = 10;

    // Bar and menus
    public bool MiniatureMode { get; set; }
    public int OpenDelayMs { get; set; } = 150;
    public int CloseDelayMs { get; set; } = 300;
    public bool ShowCounts { get; set; } = true;
    public bool ShowRunningIndicators { get; set; } = true;
    public bool ActivateRunning { get; set; } = true;
    public int SearchThreshold { get; set; } = 12;
    public string Alignment { get; set; } = "Windows";     // Windows | Left | Center | Right
    public bool AllMonitors { get; set; }
    public bool ReserveSpace { get; set; }
    public bool GridForLargeCategories { get; set; }

    public const string DefaultFont = "Segoe UI Variable Text";

    public void Normalize()
    {
        OpenDelayMs = Math.Clamp(OpenDelayMs, 0, 600);
        CloseDelayMs = Math.Clamp(CloseDelayMs, 0, 1000);
        SearchThreshold = Math.Clamp(SearchThreshold, 0, 100);
        CornerRadius = Math.Clamp(CornerRadius, 0, 16);
        InterfaceScale = double.IsFinite(InterfaceScale) ? Math.Clamp(InterfaceScale, .8, 1.5) : 1;
        SurfaceOpacity = double.IsFinite(SurfaceOpacity) ? Math.Clamp(SurfaceOpacity, .5, 1) : .96;
        // A configuration saved before languages existed belongs to someone used to the Ukrainian interface.
        if (Language != "System" && L.Languages.All(l => l.Code != Language)) Language = OnboardingCompleted ? L.Source : "System";
        if (ThemeMode is not ("System" or "Dark" or "Light")) ThemeMode = "System";
        if (AccentMode is not ("System" or "Theme" or "Custom")) AccentMode = "System";
        if (!ThemeDefinition.IsValidId(ThemeId)) ThemeId = "fluent";
        ThemeColors ??= [];
        ThemeColors = ThemeColors.Where(p => ThemeDefinition.IsValidId(p.Key) && p.Value is not null).Take(64)
            .ToDictionary(p => p.Key, p => p.Value.Where(v =>
                (v.Key.StartsWith("Dark:", StringComparison.Ordinal) || v.Key.StartsWith("Light:", StringComparison.Ordinal))
                && ThemeDefinition.Tokens.Contains(v.Key[(v.Key.IndexOf(':') + 1)..]) && IsHexColor(v.Value))
                .Take(ThemeDefinition.Tokens.Length * 2).ToDictionary(v => v.Key, v => v.Value.ToUpperInvariant()));
        ThemeParameters ??= [];
        ThemeParameters = ThemeParameters.Where(p => ThemeDefinition.IsValidId(p.Key) && p.Value is not null).Take(64)
            .ToDictionary(p => p.Key, p => p.Value.Where(v => ThemeDefinition.IsValidId(v.Key) && v.Value is { Length: <= 128 }).Take(24).ToDictionary(v => v.Key, v => v.Value));
        if (Alignment is not ("Windows" or "Left" or "Center" or "Right")) Alignment = "Windows";
        if (!IsHexColor(AccentColor)) AccentColor = "#8EA8FF";
        if (string.IsNullOrWhiteSpace(FontName)) FontName = DefaultFont;
    }

    public static bool IsHexColor(string? value) => value is { Length: 7 } && value[0] == '#'
        && value.Skip(1).All(Uri.IsHexDigit);
}

public sealed class ConfigurationStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string Path { get; } = System.IO.Path.GetFullPath(path);
    public string? RecoveryMessage { get; private set; }

    public Configuration Load()
    {
        if (!File.Exists(Path)) return new();
        foreach (var candidate in new[] { Path }.Concat(Enumerable.Range(1, 5).Select(i => Path + $".bak{i}")))
        {
            try
            {
                var config = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(candidate), Options)
                    ?? throw new InvalidDataException(L.T("Порожня конфігурація."));
                config.Validate();
                if (candidate != Path) RecoveryMessage = L.F("Налаштування відновлено з копії: {0}", candidate);
                return config;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException) { }
        }
        // Preserve the corrupt original; the UI asks the user to inspect the error.
        throw new InvalidDataException(L.F("Конфігурація та її резервні копії пошкоджені. Файли збережено: {0}", Path));
    }

    public void Save(Configuration config)
    {
        config.Validate();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temporary = Path + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, config, Options);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(Path))
            {
                for (var i = 5; i >= 2; i--)
                    if (File.Exists(Path + $".bak{i - 1}")) File.Copy(Path + $".bak{i - 1}", Path + $".bak{i}", true);
                // Do not rotate a corrupt primary file over a usable recovery copy.
                try
                {
                    var previous = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(Path));
                    previous?.Validate();
                    if (previous is not null) File.Copy(Path, Path + ".bak1", true);
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException) { }
                ReplaceWithRetry(temporary, Path);
            }
            else File.Move(temporary, Path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void ReplaceWithRetry(string temporary, string destination)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { File.Replace(temporary, destination, null); return; }
            // Windows scanners/readers can briefly deny deletion. These errors leave both names intact;
            // retry the atomic replacement, never fall back to deleting the user's configuration.
            catch (IOException ex) when (attempt < 4 && (ex.HResult & 0xffff) is 32 or 33 or 1175)
            { System.Threading.Thread.Sleep(25 * (attempt + 1)); }
        }
    }
}

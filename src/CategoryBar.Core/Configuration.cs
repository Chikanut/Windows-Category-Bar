using System.Text.Json;

namespace CategoryBar.Core;

public sealed class Configuration
{
    public int Version { get; set; } = 1;
    public List<Category> Categories { get; set; } = [];
    public Settings Settings { get; set; } = new();

    public void Validate()
    {
        if (Version != 1 || Categories is null || Settings is null)
            throw new InvalidDataException("Непідтримувана або пошкоджена конфігурація.");
        if (Categories.Count > 100 || Categories.Any(c => c is null || string.IsNullOrWhiteSpace(c.Name)
            || c.Name.Length > 100 || c.Shortcuts is null || c.Shortcuts.Any(s => s is null
                || string.IsNullOrWhiteSpace(s.Name) || string.IsNullOrWhiteSpace(s.Target)
                || s.Arguments is null || s.WorkingDirectory is null)))
            throw new InvalidDataException("Некоректні категорії або ярлики.");
        if (Categories.Select(c => c.Id).Distinct().Count() != Categories.Count)
            throw new InvalidDataException("Повторювані ідентифікатори категорій.");
        if (Categories.SelectMany(c => c.Shortcuts).Select(s => s.Id).Distinct().Count()
            != Categories.Sum(c => c.Shortcuts.Count))
            throw new InvalidDataException("Повторювані ідентифікатори ярликів.");
        Settings.OpenDelayMs = Math.Clamp(Settings.OpenDelayMs, 0, 600);
        Settings.CloseDelayMs = Math.Clamp(Settings.CloseDelayMs, 0, 1000);
    }
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
}

public sealed class Settings
{
    public int OpenDelayMs { get; set; } = 150;
    public int CloseDelayMs { get; set; } = 300;
    public bool ShowCounts { get; set; } = true;
    public int SearchThreshold { get; set; } = 15;
    public string Alignment { get; set; } = "Windows";
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
                    ?? throw new InvalidDataException("Порожня конфігурація.");
                config.Validate();
                if (candidate != Path) RecoveryMessage = "Налаштування відновлено з копії: " + candidate;
                return config;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException) { }
        }
        // Preserve the corrupt original; the UI asks the user to inspect the error.
        throw new InvalidDataException("Конфігурація та її резервні копії пошкоджені. Файли збережено: " + Path);
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
                File.Replace(temporary, Path, null);
            }
            else File.Move(temporary, Path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

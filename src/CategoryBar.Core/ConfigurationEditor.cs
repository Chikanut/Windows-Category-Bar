using System.Text.Json;

namespace CategoryBar.Core;

// Mutations and undo are transactional with respect to the persisted configuration.
public sealed class ConfigurationEditor(Configuration config, Action<Configuration> persist)
{
    private string? previous;
    public bool CanUndo => previous is not null;
    public void Change(Action change)
    {
        var before = JsonSerializer.Serialize(config);
        try { change(); config.Validate(); persist(config); previous = before; }
        catch { Restore(before); throw; }
    }
    public void Undo()
    {
        if (previous is null) return;
        var current = JsonSerializer.Serialize(config);
        var undo = previous;
        try { Restore(undo); persist(config); previous = null; }
        catch { Restore(current); throw; }
    }
    private void Restore(string json)
    {
        var restored = JsonSerializer.Deserialize<Configuration>(json)!;
        config.Categories = restored.Categories; config.Settings = restored.Settings;
    }

    public static void Move<T>(IList<T> items, T item, int destination)
    {
        var from = items.IndexOf(item);
        if (from < 0) throw new InvalidOperationException("Об’єкт більше не існує.");
        destination = Math.Clamp(destination, 0, items.Count - 1);
        if (from == destination) return;
        items.RemoveAt(from); items.Insert(destination, item);
    }
}

public static class LaunchTarget
{
    public static bool IsShellApplication(string target) => target.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase);
    public static bool IsLink(string target) => Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" or "steam";
    public static bool IsSupportedFile(string path) => Directory.Exists(path)
        || File.Exists(path) && Path.GetExtension(path).ToLowerInvariant() is ".exe" or ".lnk" or ".url";
    public static bool Exists(string target) => IsShellApplication(target) || IsLink(target) || File.Exists(target) || Directory.Exists(target);
}

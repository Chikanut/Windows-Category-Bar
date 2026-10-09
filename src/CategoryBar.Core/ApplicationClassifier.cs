namespace CategoryBar.Core;

public static class ApplicationClassifier
{
    public const string Fallback = "Програми";

    private static readonly (string Category, string[] Words)[] Rules =
    [
        ("Ігри", ["steam", "epic games", "gog galaxy", "battle.net", "ubisoft connect", "ea app", "riot client", "xbox", "minecraft", "roblox", "game launcher", "відеогра"]),
        ("Розробка", ["visual studio", "vscode", "vs code", "rider", "pycharm", "intellij", "webstorm", "android studio", "unity", "unreal", "godot", "github", "gitkraken", "sourcetree", "git bash", "terminal", "powershell", "docker", "postman", "cursor", "claude", "comfy", "ollama", "dbeaver"]),
        ("Браузери", ["chrome", "firefox", "msedge", "microsoft edge", "brave", "opera", "vivaldi", "tor browser"]),
        ("Спілкування", ["telegram", "discord", "slack", "whatsapp", "zoom", "teams", "skype", "signal", "viber", "thunderbird", "outlook"]),
        ("Медіа", ["photoshop", "illustrator", "figma", "blender", "vlc", "spotify", "audacity", "premiere", "after effects", "obs studio", "davinci", "resolve", "subtitle", "lightroom", "krita", "gimp", "inkscape", "handbrake", "foobar", "media player", "capcut", "affinity"]),
        ("Робота", ["winword", "word", "excel", "powerpoint", "libreoffice", "onenote", "notion", "obsidian", "acrobat", "trello", "todoist", "evernote"]),
        ("Системні", ["explorer", "провідник", "notepad", "блокнот", "calculator", "калькулятор", "settings", "налаштування", "control panel", "task manager", "regedit", "7-zip", "winrar", "total commander", "powertoys"])
    ];

    public static (string Category, string Source) Classify(string name, string target, string description)
    {
        var text = (name + " " + target + " " + description).ToLowerInvariant();
        if (text.Contains("steam://rungameid") || text.Contains("steamapps") || text.Contains("epic games\\"))
            return ("Ігри", "Шлях або посилання ігрової платформи");
        foreach (var rule in Rules)
            if (rule.Words.Any(text.Contains)) return (rule.Category, "Евристика за назвою та метаданими Windows");
        return (Fallback, "Тип не визначено");
    }
}

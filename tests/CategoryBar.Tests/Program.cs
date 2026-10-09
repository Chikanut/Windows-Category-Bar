using System.Text.Json;
using CategoryBar.Core;

var passed = 0;
void Check(string name, bool value)
{
    if (!value) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); passed++;
}

var normal = new TaskbarObservation(true, true, false, false, new(0, 1032, 1920, 1080), new(0, 0, 1920, 1080), false, true);
Check("Desktop / maximized window", VisibilityPolicy.ShouldShow(normal));
Check("Explorer absent", !VisibilityPolicy.ShouldShow(normal with { Exists = false }));
Check("Invisible taskbar", !VisibilityPolicy.ShouldShow(normal with { Visible = false }));
Check("Cloaked taskbar", !VisibilityPolicy.ShouldShow(normal with { Cloaked = true }));
Check("Auto-hide activation strip", !VisibilityPolicy.ShouldShow(normal with { AutoHide = true, Taskbar = new(0, 1078, 1920, 1126) }));
Check("Offscreen taskbar", !VisibilityPolicy.ShouldShow(normal with { AutoHide = true, Taskbar = new(0, 1080, 1920, 1128) }));
Check("Auto-hide revealed", VisibilityPolicy.ShouldShow(normal with { AutoHide = true }));
Check("Fullscreen covers taskbar", !VisibilityPolicy.ShouldShow(normal with { FullscreenForeground = true, TaskbarAboveForeground = false }));
Check("Taskbar revealed over fullscreen", VisibilityPolicy.ShouldShow(normal with { FullscreenForeground = true }));
Check("Negative monitor origin", VisibilityPolicy.ShouldShow(normal with { Taskbar = new(-1920, 1032, 0, 1080), Monitor = new(-1920, 0, 0, 1080) }));

var folder = Path.GetFullPath(Path.Combine(".local", "tests", "CategoryBarTests-" + Guid.NewGuid()));
Directory.CreateDirectory(folder);
try
{
    var path = Path.Combine(folder, "config.json");
    var store = new ConfigurationStore(path);
    Check("Fresh install is empty", store.Load().Categories.Count == 0);
    var config = new Configuration();
    config.Categories.Add(new Category { Name = "Розробка", Shortcuts = [new Shortcut { Name = "Термінал", Target = @"C:\Apps\tool.lnk", Arguments = "--test", WorkingDirectory = @"C:\Apps" }] });
    store.Save(config);
    Check("Unicode + launch metadata round trip", store.Load().Categories[0].Shortcuts[0].WorkingDirectory == @"C:\Apps");
    for (var i = 0; i < 7; i++) { config.Categories[0].Name = "Розробка " + i; store.Save(config); }
    Check("Five rolling backups", Directory.GetFiles(folder, "*.bak*").Length == 5);
    Check("No temporary file left", !File.Exists(path + ".tmp"));
    File.WriteAllText(path, "{broken");
    var recovered = store.Load();
    Check("Corruption recovers latest backup", recovered.Categories[0].Name == "Розробка 5" && store.RecoveryMessage is not null);
    store.Save(recovered);
    Check("Recovery survives next save", store.Load().Categories[0].Name == "Розробка 5");
    File.WriteAllText(path, JsonSerializer.Serialize(new Configuration { Version = 99 }));
    Check("Unsupported schema recovers backup", store.Load().Version == 1);
    recovered.Settings.OpenDelayMs = -1; recovered.Settings.CloseDelayMs = 5000;
    recovered.Validate();
    Check("Delay bounds", recovered.Settings.OpenDelayMs == 0 && recovered.Settings.CloseDelayMs == 1000);
    recovered.Categories[0].Name = " ";
    var rejected = false;
    try { store.Save(recovered); } catch (InvalidDataException) { rejected = true; }
    Check("Empty names cannot overwrite saved state", rejected);
}
finally
{
    // This uniquely created temporary directory is owned by this test run.
    Directory.Delete(folder, recursive: true);
}
var editable = new Configuration { Categories = [new Category { Name = "Початкова" }, new Category { Name = "Інша" }] };
var persisted = "";
var editor = new ConfigurationEditor(editable, c => persisted = JsonSerializer.Serialize(c));
editor.Change(() => editable.Categories[0].Shortcuts.Add(new Shortcut { Name = "Сайт", Target = "https://example.com", RunAsAdministrator = true }));
Check("Add is persisted and undoable", editor.CanUndo && persisted.Contains("example.com"));
editor.Undo(); Check("Undo restores additions", editable.Categories[0].Shortcuts.Count == 0 && !editor.CanUndo);
editor.Change(() => ConfigurationEditor.Move(editable.Categories, editable.Categories[0], 1));
Check("Category drag order persisted", editable.Categories[1].Name == "Початкова");
editor.Undo(); Check("Undo restores ordering", editable.Categories[0].Name == "Початкова");
var failingEditor = new ConfigurationEditor(editable, _ => throw new IOException("disk full"));
try { failingEditor.Change(() => editable.Categories.RemoveAt(0)); } catch (IOException) { }
Check("Failed save rolls back deletion", editable.Categories.Count == 2 && editable.Categories[0].Name == "Початкова");
Check("Web, Steam and Store launch targets", LaunchTarget.Exists("https://example.com") && LaunchTarget.Exists("steam://rungameid/1") && LaunchTarget.Exists(@"shell:AppsFolder\Package!App"));
Check("Reject arbitrary URL schemes", !LaunchTarget.IsLink("javascript:alert(1)") && !LaunchTarget.IsLink("file:///C:/example"));
Console.WriteLine($"{passed} checks passed.");

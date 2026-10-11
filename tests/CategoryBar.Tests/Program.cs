using System.Text.Json;
using System.Text.RegularExpressions;
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

    var retryPath = Path.Combine(folder, "retry-config.json");
    var retryStore = new ConfigurationStore(retryPath);
    var retryConfig = new Configuration { Categories = [new Category { Name = "Before" }] };
    retryStore.Save(retryConfig);
    using (var blocker = new FileStream(retryPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
    {
        var release = Task.Run(async () => { await Task.Delay(75); blocker.Dispose(); });
        retryConfig.Categories[0].Name = "After";
        retryStore.Save(retryConfig);
        release.GetAwaiter().GetResult();
    }
    Check("Saving tolerates a brief Windows delete-sharing lock", retryStore.Load().Categories[0].Name == "After");
    using (var blocker = new FileStream(retryPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
    {
        retryConfig.Categories[0].Name = "Must not replace";
        var failed = false;
        try { retryStore.Save(retryConfig); } catch (IOException) { failed = true; }
        Check("Persistent lock reports failure and retains the original configuration", failed && retryStore.Load().Categories[0].Name == "After" && !File.Exists(retryPath + ".tmp"));
    }
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
var sourceCategory = new Category { Name = "A", Shortcuts = Enumerable.Range(0, 4).Select(i => new Shortcut { Name = "Item " + i, Target = "target" + i }).ToList() };
var destinationCategory = new Category { Name = "B" };
var moved = sourceCategory.Shortcuts[0];
ConfigurationEditor.MoveShortcut(sourceCategory, sourceCategory, moved, 3);
Check("Drop below a later row accounts for removed source", sourceCategory.Shortcuts.Select(s => s.Name).SequenceEqual(new[] { "Item 1", "Item 2", "Item 0", "Item 3" }));
ConfigurationEditor.MoveShortcut(sourceCategory, destinationCategory, moved, 0);
Check("Cross-category drop preserves identity without duplication", sourceCategory.Shortcuts.Count == 3 && destinationCategory.Shortcuts.Single().Id == moved.Id);
Check("Steam path classifies games", ApplicationClassifier.Classify("Unknown", @"C:\Steam\steamapps\common\Example\game.exe", "").Category == "Ігри");
Check("Executable description helps classification", ApplicationClassifier.Classify("Unknown", "tool.exe", "Visual Studio Code").Category == "Розробка");
Check("Unknown application is not guessed as a game", ApplicationClassifier.Classify("Unrecognized", "app.exe", "").Category == "Програми");
var appearanceConfig = new Configuration { Settings = new Settings { InterfaceScale = double.NaN, SurfaceOpacity = 9, ThemeMode = "invalid", AccentColor = "red", FontName = "" } };
appearanceConfig.Validate();
Check("Appearance bounds and invalid values recover", appearanceConfig.Settings.InterfaceScale == 1 && appearanceConfig.Settings.SurfaceOpacity == 1 && appearanceConfig.Settings.ThemeMode == "System" && appearanceConfig.Settings.AccentColor == "#8EA8FF" && appearanceConfig.Settings.FontName == Settings.DefaultFont);
var legacy = JsonSerializer.Deserialize<Configuration>("""{"Version":1,"Categories":[],"Settings":{"Theme":"Blue","InitializedFromTaskbar":true},"Library":[]}""")!;
legacy.Validate();
Check("Legacy configuration loads and still gets onboarding", !legacy.Settings.OnboardingCompleted && legacy.Settings.ThemeMode == "System");
var changes = 0;
var notifyingEditor = new ConfigurationEditor(appearanceConfig, _ => { });
notifyingEditor.Changed += () => changes++;
notifyingEditor.Change(() => appearanceConfig.Settings.ShowCounts = false); notifyingEditor.Undo();
Check("Editor reports changes and undo", changes == 2 && appearanceConfig.Settings.ShowCounts);
notifyingEditor.ChangeQuietly(() => appearanceConfig.Settings.CornerRadius = 99);
Check("Quiet changes are validated and keep the undo step empty", appearanceConfig.Settings.CornerRadius == 16 && !notifyingEditor.CanUndo);
var sets = new Configuration { Categories = [new Category { Name = "Робота", Shortcuts = [new Shortcut { Name = "Ранок", Steps = [
    new Shortcut { Name = "Редактор", Target = "code.exe", Placement = new WindowPlacement { Left = -1920, Top = 0, Width = 960, Height = 1040 } },
    new Shortcut { Name = "Браузер", Target = "https://example.com" }] }] }] };
sets.Validate();
var setsBack = JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(sets))!;
setsBack.Validate();
var setBack = setsBack.Categories[0].Shortcuts[0];
Check("Set keeps its steps and window placement", setBack.IsSet && setBack.Steps.Count == 2 && setBack.Steps[0].Placement!.Left == -1920 && setBack.Steps[1].Placement is null);
bool Rejected(Action<Configuration> damage)
{
    var copy = JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(sets))!;
    damage(copy);
    try { copy.Validate(); return false; } catch (InvalidDataException) { return true; }
}
Check("Set inside a set is rejected", Rejected(c => c.Categories[0].Shortcuts[0].Steps[0].Steps.Add(new Shortcut { Name = "X", Target = "x.exe" })));
Check("Step without a target is rejected", Rejected(c => c.Categories[0].Shortcuts[0].Steps[1].Target = ""));
Check("Step sharing an identifier is rejected", Rejected(c => c.Categories[0].Shortcuts[0].Steps[1].Id = c.Categories[0].Shortcuts[0].Id));
Check("Empty window placement is rejected", Rejected(c => c.Categories[0].Shortcuts[0].Steps[0].Placement!.Width = 0));
// ---- Portable themes
foreach (var theme in BuiltInThemes.All) theme.Validate();
Check("Four built-in themes: the classic one and three new designs", BuiltInThemes.All.Select(t => t.Id).SequenceEqual(new[] { "fluent", "glass", "expressive", "brutal" }) && BuiltInThemes.All.Single(t => t.Id == "glass").Shaders.Count == 1);
Check("Distributable theme examples use the validated format", BuiltInThemes.All.All(t => ThemeFiles.Read(Path.Combine("Themes", "Examples", t.Id + ThemeFiles.Extension)).Id == t.Id));
var themeFolder = Path.GetFullPath(Path.Combine(".local", "tests", "Themes-" + Guid.NewGuid()));
Directory.CreateDirectory(themeFolder);
try
{
    var themes = new ThemeStore(themeFolder);
    var customTheme = BuiltInThemes.All[1].Copy(); customTheme.Id = "my-glass"; customTheme.Name = "Скло користувача";
    themes.Install(customTheme);
    customTheme.Dark["Text"] = "#000000";
    Check("Theme install stores an independent copy", themes.Resolve("my-glass").Dark["Text"] != "#000000");
    var reloaded = new ThemeStore(themeFolder); reloaded.Load();
    Check("Installed themes survive restart with both palettes", reloaded.Custom.Count == 1 && reloaded.Resolve("my-glass").Name == "Скло користувача" && reloaded.Resolve("my-glass").Light.Count == ThemeDefinition.Tokens.Length);
    Check("Missing theme falls back to Fluent", reloaded.Resolve("missing").Id == "fluent");
    bool ThemeRejected(Action action) { try { action(); return false; } catch (InvalidDataException) { return true; } }
    var css = ThemeStyleSheet.Parse(".theme { skin: glass; } .button { min-height: 50px; padding: 2px 8px 4px 10px; } .button { min-height: 44px; }");
    Check("Custom CSS overrides the shared defaults and later rules win", css.Number("button", "min-height", 0) == 44 && css.Number("menu", "width", 0) == 282 && css.Spacing("button", "padding", 0).SequenceEqual(new double[] { 2, 8, 4, 10 }));
    Check("CSS rejects unknown selectors, properties and external payloads", ThemeRejected(() => ThemeStyleSheet.Parse(".unknown { height: 30px; }")) && ThemeRejected(() => ThemeStyleSheet.Parse(".button { background: url(https://example.com); }")) && ThemeRejected(() => ThemeStyleSheet.Parse(".button { behavior: run; }")));
    Check("CSS rejects malformed and out-of-range declarations", ThemeRejected(() => ThemeStyleSheet.Parse(".button { min-height: 90px; }")) && ThemeRejected(() => ThemeStyleSheet.Parse(".button { padding: 1px 2px 3px; }")) && ThemeRejected(() => ThemeStyleSheet.Parse(".bar { offset: 80px; }")) && ThemeRejected(() => ThemeStyleSheet.Parse(".button { color: red } trailing")));
    var painted = ThemeStyleSheet.Parse(".card { background: linear-gradient(135deg, var(--card-bg), mix(var(--card-bg), var(--accent-fill), 0.2), rgba(40,80,120,0.5)); box-shadow: 4px 4px 0px alpha(var(--border), 0.8); } .button:hover { background: radial-gradient(#FFFFFF, transparent); color: var(--accent-fill-text); }");
    Check("File CSS supports gradients, colour functions, hard shadows and interactive states", ThemePaint.IsBrush(painted.Value("card", "background", "")) && ThemePaint.Shadow(painted.Value("card", "box-shadow", ""))?.Blur == 0 && painted.Has("button:hover", "color"));
    Check("Paint syntax rejects remote sources and unbounded effects", ThemeRejected(() => ThemeStyleSheet.Parse(".card { background: url(file:///secret); }")) && ThemeRejected(() => ThemeStyleSheet.Parse(".card { box-shadow: 0px 8px 500px #000000; }")) && ThemeRejected(() => ThemeStyleSheet.Parse(".card { background: mix(#000000, #FFFFFF, 7); }")));
    var living = ThemeStyleSheet.Parse(".bar { backdrop: wallpaper; backdrop-blur: 14px; } .menu { backdrop: live; }");
    Check("CSS lets a surface show the wallpaper behind it", living.Value("bar", "backdrop", "") == "wallpaper" && living.Number("bar", "backdrop-blur", 0) == 14
        && ThemeRejected(() => ThemeStyleSheet.Parse(".card { backdrop: live; }")) && ThemeRejected(() => ThemeStyleSheet.Parse(".bar { backdrop: screen; }")));
    var layout = ThemeStyleSheet.Parse(".bar { offset: 12px; } .menu { offset: 14px; } .tab { stagger: 40ms; } .sidebar { width: 240px; margin: 4px 8px; }");
    Check("CSS carries layout rules: floating bar, menu distance, navigation width and stagger", layout.Number("bar", "offset", 0) == 12 && layout.Number("menu", "offset", 0) == 14 && layout.Number("tab", "stagger", 0) == 40 && layout.Number("sidebar", "width", 0) == 240);
    var parametric = BuiltInThemes.All[0].Copy(); parametric.Id = "parametric";
    parametric.Parameters = [new ThemeParameter { Id = "radius", Label = "Radius", Type = "Number", Default = "12", Minimum = 0, Maximum = 30, Step = 1 }, new ThemeParameter { Id = "tone", Label = "Tone", Type = "Choice", Default = "accent", Options = new() { ["accent"] = "Accent", ["border"] = "Ink" } }];
    parametric.StyleSheet = ".card { border-radius: var(--p-radius); border-color: var(--var(--p-tone)); }";
    parametric.Validate();
    var tunedSheet = new Settings(); parametric.Parameters[0].Set(tunedSheet, parametric.Id, "20"); parametric.Parameters[1].Set(tunedSheet, parametric.Id, "border");
    Check("Stylesheet reads the theme's own parameters", parametric.Sheet().Number("card", "border-radius", 0) == 12 && parametric.Sheet(tunedSheet).Number("card", "border-radius", 0) == 20 && parametric.Sheet(tunedSheet).Value("card", "border-color", "") == "var(--border)");
    parametric.Parameters[0].Maximum = 90;
    Check("A parameter may not drive a property outside its range", ThemeRejected(parametric.Validate));
    parametric.Parameters[0].Maximum = 30; parametric.StyleSheet = ".card { border-radius: var(--p-missing); }";
    Check("Unknown stylesheet parameters are rejected", ThemeRejected(parametric.Validate));
    var dictionaryHeader = "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">";
    Check("Visual markup rejects providers, code directives and external dictionaries", ThemeRejected(() => ThemeMarkup.Validate(dictionaryHeader + "<ObjectDataProvider/></ResourceDictionary>")) && ThemeRejected(() => ThemeMarkup.Validate(dictionaryHeader.Replace("xmlns:x=", "x:Class=\"Code\" xmlns:x=") + "</ResourceDictionary>")) && ThemeRejected(() => ThemeMarkup.Validate(dictionaryHeader + "<ResourceDictionary Source=\"https://example.com/style.xaml\"/></ResourceDictionary>")));
    Check("Theme sidecars cannot escape their source directory", !ThemeMarkup.IsRelativeFile("../secret.xaml", ".xaml") && !ThemeMarkup.IsRelativeFile("C:/secret.ps", ".ps") && ThemeMarkup.IsRelativeFile("shaders/glow.ps", ".ps"));
    var aurora = ThemeFiles.Read(Path.Combine("Themes", "Examples", "aurora-motion", "theme.categorybar-theme.json"));
    Check("File theme resolves CSS, animated templates and shader sidecars", aurora.StyleResources.Contains("DoubleAnimation", StringComparison.Ordinal) && aurora.Shaders.Count == 1 && aurora.StyleSheetFile.Length == 0 && aurora.ShaderFiles.Count == 0);
    var portableAurora = ThemeFiles.Parse(ThemeFiles.Serialize(aurora));
    Check("Portable export includes templates and shader bytecode", portableAurora.StyleResources == aurora.StyleResources && portableAurora.Shaders["aurora"] == aurora.Shaders["aurora"]);
    Check("Built-in and duplicate IDs cannot be overwritten", ThemeRejected(() => reloaded.Install(BuiltInThemes.All[0])) && ThemeRejected(() => reloaded.Install(reloaded.Custom[0])));
    var bad = customTheme.Copy(); bad.Id = "../outside";
    Check("Theme IDs reject path traversal", ThemeRejected(bad.Validate) && !ThemeDefinition.IsValidId(null) && !ThemeDefinition.IsValidId("C:\\temp"));
    bad = customTheme.Copy(); bad.Light.Remove("Accent");
    Check("Incomplete palettes are rejected", ThemeRejected(bad.Validate));
    bad = customTheme.Copy(); bad.Dark["Accent"] = "red";
    Check("Theme colors accept only hex data", ThemeRejected(bad.Validate));
    bad = customTheme.Copy(); bad.FontName = "https://example.com/font/#Remote";
    Check("Theme fonts cannot reference remote or local resources", ThemeRejected(bad.Validate));
    bad = customTheme.Copy(); bad.SurfaceOpacity = double.NaN;
    Check("Invalid numeric theme properties are rejected", ThemeRejected(bad.Validate));
    var json = ThemeFiles.Serialize(reloaded.Custom[0]);
    Check("Unknown theme fields reject executable/XAML payloads", ThemeRejected(() => ThemeFiles.Parse(json.Replace("\"Version\": 1", "\"Version\": 1, \"Xaml\": \"payload\""))));
    Check("Duplicate JSON properties are rejected", ThemeRejected(() => ThemeFiles.Parse(json.Replace("\"Version\": 1", "\"Version\": 1, \"Version\": 1"))));
    Check("Future theme formats are rejected", ThemeRejected(() => ThemeFiles.Parse(json.Replace("\"Version\": 1", "\"Version\": 99"))));
    Check("Oversized theme files are rejected", ThemeRejected(() => ThemeFiles.Parse(new string(' ', ThemeFiles.MaximumBytes + 1))));
    File.WriteAllText(Path.Combine(themeFolder, "broken" + ThemeFiles.Extension), "{broken");
    reloaded.Load();
    Check("Corrupt custom theme does not prevent valid themes loading", reloaded.Custom.Count == 1 && reloaded.LoadErrors.Count == 1);
    var exportPath = Path.Combine(themeFolder, "export.json"); ThemeFiles.Write(exportPath, reloaded.Custom[0]);
    Check("Export/import preserves both palettes, CSS and geometry", ThemeFiles.Read(exportPath).Dark.SequenceEqual(reloaded.Custom[0].Dark) && ThemeFiles.Read(exportPath).CornerRadius == 16 && ThemeFiles.Read(exportPath).StyleSheet == reloaded.Custom[0].StyleSheet);
    var renamedTheme = Path.Combine(themeFolder, "renamed" + ThemeFiles.Extension);
    File.Move(Path.Combine(themeFolder, "my-glass" + ThemeFiles.Extension), renamedTheme);
    reloaded.Load(); reloaded.Remove("my-glass");
    Check("Removal also handles renamed theme files and preserves other files", !File.Exists(renamedTheme) && File.Exists(exportPath) && reloaded.All.Count() == 4);
    var themeSettings = JsonSerializer.Deserialize<Settings>("""{"ThemeId":"glass","AccentMode":"Theme"}""")!; themeSettings.Normalize();
    Check("Selected theme and its accent mode round-trip", themeSettings.ThemeId == "glass" && themeSettings.AccentMode == "Theme");
}
finally { Directory.Delete(themeFolder, recursive: true); }
// ---- Author-defined theme parameters
var parameterTheme = BuiltInThemes.All.Single(t => t.Id == "glass").Copy();
var refraction = parameterTheme.Parameters.Single(p => p.Id == "specular");
var tuned = new Settings(); refraction.Set(tuned, parameterTheme.Id, "0.045");
Check("Theme parameters survive configuration serialization", refraction.Resolve(JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(tuned))!, parameterTheme.Id) == "0.045");
Check("Theme parameter values are scoped by theme ID", refraction.Resolve(tuned, "another-theme") == refraction.Default);
tuned.ThemeParameters[parameterTheme.Id][refraction.Id] = "NaN";
Check("Invalid persisted parameter recovers to author default", refraction.Resolve(tuned, parameterTheme.Id) == refraction.Default);
Check("Numeric parameters reject culture-dependent and out-of-range values", !refraction.Accepts("0,04") && !refraction.Accepts("Infinity") && !refraction.Accepts("100"));
var parameterCopy = ThemeFiles.Parse(ThemeFiles.Serialize(parameterTheme));
Check("Parameter definitions round-trip in portable theme", parameterCopy.Parameters.Count == parameterTheme.Parameters.Count && parameterCopy.Parameters.Single(p => p.Id == refraction.Id).Step == refraction.Step && parameterCopy.Parameters[0].Group == refraction.Group);
bool ParameterRejected(Action action) { try { action(); return false; } catch (InvalidDataException) { return true; } }
parameterCopy.Parameters.Add(parameterCopy.Parameters[0]);
Check("Duplicate parameter IDs are rejected", ParameterRejected(parameterCopy.Validate));
parameterCopy = parameterTheme.Copy(); parameterCopy.Parameters[0].Default = "100";
Check("Out-of-range parameter defaults are rejected", ParameterRejected(parameterCopy.Validate));
parameterCopy = parameterTheme.Copy(); parameterCopy.Parameters[0].Step = 0;
Check("Invalid slider step is rejected", ParameterRejected(parameterCopy.Validate));
var choice = BuiltInThemes.All.Single(t => t.Id == "expressive").Parameters.Single(p => p.Id == "navigation");
Check("Choice values must be declared by the theme", choice.Accepts("left") && !choice.Accepts("missing"));
var sourcePackage = ThemeFiles.Read(Path.Combine("Themes", "Examples", "midnight", "theme.categorybar-theme.json"));
Check("A theme written for the first format still imports", sourcePackage.Parameters.Count == 3 && sourcePackage.StyleSheet.Contains("skin: studio"));
Check("Every built-in theme exposes grouped parameters and its own motion", BuiltInThemes.All.All(t => t.Parameters.Count >= 6 && t.Parameters.All(p => p.Group.Length > 0) && t.StyleResources.Contains("Theme.Menu.Enter")));
// ---- Languages
var translated = L.Languages.Select(l => l.Code).Where(code => code != L.Source).ToList();
var tables = translated.ToDictionary(code => code, L.Load);
Check("Seven languages besides the source have a table", translated.Count == 7 && tables.Values.All(t => t.Count > 0));
var literal = new Regex("""L\.(?:T|F)\(\s*"((?:[^"\\]|\\.)*)"|L\.N\([^"\n]*?,\s*"((?:[^"\\]|\\.)*)" """.TrimEnd());
var texts = Directory.EnumerateFiles("src", "*.cs", SearchOption.AllDirectories)
    .Where(file => !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !file.EndsWith("L.cs"))
    .SelectMany(file => literal.Matches(File.ReadAllText(file))).Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
    .Select(Regex.Unescape).Append(ApplicationClassifier.Fallback).Distinct().ToList();
texts.AddRange(BuiltInThemes.All.SelectMany(t => t.Parameters.SelectMany(p => new[] { p.Label, p.Description, p.Group }.Concat(p.Options.Values)).Append(t.Description)).Where(text => text.Length > 0).Distinct().Except(texts).ToList());
var untranslated = translated.SelectMany(code => texts.Where(text => !tables[code].ContainsKey(text)).Select(text => code + ": " + text)).ToList();
foreach (var gap in untranslated.Take(20)) Console.WriteLine("  missing " + gap);
Check("Every interface text is translated into every language", texts.Count > 250 && untranslated.Count == 0);
static string Holes(string text) => string.Join(",", Regex.Matches(text, @"\{\d\}").Select(m => m.Value).Distinct().Order());
var mismatched = tables.SelectMany(t => t.Value.Where(pair => Holes(pair.Key) != Holes(pair.Value)).Select(pair => t.Key + ": " + pair.Key)).ToList();
foreach (var gap in mismatched.Take(20)) Console.WriteLine("  placeholders " + gap);
Check("Translations keep the placeholders of the source", mismatched.Count == 0);
Check("Ukrainian plural forms", new[] { 1, 2, 5, 11, 21, 22, 25, 112 }.Select(n => L.PluralForm("uk", n)).SequenceEqual([0, 1, 2, 2, 0, 1, 2, 2]));
Check("Plural forms of the other languages", L.PluralForm("en", 1) == 0 && L.PluralForm("en", 0) == 1 && L.PluralForm("fr", 0) == 0 && L.PluralForm("fr", 2) == 1 && L.PluralForm("ja", 7) == 0);
L.Use("en");
Check("English texts, placeholders and plurals", L.T("Скасувати") == "Cancel" && L.T("Скасувати##undo") == "Undo" && L.F("Додано «{0}»", "Paint") == "Added “Paint”"
    && L.N(1, "Додано {0} ярлик|Додано {0} ярлики|Додано {0} ярликів") == "Added 1 shortcut" && L.N(5, "Додано {0} ярлик|Додано {0} ярлики|Додано {0} ярликів") == "Added 5 shortcuts");
L.Use("ja");
Check("A language with one plural form", L.N(3, "{0} вікно у наборі «{1}».|{0} вікна у наборі «{1}».|{0} вікон у наборі «{1}».", "朝") == "セット「朝」の 3 個のウィンドウ。");
L.Use(L.Source);
Check("Source language needs no table", L.T("Скасувати") == "Скасувати" && L.T("Скасувати##undo") == "Скасувати" && L.N(3, "Додано {0} ярлик|Додано {0} ярлики|Додано {0} ярликів") == "Додано 3 ярлики");
Check("Unknown language setting falls back to a supported one", L.Languages.Any(l => l.Code == L.Resolve("xx")) && L.Resolve("de") == "de");
var existingUser = new Configuration { Settings = new Settings { OnboardingCompleted = true } };
existingUser.Validate();
var newUser = new Configuration();
newUser.Validate();
Check("Existing installs keep Ukrainian, new ones follow Windows", existingUser.Settings.Language == "uk" && newUser.Settings.Language == "System");
Console.WriteLine($"{passed} checks passed.");

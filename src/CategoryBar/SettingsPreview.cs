using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CategoryBar.Core;

namespace CategoryBar;

// Offline rendering and integration checks: never changes the desktop or registry.
internal static class SettingsPreview
{
    internal static void RenderAndCheck(string outputFolder, string page)
    {
        outputFolder = Path.GetFullPath(outputFolder);
        Directory.CreateDirectory(outputFolder);
        var config = new Configuration();
        config.Categories.Add(new Category { Name = "Розробка" });
        config.Categories.Add(new Category { Name = "Ігри" });
        var store = new ConfigurationStore(Path.Combine(outputFolder, "preview-config.json"));
        var failSave = false;
        void Save()
        {
            if (failSave) throw new IOException("Тестова помилка запису.");
            store.Save(config);
        }
        var preview = new SettingsWindow(config, store.Path, Save, allowSystemChanges: false);
        preview.SelectPage(page);
        Render(preview, Path.Combine(outputFolder, page.ToLowerInvariant() + ".png"));
        preview.Close();
        if (page != "Panel") return;
        var window = new SettingsWindow(config, store.Path, Save, allowSystemChanges: false);
        window.SelectPage("Panel");
        ((Slider)window.FindName("Opening")).Value = 225;
        ((Slider)window.FindName("ClosingDelay")).Value = 450;
        ((CheckBox)window.FindName("Counts")).IsChecked = false;
        ((RadioButton)window.FindName("LeftAlignment")).IsChecked = true;
        ((RadioButton)window.FindName("AlwaysSearch")).IsChecked = true;
        if (!window.FlushChanges()) throw new InvalidOperationException("Settings did not flush.");
        var saved = store.Load().Settings;
        if (saved.OpenDelayMs != 225 || saved.CloseDelayMs != 450 || saved.ShowCounts || saved.Alignment != "Left" || saved.SearchThreshold != 0)
            throw new InvalidOperationException("Settings controls failed to persist their values.");
        failSave = true;
        ((CheckBox)window.FindName("Counts")).IsChecked = true;
        if (window.FlushChanges() || ((TextBlock)window.FindName("SaveError")).Visibility != Visibility.Visible)
            throw new InvalidOperationException("Settings write errors were not reported.");
        failSave = false;
        if (!window.FlushChanges() || !store.Load().Settings.ShowCounts)
            throw new InvalidOperationException("Settings did not recover from a write failure.");
        window.Close();
        File.WriteAllText(Path.Combine(outputFolder, "checks.txt"), "PASS: UI settings survive config reload.\nPASS: Save failure is visible and retry persists changes.\n");
    }

    private static void Render(SettingsWindow window, string path)
    {
        var visual = (FrameworkElement)window.Content;
        // A detached root has stable layout constraints without a live HWND.
        // Otherwise Window.UpdateLayout can re-arrange the manually measured content.
        window.Content = null;
        var size = new Size(820, 640);
        visual.InvalidateMeasure(); visual.InvalidateArrange();
        visual.Measure(size); visual.Arrange(new Rect(size)); visual.UpdateLayout();
        // Flush queued template/layout work before capturing the retained visual tree.
        visual.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        visual.UpdateLayout();
        var bitmap = new RenderTargetBitmap(820, 640, 96, 96, PixelFormats.Pbgra32);
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
            context.DrawRectangle(new VisualBrush(visual) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null, new Rect(size));
        bitmap.Render(drawing);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }

}

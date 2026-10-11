using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using CategoryBar;
using CategoryBar.Core;
using CategoryBar.Views;

internal static class MiniatureDragChecks
{
    internal static void Run(string configPath)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (var routedEvent in new[] { DragDrop.DragEnterEvent, DragDrop.DragOverEvent })
        foreach (var fileDrop in new[] { false, true })
        foreach (var allowed in new[] { DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link, DragDropEffects.Copy })
        {
            var config = new Configuration();
            config.Settings.MiniatureMode = true;
            config.Categories.Add(new Category { Name = "Drop here" });
            Theme.Apply(config.Settings);
            var session = new Session(config, new ConfigurationStore(configPath));
            // Native windows exist, but taskbar tracking and showing windows are disabled for this test.
            var bar = new BarWindow(session, trackTaskbar: false);
            try
            {
                var tabs = (StackPanel)typeof(BarWindow).GetField("tabs", flags)!.GetValue(bar)!;
                var handle = (ContentControl)tabs.Children[0];
                if (!handle.AllowDrop) throw new Exception("Miniature handle must receive OLE drag events.");
                var data = new DataObject();
                if (fileDrop) data.SetData(DataFormats.FileDrop, new[] { @"C:\Example\Application.lnk" });
                else data.SetText("unsupported plain text");
                var drag = (DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs), flags, null,
                    new object[] { data, DragDropKeyStates.LeftMouseButton, allowed, handle, new Point(1, 1) }, null)!;
                drag.RoutedEvent = routedEvent;
                handle.RaiseEvent(drag);
                var expanded = (bool)typeof(BarWindow).GetField("expanded", flags)!.GetValue(bar)!;
                var expected = !fileDrop ? DragDropEffects.None : allowed == DragDropEffects.Copy ? DragDropEffects.Copy : DragDropEffects.Link;
                if (expanded != fileDrop || drag.Effects != expected || !drag.Handled)
                    throw new Exception("Miniature drag reveal or drop feedback is incorrect.");
                if (config.Categories[0].Shortcuts.Count != 0)
                    throw new Exception("Hovering the handle must not add a shortcut before a category drop.");
                Console.WriteLine($"PASS: miniature {routedEvent.Name} ({allowed}) {(fileDrop ? "reveals categories for shortcuts" : "rejects unsupported data")}");
            }
            finally { bar.Close(); }
        }
    }
}

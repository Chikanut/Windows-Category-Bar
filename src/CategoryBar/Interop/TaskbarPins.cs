using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace CategoryBar.Interop;

/// <summary>
/// The list of icons pinned to the Windows taskbar. Windows offers applications no way to pin a shortcut, so
/// the list is edited where Explorer keeps it: the shortcut is copied to the user's pinned folder, an entry is
/// added to the Taskband registry values and the taskbar is told to read them again. The format is not
/// documented; it was taken from entries Explorer writes itself (Windows 11, build 26200). Everything here
/// is checked before it is written, and a list that does not look as expected is left untouched.
/// </summary>
internal static class TaskbarPins
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Taskband";
    internal static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar");

    [DllImport("shell32.dll")] private static extern uint ILGetSize(IntPtr list);
    [DllImport("ole32.dll")] private static extern int CreateStreamOnHGlobal(IntPtr memory, bool deleteOnRelease, out IStream stream);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr first, IntPtr second);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern void SHChangeNotify(int eventId, uint flags, string first, string? second);

    private sealed record PinList(List<byte[]> Entries, List<byte[]> Records, byte[] Favorites, int Changes);

    /// <summary>
    /// Favorites is a run of entries closed by 0xFF: one byte, the size of an item list, the item list.
    /// FavoritesResolve holds a saved shell link for every entry, each behind its size.
    /// </summary>
    private static PinList? Read(RegistryKey key)
    {
        if (key.GetValue("Favorites") is not byte[] favorites || favorites.Length == 0 || favorites[^1] != 0xFF) return null;
        var entries = new List<byte[]>();
        for (var offset = 0; offset < favorites.Length - 1;)
        {
            if (offset + 5 > favorites.Length - 1) return null;
            var size = BitConverter.ToInt32(favorites, offset + 1);
            if (size < 2 || size > favorites.Length || offset + 5 + size > favorites.Length - 1) return null;
            entries.Add(favorites[offset..(offset + 5 + size)]);
            offset += 5 + size;
        }
        var records = new List<byte[]>();
        if (key.GetValue("FavoritesResolve") is byte[] resolve)
            for (var offset = 0; offset + 4 <= resolve.Length && records.Count < entries.Count;)
            {
                var size = BitConverter.ToInt32(resolve, offset);
                if (size < 0 || offset + 4 + size > resolve.Length) break;
                records.Add(resolve[offset..(offset + 4 + size)]);
                offset += 4 + size;
            }
        // An entry without a saved link gets an empty one, so both lists stay in step.
        while (records.Count < entries.Count) records.Add(new byte[4]);
        return new(entries, records, favorites, key.GetValue("FavoritesChanges") as int? ?? 0);
    }

    private static int Find(byte[] data, byte[] pattern)
    {
        for (var i = 0; i + pattern.Length <= data.Length; i++)
        {
            var j = 0;
            while (j < pattern.Length && data[i + j] == pattern[j]) j++;
            if (j == pattern.Length) return i;
        }
        return -1;
    }

    private static bool Holds(byte[] entry, string appId) => Find(entry, Encoding.Unicode.GetBytes(appId + "\0")) >= 0;

    /// <summary>The application ids that start with <paramref name="prefix"/> and are pinned now.</summary>
    internal static HashSet<string> Pinned(string prefix)
    {
        var result = new HashSet<string>();
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            if (key is null || Read(key) is not { } list) return result;
            foreach (var entry in list.Entries) if (IdIn(entry, prefix) is { } id) result.Add(id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        return result;
    }

    private static string? IdIn(byte[] entry, string prefix)
    {
        var start = Find(entry, Encoding.Unicode.GetBytes(prefix));
        if (start < 0) return null;
        var end = start;
        while (end + 1 < entry.Length && (entry[end] != 0 || entry[end + 1] != 0)) end += 2;
        return Encoding.Unicode.GetString(entry, start, end - start);
    }

    /// <summary>The application id of every pin, ours or not.</summary>
    internal static HashSet<string> AllIds()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            if (key is null || Read(key) is not { } list) return result;
            byte[] signature = [0x1D, 0x00, 0xEF, 0xBE, 0x02, 0x00];
            foreach (var entry in list.Entries)
            {
                var start = Find(entry, signature);
                if (start < 0) continue;
                var end = start += signature.Length;
                while (end + 1 < entry.Length && (entry[end] != 0 || entry[end + 1] != 0)) end += 2;
                result.Add(Encoding.Unicode.GetString(entry, start, end - start));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        return result;
    }

    /// <summary>
    /// Moves the taskbar buttons of running applications to the end of the taskbar, in the given order. The
    /// taskbar puts a new pin after the buttons that are already there; this is what puts those buttons
    /// after the pins again, the way they are after signing in. A button moves only when all the windows
    /// behind it leave the taskbar together, so the windows come grouped by button.
    /// </summary>
    internal static void MoveToEnd(IEnumerable<IReadOnlyList<IntPtr>> groups)
    {
        object? instance = null;
        try
        {
            instance = new TaskbarListObject();
            var list = (ITaskbarList)instance;
            if (list.HrInit() < 0) return;
            foreach (var group in groups)
            {
                var windows = group.Where(Native.IsWindow).ToList();
                foreach (var window in windows) list.DeleteTab(window);
                Thread.Sleep(150);
                foreach (var window in windows) list.AddTab(window);
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException) { }
        finally { if (instance is not null) Marshal.FinalReleaseComObject(instance); }
    }

    /// <summary>An extra block of an item: size, version, signature, a string and the offset every block ends with.</summary>
    private static byte[] Block(uint signature, string text, byte[] tail)
    {
        var content = Encoding.Unicode.GetBytes(text + "\0");
        return [.. BitConverter.GetBytes((ushort)(12 + content.Length)), 0, 0, .. BitConverter.GetBytes(signature), 2, 0, .. content, .. tail];
    }

    /// <summary>
    /// The entry of a shortcut in the pinned folder: its item list, where the last item also names the
    /// application id (block 0xBEEF001D) and the kind of pin (0xBEEF001E), as Explorer writes them.
    /// </summary>
    private static (byte[] Entry, byte[] Record)? Build(string link, string appId)
    {
        var list = Native.ILCreateFromPathW(link);
        if (list == IntPtr.Zero) return null;
        byte[] items;
        try { items = new byte[ILGetSize(list)]; Marshal.Copy(list, items, 0, items.Length); }
        finally { Native.ILFree(list); }
        int last = -1, offset = 0;
        while (offset + 2 <= items.Length && BitConverter.ToUInt16(items, offset) is var size and not 0)
        {
            if (offset + size > items.Length - 2) return null;
            last = offset; offset += size;
        }
        if (last < 0 || offset != items.Length - 2 || offset - last < 4) return null;
        byte[] tail = [items[offset - 2], items[offset - 1]];
        byte[] item = [.. items[last..offset], .. Block(0xBEEF001D, appId, tail), .. Block(0xBEEF001E, "UserPinned", tail)];
        if (item.Length > ushort.MaxValue) return null;
        BitConverter.GetBytes((ushort)item.Length).CopyTo(item, 0);
        byte[] patched = [.. items[..last], .. item, 0, 0];
        return Record(patched) is { } record ? ([0, .. BitConverter.GetBytes(patched.Length), .. patched], record) : null;
    }

    /// <summary>A shell link to the item list, saved the way Explorer saves the ones it keeps beside the entries.</summary>
    private static byte[]? Record(byte[] items)
    {
        var instance = new ShellLinkObject();
        var memory = Marshal.AllocCoTaskMem(items.Length);
        try
        {
            Marshal.Copy(items, 0, memory, items.Length);
            var data = (IShellLinkDataList)instance;
            if (data.GetFlags(out var flags) < 0 || data.SetFlags(flags | 0x00800000) < 0) return null;
            if (((IShellLinkW)instance).SetIDList(memory) < 0) return null;
            if (CreateStreamOnHGlobal(IntPtr.Zero, true, out var stream) < 0) return null;
            if (((IPersistStream)instance).Save(stream, true) < 0) return null;
            stream.Stat(out var stat, 1);
            var bytes = new byte[stat.cbSize];
            stream.Seek(0, 0, IntPtr.Zero);
            stream.Read(bytes, bytes.Length, IntPtr.Zero);
            Marshal.ReleaseComObject(stream);
            return [.. BitConverter.GetBytes(bytes.Length), .. bytes];
        }
        finally { Marshal.FreeCoTaskMem(memory); Marshal.FinalReleaseComObject(instance); }
    }

    /// <summary>
    /// Makes the application ids in <paramref name="wanted"/> the first pins, pinning the shortcuts in
    /// <paramref name="add"/> on the way, and removes every other pin whose id starts with
    /// <paramref name="prefix"/>. Pins that are already there keep the order the user gave them, new ones follow
    /// in the order of <paramref name="wanted"/>, and <paramref name="last"/> closes the group. Pins of other
    /// applications keep their order. False when nothing could be done.
    /// </summary>
    internal static bool Arrange(string prefix, IReadOnlyList<string> wanted, IReadOnlyDictionary<string, string> add, string? last = null)
    {
        using var mutex = new Mutex(false, "Local\\WindowsCategoryBar.TaskbarPins");
        try { if (!mutex.WaitOne(5000)) return false; } catch (AbandonedMutexException) { }
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
                if (key is null || Read(key) is not { } list) return false;
                var own = new Dictionary<string, (byte[] Entry, byte[] Record)>();
                var created = new List<string>();
                var order = new List<string>();
                for (var i = 0; i < list.Entries.Count; i++)
                    if (IdIn(list.Entries[i], prefix) is { } id && own.TryAdd(id, (list.Entries[i], list.Records[i])) && wanted.Contains(id)) order.Add(id);
                foreach (var (id, source) in add)
                {
                    if (own.ContainsKey(id) || !File.Exists(source)) continue;
                    Directory.CreateDirectory(Folder);
                    var name = Path.GetFileNameWithoutExtension(source);
                    var copy = Path.Combine(Folder, name + ".lnk");
                    for (var suffix = 2; File.Exists(copy); suffix++) copy = Path.Combine(Folder, name + " (" + suffix + ").lnk");
                    File.Copy(source, copy);
                    created.Add(copy);
                    SHChangeNotify(0x2, 0x1005, copy, null); // SHCNE_CREATE, SHCNF_PATHW | SHCNF_FLUSH
                    if (Build(copy, id) is { } built && Holds(built.Entry, id)) own[id] = built;
                    else { File.Delete(copy); created.Remove(copy); }
                }

                order.AddRange(wanted.Where(id => own.ContainsKey(id) && !order.Contains(id)));
                if (last is not null && order.Remove(last)) order.Add(last);
                var entries = new List<byte[]>(); var records = new List<byte[]>();
                foreach (var id in order)
                    if (own.TryGetValue(id, out var pin)) { entries.Add(pin.Entry); records.Add(pin.Record); }
                for (var i = 0; i < list.Entries.Count; i++)
                    if (IdIn(list.Entries[i], prefix) is null) { entries.Add(list.Entries[i]); records.Add(list.Records[i]); }

                byte[] favorites = [.. entries.SelectMany(e => e), 0xFF];
                if (favorites.AsSpan().SequenceEqual(list.Favorites)) return true;
                // Explorer or the user may have changed the list meanwhile: start again from what is there now.
                if (key.GetValue("Favorites") is not byte[] now || !now.AsSpan().SequenceEqual(list.Favorites))
                {
                    foreach (var copy in created) File.Delete(copy);
                    continue;
                }
                key.SetValue("Favorites", favorites, RegistryValueKind.Binary);
                key.SetValue("FavoritesResolve", records.SelectMany(r => r).ToArray(), RegistryValueKind.Binary);
                key.SetValue("FavoritesChanges", unchecked(list.Changes + 1), RegistryValueKind.DWord);
                return Reload();
            }
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or COMException or InvalidCastException) { return false; }
        finally { try { mutex.ReleaseMutex(); } catch (ApplicationException) { } }
    }

    /// <summary>Asks the taskbar to read its pin list again.</summary>
    private static bool Reload()
    {
        var band = Native.FindWindowEx(Native.FindWindow("Shell_TrayWnd", null), IntPtr.Zero, "ReBarWindow32", null);
        var tasks = band == IntPtr.Zero ? IntPtr.Zero : Native.FindWindowEx(band, IntPtr.Zero, "MSTaskSwWClass", null);
        return tasks != IntPtr.Zero && PostMessage(tasks, 0x446, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>Tells the shell that a pinned shortcut changed, so that the taskbar redraws its icon.</summary>
    internal static void Refresh(string link)
    {
        SHChangeNotify(0x1, 0x1005, link, link);    // SHCNE_RENAMEITEM onto itself
        SHChangeNotify(0x2000, 0x1005, link, null); // SHCNE_UPDATEITEM
    }
}

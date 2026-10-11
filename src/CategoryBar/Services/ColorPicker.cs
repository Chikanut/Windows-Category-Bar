using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace CategoryBar.Services;

internal static class ColorPicker
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ChooseColorData
    {
        internal int Size;
        internal IntPtr Owner, Instance;
        internal uint Result;
        internal IntPtr CustomColors;
        internal uint Flags;
        internal IntPtr Data, Hook, Template;
    }

    [DllImport("comdlg32.dll", EntryPoint = "ChooseColorW", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChooseColor(ref ChooseColorData data);

    internal static Color? Show(Window owner, Color initial)
    {
        var custom = Marshal.AllocHGlobal(16 * sizeof(int));
        try
        {
            for (var i = 0; i < 16; i++) Marshal.WriteInt32(custom, i * sizeof(int), 0x00FFFFFF);
            var data = new ChooseColorData {
                Size = Marshal.SizeOf<ChooseColorData>(), Owner = new WindowInteropHelper(owner).Handle,
                Result = (uint)(initial.R | initial.G << 8 | initial.B << 16), CustomColors = custom,
                Flags = 0x00000001 | 0x00000002 // CC_RGBINIT | CC_FULLOPEN
            };
            return ChooseColor(ref data) ? Color.FromRgb((byte)data.Result, (byte)(data.Result >> 8), (byte)(data.Result >> 16)) : null;
        }
        finally { Marshal.FreeHGlobal(custom); }
    }
}

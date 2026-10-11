using System.Runtime.InteropServices;

namespace CategoryBar.Interop;

// COM declarations for shortcuts that carry their own application id and for their Jump Lists.

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct PropertyKey(Guid format, uint id)
{
    public Guid Format = format;
    public uint Id = id;
    internal static readonly PropertyKey AppUserModelId = new(new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
    internal static readonly PropertyKey Title = new(new("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), 2);
}

/// <summary>A PROPVARIANT that only ever holds a string.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PropVariant
{
    public ushort Type, Reserved1, Reserved2, Reserved3;
    public IntPtr Value, Unused;

    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant value);

    internal static void Set(IPropertyStore store, PropertyKey key, string text)
    {
        var value = new PropVariant { Type = 31, Value = Marshal.StringToCoTaskMemUni(text) }; // VT_LPWSTR
        try { store.SetValue(ref key, ref value); store.Commit(); }
        finally { PropVariantClear(ref value); }
    }
}

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
internal interface IPropertyStore
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int GetAt(uint index, out PropertyKey key);
    [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
    [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
    [PreserveSig] int Commit();
}

[ComImport, Guid("77F10CF0-3DB5-4966-B520-B7C54FD35ED6")]
internal class DestinationListObject { }

[ComImport, Guid("2D3468C1-36A7-43B6-AC24-D3F02FD9607A")]
internal class ObjectCollectionObject { }

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9")]
internal interface IObjectArray
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int GetAt(uint index, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object item);
}

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("5632B1A4-E38A-400A-928A-D4CD63230295")]
internal interface IObjectCollection
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int GetAt(uint index, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object item);
    [PreserveSig] int AddObject([MarshalAs(UnmanagedType.Interface)] object item);
    [PreserveSig] int AddFromArray([MarshalAs(UnmanagedType.Interface)] IObjectArray source);
    [PreserveSig] int RemoveObjectAt(uint index);
    [PreserveSig] int Clear();
}

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("6332DEBF-87B5-4670-90C0-5E57B408A49E")]
internal interface ICustomDestinationList
{
    [PreserveSig] int SetAppID([MarshalAs(UnmanagedType.LPWStr)] string appId);
    [PreserveSig] int BeginList(out uint slots, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object removed);
    [PreserveSig] int AppendCategory([MarshalAs(UnmanagedType.LPWStr)] string category, [MarshalAs(UnmanagedType.Interface)] IObjectArray items);
    [PreserveSig] int AppendKnownCategory(int category);
    [PreserveSig] int AddUserTasks([MarshalAs(UnmanagedType.Interface)] IObjectArray items);
    [PreserveSig] int CommitList();
    [PreserveSig] int GetRemovedDestinations(ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object removed);
    [PreserveSig] int DeleteList([MarshalAs(UnmanagedType.LPWStr)] string? appId);
    [PreserveSig] int AbortList();
}

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("45E2B4AE-B1C3-11D0-B92F-00A0C90312E1")]
internal interface IShellLinkDataList
{
    [PreserveSig] int AddDataBlock(IntPtr block);
    [PreserveSig] int CopyDataBlock(uint signature, out IntPtr block);
    [PreserveSig] int RemoveDataBlock(uint signature);
    [PreserveSig] int GetFlags(out uint flags);
    [PreserveSig] int SetFlags(uint flags);
}

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("00000109-0000-0000-C000-000000000046")]
internal interface IPersistStream
{
    [PreserveSig] int GetClassID(out Guid classId);
    [PreserveSig] int IsDirty();
    [PreserveSig] int Load(System.Runtime.InteropServices.ComTypes.IStream stream);
    [PreserveSig] int Save(System.Runtime.InteropServices.ComTypes.IStream stream, bool clearDirty);
    [PreserveSig] int GetSizeMax(out long size);
}

[ComImport, Guid("56FDF344-FD6D-11D0-958A-006097C9A090")]
internal class TaskbarListObject { }

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("56FDF342-FD6D-11D0-958A-006097C9A090")]
internal interface ITaskbarList
{
    [PreserveSig] int HrInit();
    [PreserveSig] int AddTab(IntPtr window);
    [PreserveSig] int DeleteTab(IntPtr window);
    [PreserveSig] int ActivateTab(IntPtr window);
    [PreserveSig] int SetActiveAlt(IntPtr window);
}

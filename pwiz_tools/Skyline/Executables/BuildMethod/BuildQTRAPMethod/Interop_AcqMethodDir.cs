using System.Runtime.InteropServices;

namespace BuildQTRAPMethod
{
    [InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
    [GuidAttribute("5D48D880-36AA-11D2-82F5-00104B1F7094")]
    [ComImportAttribute()]
    public interface IAcqMethodDirConfig
    {
        int CreateMethod([Out, MarshalAs(UnmanagedType.IUnknown)] out object method);
        int EnableMethodEditor([In, MarshalAs(UnmanagedType.IUnknown)] object server, [In] int state);
        int CreateNonUIMethod([Out, MarshalAs(UnmanagedType.IUnknown)] out object method);
        int LoadNonUIMethod([In, MarshalAs(UnmanagedType.BStr)] string filePath, [Out, MarshalAs(UnmanagedType.IUnknown)] out object method);
    }
}

using System.Runtime.InteropServices;

namespace WinSW.Plugins.SharedDirectoryMapper
{
    internal static class NativeMethods
    {
        internal const uint RESOURCETYPE_DISK = 0x00000001;

        private const string MprLibraryName = "mpr.dll";

        [DllImport(MprLibraryName, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "WNetAddConnection2W")]
        internal static extern int WNetAddConnection2(in NETRESOURCE netResource, string? password = null, string? userName = null, uint flags = 0);

        [DllImport(MprLibraryName, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "WNetCancelConnection2W")]
        internal static extern int WNetCancelConnection2(string name, uint flags = 0, bool force = false);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct NETRESOURCE
        {
            public uint Scope;
            public uint Type;
            public uint DisplayType;
            public uint Usage;
            public string LocalName;
            public string RemoteName;
            public string Comment;
            public string Provider;
        }
    }
}

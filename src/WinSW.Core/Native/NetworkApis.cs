#pragma warning disable SA1310 // Field names should not contain underscore

using System.Runtime.InteropServices;
using static WinSW.Native.Libraries;

namespace WinSW.Native
{
    internal static class NetworkApis
    {
        internal const uint RESOURCETYPE_DISK = 0x00000001;

        [DllImport(Mpr, SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int WNetAddConnection2W(in NETRESOURCEW netResource, string? password = null, string? userName = null, uint flags = 0);

        [DllImport(Mpr, SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int WNetCancelConnection2W(string name, uint flags = 0, bool force = false);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct NETRESOURCEW
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

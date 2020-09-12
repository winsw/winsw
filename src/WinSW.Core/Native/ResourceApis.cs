#pragma warning disable SA1310 // Field names should not contain underscore

using System;
using System.Runtime.InteropServices;

namespace WinSW.Native
{
    internal static class ResourceApis
    {
        internal static readonly IntPtr VS_VERSION_INFO = new(1);

        internal static readonly IntPtr RT_VERSION = new(16);

        [DllImport(Libraries.Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr BeginUpdateResourceW(string fileName, bool deleteExistingResources);

        [DllImport(Libraries.Kernel32, SetLastError = true)]
        internal static extern bool EndUpdateResourceW(IntPtr update, bool discard);

        [DllImport(Libraries.Kernel32, SetLastError = true)]
        internal static extern IntPtr FindResourceW(IntPtr module, IntPtr name, IntPtr type);

        [DllImport(Libraries.Kernel32)]
        internal static extern bool FreeLibrary(IntPtr libModule);

        [DllImport(Libraries.Kernel32, CharSet = CharSet.Unicode)]
        internal static extern IntPtr LoadLibraryW(string libFileName);

        [DllImport(Libraries.Kernel32, SetLastError = true)]
        internal static extern IntPtr LoadResource(IntPtr module, IntPtr resInfo);

        [DllImport(Libraries.Kernel32)]
        internal static extern IntPtr LockResource(IntPtr resData);

        [DllImport(Libraries.Kernel32, SetLastError = true)]
        internal static extern bool UpdateResourceW(IntPtr update, IntPtr type, IntPtr name, ushort language, IntPtr data, int size);
    }
}

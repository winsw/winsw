using System;
using System.Runtime.InteropServices;

namespace WinSW.Native
{
    internal static class HandleApis
    {
        [DllImport(Libraries.Kernel32)]
        internal static extern bool CloseHandle(IntPtr objectHandle);
    }
}

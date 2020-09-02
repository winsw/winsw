using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WinSW.Native
{
    internal static class Kernel32
    {
        [DllImport(Libraries.Kernel32)]
        internal static extern bool SetStdHandle(int stdHandle, SafeFileHandle handle);
    }
}

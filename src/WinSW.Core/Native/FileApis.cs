using System;
using System.IO;
using System.Runtime.InteropServices;

namespace WinSW.Native
{
    internal static class FileApis
    {
        [DllImport(Libraries.Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern Handle CreateFileW(
            string fileName,
            GenericAccess desiredAccess,
            FileShare shareMode,
            IntPtr securityAttributes,
            FileMode creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        internal enum GenericAccess : uint
        {
            Read = 0x80000000,
            Write = 0x40000000,
            Execute = 0x20000000,
            All = 0x10000000,
        }
    }
}

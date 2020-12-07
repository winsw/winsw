#if !NETCOREAPP
using System;
#endif
using System.IO;
#if !NETCOREAPP
using System.Runtime.InteropServices;
#endif

namespace WinSW.Util
{
    public static class FileHelper
    {
        public static void MoveOrReplaceFile(string sourceFileName, string destFileName)
        {
#if NET
            File.Move(sourceFileName, destFileName, true);
#else
            string sourceFilePath = Path.GetFullPath(sourceFileName);
            string destFilePath = Path.GetFullPath(destFileName);

            if (!NativeMethods.MoveFileEx(sourceFilePath, destFilePath, NativeMethods.MOVEFILE_REPLACE_EXISTING | NativeMethods.MOVEFILE_COPY_ALLOWED))
            {
                throw GetExceptionForLastWin32Error(sourceFilePath);
            }
#endif
        }
#if !NETCOREAPP

        private static Exception GetExceptionForLastWin32Error(string path) => Marshal.GetLastWin32Error() switch
        {
            2 => new FileNotFoundException(null, path), // ERROR_FILE_NOT_FOUND
            3 => new DirectoryNotFoundException(), // ERROR_PATH_NOT_FOUND
            5 => new UnauthorizedAccessException(), // ERROR_ACCESS_DENIED
            _ => new IOException()
        };

        private static class NativeMethods
        {
            internal const uint MOVEFILE_REPLACE_EXISTING = 0x01;
            internal const uint MOVEFILE_COPY_ALLOWED = 0x02;

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "MoveFileExW")]
            internal static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, uint dwFlags);
        }
#endif
    }
}

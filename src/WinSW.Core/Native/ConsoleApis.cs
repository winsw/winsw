#pragma warning disable SA1310 // Field names should not contain underscore

using System;
using System.Runtime.InteropServices;

namespace WinSW.Native
{
    internal static class ConsoleApis
    {
        internal const int ATTACH_PARENT_PROCESS = -1;

        internal const uint CP_UTF8 = 65001;

        internal const uint ENABLE_PROCESSED_INPUT = 0x0001;
        internal const uint ENABLE_LINE_INPUT = 0x0002;
        internal const uint ENABLE_ECHO_INPUT = 0x0004;
        internal const uint ENABLE_MOUSE_INPUT = 0x0010;

        [DllImport(Libraries.Kernel32, SetLastError = true)]
        internal static extern bool AttachConsole(int processId);

        [DllImport(Libraries.Kernel32)]
        internal static extern bool FreeConsole();

        [DllImport(Libraries.Kernel32)]
        internal static extern bool GenerateConsoleCtrlEvent(CtrlEvents ctrlEvent, uint processGroupId);

        [DllImport(Libraries.Kernel32, SetLastError = true)]
        internal static extern bool GetConsoleMode(IntPtr consoleHandle, out uint mode);

        [DllImport(Libraries.Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool ReadConsoleW(IntPtr consoleInput, out char buffer, int numberOfCharsToRead, out int numberOfCharsRead, IntPtr inputControl);

        [DllImport(Libraries.Kernel32)]
        internal static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandlerRoutine? handlerRoutine, bool add);

        [DllImport(Libraries.Kernel32, SetLastError = true)]
        internal static extern bool SetConsoleMode(IntPtr consoleHandle, uint mode);

        [DllImport(Libraries.Kernel32)]
        internal static extern bool SetConsoleOutputCP(uint codePageID);

        [DllImport(Libraries.Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool WriteConsoleW(IntPtr consoleOutput, string buffer, int numberOfCharsToWrite, out int numberOfCharsWritten, IntPtr reserved);

        internal delegate bool ConsoleCtrlHandlerRoutine(CtrlEvents ctrlType);

        internal enum CtrlEvents : uint
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6,
        }
    }
}

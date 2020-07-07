#pragma warning disable SA1310 // Field names should not contain underscore

using System.Runtime.InteropServices;

namespace WinSW.Native
{
    internal static class ConsoleApis
    {
        internal const int ATTACH_PARENT_PROCESS = -1;

        [DllImport(Libraries.Kernel32, SetLastError = true)]
        internal static extern bool AttachConsole(int processId);

        [DllImport(Libraries.Kernel32)]
        internal static extern bool FreeConsole();

        [DllImport(Libraries.Kernel32)]
        internal static extern bool GenerateConsoleCtrlEvent(CtrlEvents ctrlEvent, uint processGroupId);

        [DllImport(Libraries.Kernel32)]
        internal static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandlerRoutine? handlerRoutine, bool add);

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

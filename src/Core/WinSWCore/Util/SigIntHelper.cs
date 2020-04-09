﻿using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using log4net;

namespace winsw.Util
{
    public static class SigIntHelper
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(SigIntHelper));

        public const int ATTACH_PARENT_PROCESS = -1;

        private const string Kernel32LibraryName = "kernel32.dll";

        [DllImport(Kernel32LibraryName, SetLastError = true)]
        public static extern bool AttachConsole(int dwProcessId);

        [DllImport(Kernel32LibraryName, SetLastError = true)]
        public static extern bool FreeConsole();

        [DllImport(Kernel32LibraryName)]
        private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate? HandlerRoutine, bool Add);

        // Delegate type to be used as the Handler Routine for SCCH
        private delegate bool ConsoleCtrlDelegate(CtrlTypes CtrlType);

        // Enumerated type for the control messages sent to the handler routine
        private enum CtrlTypes : uint
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }

        [DllImport(Kernel32LibraryName)]
        private static extern bool GenerateConsoleCtrlEvent(CtrlTypes dwCtrlEvent, uint dwProcessGroupId);

        /// <summary>
        /// Uses the native funciton "AttachConsole" to attach the thread to the executing process to try to trigger a CTRL_C event (SIGINT).  If the application
        /// doesn't honor the event and shut down gracefully, the. wait period will time out after 15 seconds.
        /// </summary>
        /// <param name="process">The process to attach to and send the SIGINT</param>
        /// <returns>True if the process shut down successfully to the SIGINT, false if it did not.</returns>
        public static bool SendSIGINTToProcess(Process process, TimeSpan shutdownTimeout)
        {
            if (AttachConsole(process.Id))
            {
                // Disable Ctrl-C handling for our program
                _ = SetConsoleCtrlHandler(null, true);
                _ = GenerateConsoleCtrlEvent(CtrlTypes.CTRL_C_EVENT, 0);

                process.WaitForExit((int)shutdownTimeout.TotalMilliseconds);

                // Detach from console. Causes child console process to be automatically closed.
                bool success = FreeConsole();
                if (!success)
                {
                    long errorCode = Marshal.GetLastWin32Error();
                    Logger.Warn("Failed to detach from console. Error code: " + errorCode);
                }

                return process.HasExited;
            }

            return false;
        }
    }
}

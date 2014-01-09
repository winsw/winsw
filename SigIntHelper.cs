using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace winsw
{
    public static class SigIntHelper
    {
        private const string KERNEL32 = "kernel32.dll";

        [DllImport(KERNEL32, SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        [DllImport(KERNEL32, SetLastError = true, ExactSpelling = true)]
        private static extern bool FreeConsole();

        [DllImport(KERNEL32)]
        private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);
        // Delegate type to be used as the Handler Routine for SCCH
        private delegate Boolean ConsoleCtrlDelegate(CtrlTypes CtrlType);

        // Enumerated type for the control messages sent to the handler routine
        private enum CtrlTypes : uint
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }

        [DllImport(KERNEL32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GenerateConsoleCtrlEvent(CtrlTypes dwCtrlEvent, uint dwProcessGroupId);

        /// <summary>
        /// Uses the native funciton "AttachConsole" to attach the thread to the executing process to try to trigger a CTRL_C event (SIGINT).  If the application
        /// doesn't honor the event and shut down gracefully, the. wait period will time out after 15 seconds.
        /// </summary>
        /// <param name="process">The process to attach to and send the SIGINT</param>
        /// <returns>True if the process shut down successfully to the SIGINT, false if it did not.</returns>
        public static bool SendSIGINTToProcess(Process process, TimeSpan shutdownTimeout)
        {
            if (AttachConsole((uint)process.Id))
            {
                //Disable Ctrl-C handling for our program
                SetConsoleCtrlHandler(null, true);
                GenerateConsoleCtrlEvent(CtrlTypes.CTRL_C_EVENT, 0);

                process.WaitForExit((int)shutdownTimeout.TotalMilliseconds);

                return process.HasExited;
            }
            else
            {
                return false;
            }
        }
    }
}

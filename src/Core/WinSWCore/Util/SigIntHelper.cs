using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using log4net;
using winsw.Native;

namespace winsw.Util
{
    public static class SigIntHelper
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(SigIntHelper));

        /// <summary>
        /// Uses the native funciton "AttachConsole" to attach the thread to the executing process to try to trigger a CTRL_C event (SIGINT).  If the application
        /// doesn't honor the event and shut down gracefully, the. wait period will time out after 15 seconds.
        /// </summary>
        /// <param name="process">The process to attach to and send the SIGINT</param>
        /// <returns>True if the process shut down successfully to the SIGINT, false if it did not.</returns>
        public static bool SendSIGINTToProcess(Process process, TimeSpan shutdownTimeout)
        {
            if (!ConsoleApis.AttachConsole(process.Id))
            {
                Logger.Warn("Failed to attach to console. Error code: " + Marshal.GetLastWin32Error());
                return false;
            }

            // Disable Ctrl-C handling for our program
            _ = ConsoleApis.SetConsoleCtrlHandler(null, true);
            _ = ConsoleApis.GenerateConsoleCtrlEvent(ConsoleApis.CtrlEvents.CTRL_C_EVENT, 0);

            process.WaitForExit((int)shutdownTimeout.TotalMilliseconds);

            // Detach from console. Causes child console process to be automatically closed.
            bool succeeded = ConsoleApis.FreeConsole();
            Debug.Assert(succeeded);

            return process.HasExited;
        }
    }
}

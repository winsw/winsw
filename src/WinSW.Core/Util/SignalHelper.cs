using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using log4net;
using WinSW.Native;

namespace WinSW.Util
{
    internal static class SignalHelper
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(SignalHelper));

        // (bool sent, bool exited)
        internal static KeyValuePair<bool, bool> SendCtrlCToProcess(Process process, TimeSpan shutdownTimeout)
        {
            if (!ConsoleApis.AttachConsole(process.Id))
            {
                int error = Marshal.GetLastWin32Error();
                Logger.Warn("Failed to attach to console. " + error switch
                {
                    Errors.ERROR_ACCESS_DENIED => "WinSW is already attached to a console.", // TODO: test mode
                    Errors.ERROR_INVALID_HANDLE => "The process does not have a console.",
                    Errors.ERROR_INVALID_PARAMETER => "The process has exited.",
                    _ => new Win32Exception(error).Message // unreachable
                });

                return new KeyValuePair<bool, bool>(false, error == Errors.ERROR_INVALID_PARAMETER);
            }

            _ = ConsoleApis.SetConsoleCtrlHandler(null, true);
            _ = ConsoleApis.GenerateConsoleCtrlEvent(ConsoleApis.CtrlEvents.CTRL_C_EVENT, 0);
            _ = ConsoleApis.SetConsoleCtrlHandler(null, false);
            bool succeeded = ConsoleApis.FreeConsole();
            Debug.Assert(succeeded);

            return new KeyValuePair<bool, bool>(true, process.WaitForExit((int)shutdownTimeout.TotalMilliseconds));
        }
    }
}

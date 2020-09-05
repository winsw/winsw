using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using log4net;
using WinSW.Native;
using static WinSW.Native.ConsoleApis;
using static WinSW.Native.ProcessApis;

namespace WinSW.Util
{
    public static class ProcessExtensions
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ProcessExtensions));

        public static void StopTree(this Process process, int millisecondsTimeout)
        {
            StopPrivate(process, millisecondsTimeout);

            foreach (var child in GetChildren(process))
            {
                using (child)
                {
                    StopTree(child, millisecondsTimeout);
                }
            }
        }

        internal static void StopDescendants(this Process process, int millisecondsTimeout)
        {
            foreach (var child in GetChildren(process))
            {
                using (child)
                {
                    StopTree(child, millisecondsTimeout);
                }
            }
        }

        internal static unsafe List<Process> GetChildren(this Process process)
        {
            var startTime = process.StartTime;
            int processId = process.Id;

            var children = new List<Process>();

            foreach (var other in Process.GetProcesses())
            {
                try
                {
                    if (other.StartTime <= startTime)
                    {
                        goto Next;
                    }

                    var handle = other.Handle;

                    if (NtQueryInformationProcess(
                        handle,
                        PROCESSINFOCLASS.ProcessBasicInformation,
                        out var information,
                        sizeof(PROCESS_BASIC_INFORMATION)) != 0)
                    {
                        goto Next;
                    }

                    if ((int)information.InheritedFromUniqueProcessId == processId)
                    {
                        Log.Debug($"Found child process '{other.Format()}'.");
                        children.Add(other);
                        continue;
                    }

                Next:
                    other.Dispose();
                }
                catch (Exception e) when (e is InvalidOperationException || e is Win32Exception)
                {
                    other.Dispose();
                }
            }

            return children;
        }

        // true  => canceled
        // false => terminated
        // null  => finished
        internal static bool? Stop(this Process process, int millisecondsTimeout)
        {
            if (process.HasExited)
            {
                return null;
            }

            if (!(SendCtrlC(process) is bool sent))
            {
                return null;
            }

            if (!sent)
            {
                try
                {
                    sent = process.CloseMainWindow();
                }
                catch (InvalidOperationException)
                {
                    return null;
                }
            }

            if (sent)
            {
                if (process.WaitForExit(millisecondsTimeout))
                {
                    return true;
                }
            }

#if NET
            process.Kill();
#else
            try
            {
                process.Kill();
            }
            catch when (process.HasExited)
            {
            }
#endif

            return false;
        }

        private static void StopPrivate(Process process, int millisecondsTimeout)
        {
            Log.Debug($"Stopping process '{process.Format()}'...");

            if (process.HasExited)
            {
                goto Exited;
            }

            if (!(SendCtrlC(process) is bool sent))
            {
                goto Exited;
            }

            if (!sent)
            {
                try
                {
                    sent = process.CloseMainWindow();
                }
                catch (InvalidOperationException)
                {
                    goto Exited;
                }
            }

            if (sent)
            {
                if (process.WaitForExit(millisecondsTimeout))
                {
                    Log.Debug($"Process '{process.Format()}' canceled with code {process.ExitCode}.");
                    return;
                }
            }

#if NET
            process.Kill();
#else
            try
            {
                process.Kill();
            }
            catch when (process.HasExited)
            {
            }
#endif

            Log.Debug($"Process '{process.Format()}' terminated.");
            return;

        Exited:
            Log.Debug($"Process '{process.Format()}' has already exited.");
        }

        private static bool? SendCtrlC(Process process)
        {
            if (!AttachConsole(process.Id))
            {
                int error = Marshal.GetLastWin32Error();
                switch (error)
                {
                    // The process does not have a console.
                    case Errors.ERROR_INVALID_HANDLE:
                        return false;

                    // The process has exited.
                    case Errors.ERROR_INVALID_PARAMETER:
                        return null;

                    // The calling process is already attached to a console.
                    case Errors.ERROR_ACCESS_DENIED:
                    default:
                        Log.Warn("Failed to attach to console. " + new Win32Exception(error).Message);
                        return false;
                }
            }

            // Don't call GenerateConsoleCtrlEvent immediately after SetConsoleCtrlHandler.
            // A delay was observed as of Windows 10, version 2004 and Windows Server 2019.
            _ = GenerateConsoleCtrlEvent(CtrlEvents.CTRL_C_EVENT, 0);

            bool succeeded = FreeConsole();
            Debug.Assert(succeeded);

            return true;
        }
    }
}

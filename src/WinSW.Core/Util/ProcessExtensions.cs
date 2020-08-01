using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using log4net;
using static WinSW.Native.ProcessApis;

namespace WinSW.Util
{
    public static class ProcessExtensions
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ProcessExtensions));

        public static void StopTree(this Process process, TimeSpan stopTimeout)
        {
            Stop(process, stopTimeout);

            foreach (Process child in GetChildren(process))
            {
                using (child)
                {
                    StopTree(child, stopTimeout);
                }
            }
        }

        internal static void StopDescendants(this Process process, TimeSpan stopTimeout)
        {
            foreach (Process child in GetChildren(process))
            {
                using (child)
                {
                    StopTree(child, stopTimeout);
                }
            }
        }

        internal static unsafe List<Process> GetChildren(this Process process)
        {
            DateTime startTime = process.StartTime;
            int processId = process.Id;

            var children = new List<Process>();

            foreach (Process other in Process.GetProcesses())
            {
                try
                {
                    if (other.StartTime <= startTime)
                    {
                        goto Next;
                    }

                    IntPtr handle = other.Handle;

                    if (NtQueryInformationProcess(
                        handle,
                        PROCESSINFOCLASS.ProcessBasicInformation,
                        out PROCESS_BASIC_INFORMATION information,
                        sizeof(PROCESS_BASIC_INFORMATION)) != 0)
                    {
                        goto Next;
                    }

                    if ((int)information.InheritedFromUniqueProcessId == processId)
                    {
                        Logger.Info($"Found child process '{other.Format()}'.");
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

        private static void Stop(Process process, TimeSpan stopTimeout)
        {
            Logger.Info("Stopping process " + process.Id);

            if (process.HasExited)
            {
                Logger.Info("Process " + process.Id + " is already stopped");
                return;
            }

            // (bool sent, bool exited)
            KeyValuePair<bool, bool> result = SignalHelper.SendCtrlCToProcess(process, stopTimeout);
            bool exited = result.Value;
            if (!exited)
            {
                bool sent = result.Key;
                if (sent)
                {
                    Logger.Info("Process " + process.Id + " did not respond to Ctrl+C signal - Killing as fallback");
                }

                try
                {
                    process.Kill();
                }
                catch when (process.HasExited)
                {
                }
            }

            // TODO: Propagate error if process kill fails? Currently we use the legacy behavior
        }
    }
}

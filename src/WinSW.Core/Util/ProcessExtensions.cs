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

            foreach (Process child in GetDescendants(process))
            {
                StopTree(child, stopTimeout);
            }
        }

        internal static void StopDescendants(this Process process, TimeSpan stopTimeout)
        {
            foreach (Process child in GetDescendants(process))
            {
                StopTree(child, stopTimeout);
            }
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

        private static unsafe List<Process> GetDescendants(Process root)
        {
            DateTime startTime = root.StartTime;
            int processId = root.Id;

            var children = new List<Process>();

            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    if (process.StartTime <= startTime)
                    {
                        goto Next;
                    }

                    IntPtr handle = process.Handle;

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
                        Logger.Info($"Found child process '{process.Format()}'.");
                        children.Add(process);
                        continue;
                    }

                Next:
                    process.Dispose();
                }
                catch (Exception e) when (e is InvalidOperationException || e is Win32Exception)
                {
                    process.Dispose();
                }
            }

            return children;
        }
    }
}

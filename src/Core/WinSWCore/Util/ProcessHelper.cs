using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Text;

namespace winsw.Util
{
    /// <summary>
    /// Provides helper classes for Process Management
    /// </summary>
    /// <remarks>Since WinSW 2.0</remarks>
    public class ProcessHelper
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ProcessHelper));

        /// <summary>
        /// Gets all children of the specified process.
        /// </summary>
        /// <param name="pid">Process PID</param>
        /// <returns>List of child process PIDs</returns>
        public static List<int> GetChildPids(int pid)
        {
            var searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
            var childPids = new List<int>();
            foreach (var mo in searcher.Get())
            {
                var childProcessId = mo["ProcessID"];
                Logger.Info("Found child process: " + childProcessId + " Name: " + mo["Name"]);
                childPids.Add(Convert.ToInt32(childProcessId));
            }
            return childPids;
        }

        /// <summary>
        /// Stops the process.
        /// If the process cannot be stopped within the stop timeout, it gets killed
        /// </summary>
        /// <param name="pid">PID of the process</param>
        /// <param name="stopTimeout">Stop timeout</param>
        public static void StopProcess(int pid, TimeSpan stopTimeout)
        {
            Logger.Info("Stopping process " + pid);
            Process proc;
            try
            {
                proc = Process.GetProcessById(pid);
            }
            catch (ArgumentException ex)
            {
                Logger.Info("Process " + pid + " is already stopped", ex);
                return;
            }

            Logger.Info("Send SIGINT " + pid);
            bool successful = SigIntHelper.SendSIGINTToProcess(proc, stopTimeout);
            if (successful)
            {
                Logger.Info("SIGINT to" + pid + " successful");
            }
            else
            {
                try
                {
                    Logger.Warn("SIGINT to " + pid + " failed - Killing as fallback");
                    proc.Kill();
                }
                catch (Exception ex)
                {
                    if (!proc.HasExited)
                    {
                        throw;
                    }

                    // Process already exited.
                    Logger.Warn("Ignoring exception from killing process because it has exited", ex);
                }
            }

            //TODO: Propagate error if process kill fails? Currently we use the legacy behavior
        }

        /// <summary>
        /// Terminate process and its children.
        /// By default the child processes get terminated first.
        /// </summary>
        /// <param name="pid">Process PID</param>
        /// <param name="stopTimeout">Stop timeout (for each process)</param>
        /// <param name="stopParentProcessFirst">If enabled, the perent process will be terminated before its children on all levels</param>
        public static void StopProcessAndChildren(int pid, TimeSpan stopTimeout, bool stopParentProcessFirst)
        {
            List<int> childPids = null;
            try
            {
                childPids = GetChildPids(pid);
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to locate children of the process with PID=" + pid + ". Child processes won't be terminated", ex);
            }

            if (!stopParentProcessFirst && childPids != null)
            {  
                foreach (var childPid in childPids)
                {
                    StopProcessAndChildren(childPid, stopTimeout, stopParentProcessFirst);
                }
            }

            StopProcess(pid, stopTimeout);

            if (stopParentProcessFirst && childPids != null)
            {
                foreach (var childPid in childPids)
                {
                    StopProcessAndChildren(childPid, stopTimeout, stopParentProcessFirst);
                }
            }
        }

        // TODO: Also move StartProcess methods once LogEvent()/WriteEvent() mess gets solved
    }
}

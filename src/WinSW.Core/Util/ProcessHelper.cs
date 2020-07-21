using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using log4net;
using static WinSW.Native.ProcessApis;

namespace WinSW.Util
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
        /// <param name="processId">Process PID</param>
        /// <returns>List of child process PIDs</returns>
        private static unsafe List<Process> GetChildProcesses(DateTime startTime, int processId)
        {
            var children = new List<Process>();

            foreach (Process process in Process.GetProcesses())
            {
                if (process.StartTime <= startTime)
                {
                    process.Dispose();
                    continue;
                }

                IntPtr handle;
                try
                {
                    handle = process.Handle;
                }
                catch (Win32Exception)
                {
                    process.Dispose();
                    continue;
                }

                if (NtQueryInformationProcess(
                    handle,
                    PROCESSINFOCLASS.ProcessBasicInformation,
                    out PROCESS_BASIC_INFORMATION information,
                    sizeof(PROCESS_BASIC_INFORMATION)) != 0)
                {
                    Logger.Warn("Failed to locate children of the process with PID=" + processId + ". Child processes won't be terminated");
                    process.Dispose();
                    continue;
                }

                if ((int)information.InheritedFromUniqueProcessId == processId)
                {
                    Logger.Info("Found child process: " + process.Id + " Name: " + process.ProcessName);
                    children.Add(process);
                }
                else
                {
                    process.Dispose();
                }
            }

            return children;
        }

        /// <summary>
        /// Stops the process.
        /// If the process cannot be stopped within the stop timeout, it gets killed
        /// </summary>
        public static void StopProcess(Process process, TimeSpan stopTimeout)
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
                    Logger.Warn("Process " + process.Id + " did not respond to Ctrl+C signal - Killing as fallback");
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

        /// <summary>
        /// Terminate process and its children.
        /// By default the child processes get terminated first.
        /// </summary>
        public static void StopProcessTree(Process process, TimeSpan stopTimeout)
        {
            StopProcess(process, stopTimeout);

            foreach (Process child in GetChildProcesses(process.StartTime, process.Id))
            {
                StopProcessTree(child, stopTimeout);
            }
        }

        /// <summary>
        /// Starts a process and asynchronosly waits for its termination.
        /// Once the process exits, the callback will be invoked.
        /// </summary>
        /// <param name="processToStart">Process object to be used</param>
        /// <param name="executable">Executable, which should be invoked</param>
        /// <param name="arguments">Arguments to be passed</param>
        /// <param name="envVars">Additional environment variables</param>
        /// <param name="workingDirectory">Working directory</param>
        /// <param name="priority">Priority</param>
        /// <param name="callback">Completion callback. If null, the completion won't be monitored</param>
        /// <param name="redirectStdin">Redirect standard input</param>
        /// <param name="logHandler">Log handler. If enabled, logs will be redirected to the process and then reported</param>
        public static void StartProcessAndCallbackForExit(
            Process processToStart,
            string? executable = null,
            string? arguments = null,
            Dictionary<string, string>? envVars = null,
            string? workingDirectory = null,
            ProcessPriorityClass? priority = null,
            Action<Process>? callback = null,
            bool redirectStdin = true,
            LogHandler? logHandler = null,
            bool hideWindow = false)
        {
            var ps = processToStart.StartInfo;
            ps.FileName = executable ?? ps.FileName;
            ps.Arguments = arguments ?? ps.Arguments;
            ps.WorkingDirectory = workingDirectory ?? ps.WorkingDirectory;
            ps.CreateNoWindow = hideWindow;
            ps.UseShellExecute = false;
            ps.RedirectStandardInput = redirectStdin;
            ps.RedirectStandardOutput = logHandler != null;
            ps.RedirectStandardError = logHandler != null;

            if (envVars != null)
            {
                var newEnvironment =
#if NETCOREAPP
                    ps.Environment;
#else
                    ps.EnvironmentVariables;
#endif
                foreach (KeyValuePair<string, string> pair in envVars)
                {
                    newEnvironment[pair.Key] = pair.Value;
                }
            }

            processToStart.Start();
            Logger.Info("Started process " + processToStart.Id);

            if (priority != null)
            {
                processToStart.PriorityClass = priority.Value;
            }

            // Redirect logs if required
            if (logHandler != null)
            {
                Logger.Debug("Forwarding logs of the process " + processToStart + " to " + logHandler);
                logHandler.Log(processToStart.StandardOutput, processToStart.StandardError);
            }

            // monitor the completion of the process
            if (callback != null)
            {
                processToStart.Exited += (sender, _) =>
                {
                    try
                    {
                        callback((Process)sender!);
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Thread failed unexpectedly", e);
                    }
                };

                processToStart.EnableRaisingEvents = true;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Threading;
using log4net;

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
        /// <param name="pid">Process PID</param>
        /// <returns>List of child process PIDs</returns>
        public static List<int> GetChildPids(int pid)
        {
            var childPids = new List<int>();

            try
            {
                string query = "SELECT * FROM Win32_Process WHERE ParentProcessID = " + pid;
                using ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
                using ManagementObjectCollection results = searcher.Get();
                foreach (ManagementBaseObject wmiObject in results)
                {
                    var childProcessId = wmiObject["ProcessID"];
                    Logger.Info("Found child process: " + childProcessId + " Name: " + wmiObject["Name"]);
                    childPids.Add(Convert.ToInt32(childProcessId));
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to locate children of the process with PID=" + pid + ". Child processes won't be terminated", ex);
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

            // (bool sent, bool exited)
            KeyValuePair<bool, bool> result = SignalHelper.SendCtrlCToProcess(proc, stopTimeout);
            bool exited = result.Value;
            if (!exited)
            {
                try
                {
                    bool sent = result.Key;
                    if (sent)
                    {
                        Logger.Warn("Process " + pid + " did not respond to Ctrl+C signal - Killing as fallback");
                    }

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

            // TODO: Propagate error if process kill fails? Currently we use the legacy behavior
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
            if (!stopParentProcessFirst)
            {
                foreach (var childPid in GetChildPids(pid))
                {
                    StopProcessAndChildren(childPid, stopTimeout, stopParentProcessFirst);
                }
            }

            StopProcess(pid, stopTimeout);

            if (stopParentProcessFirst)
            {
                foreach (var childPid in GetChildPids(pid))
                {
                    StopProcessAndChildren(childPid, stopTimeout, stopParentProcessFirst);
                }
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
            ProcessCompletionCallback? callback = null,
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
                foreach (string key in envVars.Keys)
                {
                    Environment.SetEnvironmentVariable(key, envVars[key]);

                    // DONTDO: ps.EnvironmentVariables[key] = envs[key];
                    // bugged (lower cases all variable names due to StringDictionary being used, see http://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=326163)
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
                StartThread(() =>
                {
                    processToStart.WaitForExit();
                    callback(processToStart);
                });
            }
        }

        /// <summary>
        /// Starts a thread that protects the execution with a try/catch block.
        /// It appears that in .NET, unhandled exception in any thread causes the app to terminate
        /// http://msdn.microsoft.com/en-us/library/ms228965.aspx
        /// </summary>
        public static void StartThread(ThreadStart main)
        {
            new Thread(() =>
            {
                try
                {
                    main();
                }
                catch (Exception e)
                {
                    Logger.Error("Thread failed unexpectedly", e);
                }
            }).Start();
        }
    }

    public delegate void ProcessCompletionCallback(Process process);
}

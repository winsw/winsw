using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using log4net;
using WinSW.Native;
using static WinSW.Native.ConsoleApis;
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

        public static void StopProcessTree(Process process, TimeSpan stopTimeout, bool stopParentProcessFirst)
        {
            if (!stopParentProcessFirst)
            {
                foreach (var child in GetChildren(process))
                {
                    StopProcessTree(child, stopTimeout, stopParentProcessFirst);
                }
            }

            StopProcess(process, stopTimeout);

            if (stopParentProcessFirst)
            {
                foreach (var child in GetChildren(process))
                {
                    StopProcessTree(child, stopTimeout, stopParentProcessFirst);
                }
            }
        }

        private static void StopProcess(Process process, TimeSpan stopTimeout)
        {
            Logger.Debug($"Stopping process {process.Id}...");

            if (process.HasExited)
            {
                goto Exited;
            }

            if (SendCtrlC(process) is not bool sent)
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
                if (process.WaitForExit((int)stopTimeout.TotalMilliseconds))
                {
                    Logger.Debug($"Process {process.Id} canceled with code {process.ExitCode}.");
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

            Logger.Debug($"Process {process.Id} terminated.");
            return;

        Exited:
            Logger.Debug($"Process {process.Id} has already exited.");
        }

        private static unsafe List<Process> GetChildren(Process process)
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
                        Logger.Debug($"Found child process {other.Id}.");
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
                        Logger.Warn("Failed to attach to console. " + new Win32Exception(error).Message);
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
        /// <param name="logHandler">Log handler. If enabled, logs will be redirected to the process and then reported</param>
        public static void StartProcessAndCallbackForExit(
            Process processToStart,
            string? executable = null,
            string? arguments = null,
            Dictionary<string, string>? envVars = null,
            string? workingDirectory = null,
            ProcessPriorityClass? priority = null,
            ProcessCompletionCallback? callback = null,
            LogHandler? logHandler = null,
            bool hideWindow = false)
        {
            var ps = processToStart.StartInfo;
            ps.FileName = executable ?? ps.FileName;
            ps.Arguments = arguments ?? ps.Arguments;
            ps.WorkingDirectory = workingDirectory ?? ps.WorkingDirectory;
            ps.CreateNoWindow = hideWindow;
            ps.UseShellExecute = false;
            ps.RedirectStandardOutput = logHandler?.OutFileDisabled == false;
            ps.RedirectStandardError = logHandler?.ErrFileDisabled == false;

            if (envVars != null)
            {
                foreach (string key in envVars.Keys)
                {
                    Environment.SetEnvironmentVariable(key, envVars[key]);

                    // DONTDO: ps.EnvironmentVariables[key] = envs[key];
                    // bugged (lower cases all variable names due to StringDictionary being used, see http://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=326163)
                }
            }

            bool succeeded = SetConsoleCtrlHandler(null, false); // inherited
            Debug.Assert(succeeded);
            succeeded = SetConsoleOutputCP(CP_UTF8);
            Debug.Assert(succeeded);

            try
            {
                processToStart.Start();
            }
            finally
            {
                succeeded = SetConsoleCtrlHandler(null, true);
                Debug.Assert(succeeded);
            }

            Logger.Info("Started process " + processToStart.Id);

            if (priority != null)
            {
                try
                {
                    processToStart.PriorityClass = priority.Value;
                }
                catch (InvalidOperationException)
                {
                    // exited
                }
            }

            // Redirect logs if required
            if (logHandler != null)
            {
                Logger.Debug("Forwarding logs of the process " + processToStart + " to " + logHandler);
                logHandler.Log(
                    ps.RedirectStandardOutput ? processToStart.StandardOutput : StreamReader.Null,
                    ps.RedirectStandardError ? processToStart.StandardError : StreamReader.Null);
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

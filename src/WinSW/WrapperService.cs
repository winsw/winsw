using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
#if VNEXT
using System.Threading.Tasks;
#endif
using log4net;
using WinSW.Configuration;
using WinSW.Extensions;
using WinSW.Logging;
using WinSW.Native;
using WinSW.Util;

namespace WinSW
{
    public class WrapperService : ServiceBase, IEventLogger
    {
        private readonly Process process = new();

        private readonly IServiceConfig config;

        private Dictionary<string, string>? envs;

        internal WinSWExtensionManager ExtensionManager { get; private set; }

        private static readonly ILog Log = LogManager.GetLogger(typeof(WrapperService));

        internal static readonly WrapperServiceEventLogProvider eventLogProvider = new();

        /// <summary>
        /// Indicates to the watch dog thread that we are going to terminate the process,
        /// so don't try to kill us when the child exits.
        /// </summary>
        private bool orderlyShutdown;
        private bool shuttingdown;

        /// <summary>
        /// Version of Windows service wrapper
        /// </summary>
        /// <remarks>
        /// The version will be taken from <see cref="AssemblyInfo"/>
        /// </remarks>
        public static Version Version => Assembly.GetExecutingAssembly().GetName().Version!;

        /// <summary>
        /// Indicates that the system is shutting down.
        /// </summary>
        public bool IsShuttingDown => this.shuttingdown;

        public WrapperService(IServiceConfig config)
        {
            this.config = config;
            this.ServiceName = this.config.Name;
            this.ExtensionManager = new WinSWExtensionManager(this.config);
            this.CanShutdown = true;
            this.CanStop = true;
            this.CanPauseAndContinue = false;
            this.AutoLog = true;
            this.shuttingdown = false;

            // Register the event log provider
            eventLogProvider.Service = this;
        }

        /// <summary>
        /// Process the file copy instructions, so that we can replace files that are always in use while
        /// the service runs.
        /// </summary>
        private void HandleFileCopies()
        {
            string file = this.config.BasePath + ".copies";
            if (!File.Exists(file))
            {
                return; // nothing to handle
            }

            try
            {
                using var tr = new StreamReader(file, Encoding.UTF8);
                string? line;
                while ((line = tr.ReadLine()) != null)
                {
                    this.LogEvent("Handling copy: " + line);
                    string[] tokens = line.Split('>');
                    if (tokens.Length > 2)
                    {
                        this.LogEvent("Too many delimiters in " + line);
                        continue;
                    }

                    this.MoveFile(tokens[0], tokens[1]);
                }
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// File replacement.
        /// </summary>
        private void MoveFile(string sourceFileName, string destFileName)
        {
            try
            {
                FileHelper.MoveOrReplaceFile(sourceFileName, destFileName);
            }
            catch (IOException e)
            {
                this.LogEvent("Failed to move :" + sourceFileName + " to " + destFileName + " because " + e.Message);
            }
        }

        /// <summary>
        /// Handle the creation of the logfiles based on the optional logmode setting.
        /// </summary>
        /// <returns>Log Handler, which should be used for the spawned process</returns>
        private LogHandler CreateExecutableLogHandler()
        {
            string? logDirectory = this.config.LogDirectory;

            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            var logAppender = this.config.Log.CreateLogHandler();

            logAppender.EventLogger = this;
            return logAppender;
        }

        public void LogEvent(string message)
        {
            if (this.shuttingdown)
            {
                /* NOP - cannot call EventLog because of shutdown. */
            }
            else
            {
                try
                {
                    this.EventLog.WriteEntry(message);
                }
                catch (Exception e)
                {
                    Log.Error("Failed to log event in Windows Event Log: " + message + "; Reason: ", e);
                }
            }
        }

        public void LogEvent(string message, EventLogEntryType type)
        {
            if (this.shuttingdown)
            {
                /* NOP - cannot call EventLog because of shutdown. */
            }
            else
            {
                try
                {
                    this.EventLog.WriteEntry(message, type);
                }
                catch (Exception e)
                {
                    Log.Error("Failed to log event in Windows Event Log. Reason: ", e);
                }
            }
        }
        internal void RaiseOnStart(string[] args) => this.OnStart(args);

        internal void RaiseOnStop() => this.OnStop();

        protected override void OnStart(string[] args)
        {
            try
            {
                this.DoStart();
            }
            catch (Exception e)
            {
                Log.Error("Failed to start service.", e);
                throw;
            }
        }

        protected override void OnStop()
        {
            try
            {
                this.DoStop();
            }
            catch (Exception e)
            {
                Log.Error("Failed to stop service.", e);
                throw;
            }
        }

        protected override void OnShutdown()
        {
            try
            {
                this.shuttingdown = true;
                this.DoStop();
            }
            catch (Exception e)
            {
                Log.Error("Failed to shut down service.", e);
                throw;
            }
        }

        private void DoStart()
        {
            bool succeeded = ConsoleApis.SetConsoleCtrlHandler(null, true);
            Debug.Assert(succeeded);

            this.envs = this.config.EnvironmentVariables;

            // TODO: Disabled according to security concerns in https://github.com/kohsuke/winsw/issues/54
            // Could be restored, but unlikely it's required in event logs at all
            /**
            foreach (string key in _envs.Keys)
            {
                LogEvent("envar " + key + '=' + _envs[key]);
            }*/
            this.HandleFileCopies();

            // handle downloads
#if VNEXT
            var downloads = this.config.Downloads;
            var tasks = new Task[downloads.Count];
            for (int i = 0; i < downloads.Count; i++)
            {
                var download = downloads[i];
                string downloadMessage = $"Downloading: {download.From} to {download.To}. failOnError={download.FailOnError.ToString()}";
                this.LogEvent(downloadMessage);
                Log.Info(downloadMessage);
                tasks[i] = download.PerformAsync();
            }

            Task.WaitAll(tasks);
#else
            foreach (var download in this.config.Downloads)
            {
                string downloadMessage = $"Downloading: {download.From} to {download.To}. failOnError={download.FailOnError.ToString()}";
                this.LogEvent(downloadMessage);
                Log.Info(downloadMessage);
                download.Perform();
            }
#endif

            string? startArguments = this.config.StartArguments;

            if (startArguments is null)
            {
                startArguments = this.config.Arguments;
            }
            else
            {
                startArguments += " " + this.config.Arguments;
            }

            // Converting newlines, line returns, tabs into a single
            // space. This allows users to provide multi-line arguments
            // in the xml for readability.
            startArguments = Regex.Replace(startArguments, @"\s*[\n\r]+\s*", " ");

            this.LogEvent("Starting " + this.config.Executable + ' ' + startArguments);
            Log.Info("Starting " + this.config.Executable + ' ' + startArguments);

            // Load and start extensions
            this.ExtensionManager.LoadExtensions();
            this.ExtensionManager.FireOnWrapperStarted();

            var executableLogHandler = this.CreateExecutableLogHandler();
            this.StartProcess(this.process, startArguments, this.config.Executable, executableLogHandler);
            this.ExtensionManager.FireOnProcessStarted(this.process);
        }

        /// <summary>
        /// Called when we are told by Windows SCM to exit.
        /// </summary>
        private void DoStop()
        {
            string? stopArguments = this.config.StopArguments;
            this.LogEvent("Stopping " + this.config.Name);
            Log.Info("Stopping " + this.config.Name);
            this.orderlyShutdown = true;

            if (stopArguments is null)
            {
                try
                {
                    Log.Debug("ProcessKill " + this.process.Id);
                    ProcessHelper.StopProcessTree(this.process, this.config.StopTimeout, this.config.StopParentProcessFirst);
                    this.ExtensionManager.FireOnProcessTerminated(this.process);
                }
                catch (InvalidOperationException)
                {
                    // already terminated
                }
            }
            else
            {
                this.SignalPending();

                stopArguments += " " + this.config.Arguments;

                var stopProcess = new Process();
                string? executable = this.config.StopExecutable;

                executable ??= this.config.Executable;

                // TODO: Redirect logging to Log4Net once https://github.com/kohsuke/winsw/pull/213 is integrated
                this.StartProcess(stopProcess, stopArguments, executable, null);

                Log.Debug("WaitForProcessToExit " + this.process.Id + "+" + stopProcess.Id);
                this.WaitForProcessToExit(this.process);
                this.WaitForProcessToExit(stopProcess);
            }

            // Stop extensions
            this.ExtensionManager.FireBeforeWrapperStopped();

            if (this.shuttingdown && this.config.BeepOnShutdown)
            {
                Console.Beep();
            }

            Log.Info("Finished " + this.config.Name);
        }

        private void WaitForProcessToExit(Process processoWait)
        {
            this.SignalPending();

            int effectiveProcessWaitSleepTime;
            if (this.config.SleepTime.TotalMilliseconds > int.MaxValue)
            {
                Log.Warn("The requested sleep time " + this.config.SleepTime.TotalMilliseconds + "is greater that the max value " +
                    int.MaxValue + ". The value will be truncated");
                effectiveProcessWaitSleepTime = int.MaxValue;
            }
            else
            {
                effectiveProcessWaitSleepTime = (int)this.config.SleepTime.TotalMilliseconds;
            }

            try
            {
                // WriteEvent("WaitForProcessToExit [start]");

                while (!processoWait.WaitForExit(effectiveProcessWaitSleepTime))
                {
                    this.SignalPending();
                    // WriteEvent("WaitForProcessToExit [repeat]");
                }
            }
            catch (InvalidOperationException)
            {
                // already terminated
            }

            // WriteEvent("WaitForProcessToExit [finished]");
        }

        private void SignalPending()
        {
            int effectiveWaitHint;
            if (this.config.WaitHint.TotalMilliseconds > int.MaxValue)
            {
                Log.Warn("The requested WaitHint value (" + this.config.WaitHint.TotalMilliseconds + " ms)  is greater that the max value " +
                    int.MaxValue + ". The value will be truncated");
                effectiveWaitHint = int.MaxValue;
            }
            else
            {
                effectiveWaitHint = (int)this.config.WaitHint.TotalMilliseconds;
            }

            this.RequestAdditionalTime(effectiveWaitHint);
        }

        private void SignalStopped()
        {
            using var scm = ServiceManager.Open();
            using var sc = scm.OpenService(this.ServiceName, ServiceApis.ServiceAccess.QueryStatus);

            sc.SetStatus(this.ServiceHandle, ServiceControllerStatus.Stopped);
        }

        private void StartProcess(Process processToStart, string arguments, string executable, LogHandler? logHandler)
        {
            // Define handler of the completed process
            void OnProcessCompleted(Process proc)
            {
                string msg = processToStart.Id + " - " + processToStart.StartInfo.FileName + " " + processToStart.StartInfo.Arguments;
                try
                {
                    if (this.orderlyShutdown)
                    {
                        this.LogEvent("Child process [" + msg + "] terminated with " + proc.ExitCode, EventLogEntryType.Information);
                    }
                    else
                    {
                        this.LogEvent("Child process [" + msg + "] finished with " + proc.ExitCode, EventLogEntryType.Warning);

                        // if we finished orderly, report that to SCM.
                        // by not reporting unclean shutdown, we let Windows SCM to decide if it wants to
                        // restart the service automatically
                        if (proc.ExitCode == 0)
                        {
                            this.SignalStopped();
                        }

                        Environment.Exit(proc.ExitCode);
                    }
                }
                catch (InvalidOperationException ioe)
                {
                    this.LogEvent("WaitForExit " + ioe.Message);
                }
                finally
                {
                    proc.Dispose();
                }
            }

            // Invoke process and exit
            ProcessHelper.StartProcessAndCallbackForExit(
                processToStart: processToStart,
                executable: executable,
                arguments: arguments,
                envVars: this.envs,
                workingDirectory: this.config.WorkingDirectory,
                priority: this.config.Priority,
                callback: OnProcessCompleted,
                logHandler: logHandler,
                hideWindow: this.config.HideWindow);
        }
    }
}

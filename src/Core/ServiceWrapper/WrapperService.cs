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
using WinSW.Extensions;
using WinSW.Logging;
using WinSW.Native;
using WinSW.Util;

namespace WinSW
{
    public class WrapperService : ServiceBase, IEventLogger
    {
        private ServiceApis.SERVICE_STATUS wrapperServiceStatus;

        private readonly Process process = new Process();
        private readonly ServiceDescriptor descriptor;
        private Dictionary<string, string>? envs;

        internal WinSWExtensionManager ExtensionManager { get; private set; }

        private static readonly ILog Log = LogManager.GetLogger(
#if NETCOREAPP
            Assembly.GetExecutingAssembly(),
#endif
            "WinSW");

        internal static readonly WrapperServiceEventLogProvider eventLogProvider = new WrapperServiceEventLogProvider();

        /// <summary>
        /// Indicates to the watch dog thread that we are going to terminate the process,
        /// so don't try to kill us when the child exits.
        /// </summary>
        private bool orderlyShutdown;
        private bool systemShuttingdown;

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
        public bool IsShuttingDown => this.systemShuttingdown;

        public WrapperService(ServiceDescriptor descriptor)
        {
            this.descriptor = descriptor;
            this.ServiceName = this.descriptor.Id;
            this.ExtensionManager = new WinSWExtensionManager(this.descriptor);
            this.CanShutdown = true;
            this.CanStop = true;
            this.CanPauseAndContinue = false;
            this.AutoLog = true;
            this.systemShuttingdown = false;

            // Register the event log provider
            eventLogProvider.Service = this;
        }

        public WrapperService()
            : this(new ServiceDescriptor())
        {
        }

        /// <summary>
        /// Process the file copy instructions, so that we can replace files that are always in use while
        /// the service runs.
        /// </summary>
        private void HandleFileCopies()
        {
            var file = this.descriptor.BasePath + ".copies";
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
            string logDirectory = this.descriptor.LogDirectory;

            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            LogHandler logAppender = this.descriptor.LogHandler;
            logAppender.EventLogger = this;
            return logAppender;
        }

        public void LogEvent(string message)
        {
            if (this.systemShuttingdown)
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
            if (this.systemShuttingdown)
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

        protected override void OnStart(string[] args)
        {
            this.envs = this.descriptor.EnvironmentVariables;

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
            List<Download> downloads = this.descriptor.Downloads;
            Task[] tasks = new Task[downloads.Count];
            for (int i = 0; i < downloads.Count; i++)
            {
                Download download = downloads[i];
                string downloadMessage = $"Downloading: {download.From} to {download.To}. failOnError={download.FailOnError.ToString()}";
                this.LogEvent(downloadMessage);
                Log.Info(downloadMessage);
                tasks[i] = download.PerformAsync();
            }

            try
            {
                Task.WaitAll(tasks);
            }
            catch (AggregateException e)
            {
                List<Exception> exceptions = new List<Exception>(e.InnerExceptions.Count);
                for (int i = 0; i < tasks.Length; i++)
                {
                    if (tasks[i].IsFaulted)
                    {
                        Download download = downloads[i];
                        string errorMessage = $"Failed to download {download.From} to {download.To}";
                        AggregateException exception = tasks[i].Exception!;
                        this.LogEvent($"{errorMessage}. {exception.Message}");
                        Log.Error(errorMessage, exception);

                        // TODO: move this code into the download logic
                        if (download.FailOnError)
                        {
                            exceptions.Add(new IOException(errorMessage, exception));
                        }
                    }
                }

                throw new AggregateException(exceptions);
            }
#else
            foreach (Download download in this.descriptor.Downloads)
            {
                string downloadMessage = $"Downloading: {download.From} to {download.To}. failOnError={download.FailOnError.ToString()}";
                this.LogEvent(downloadMessage);
                Log.Info(downloadMessage);
                try
                {
                    download.Perform();
                }
                catch (Exception e)
                {
                    string errorMessage = $"Failed to download {download.From} to {download.To}";
                    this.LogEvent($"{errorMessage}. {e.Message}");
                    Log.Error(errorMessage, e);

                    // TODO: move this code into the download logic
                    if (download.FailOnError)
                    {
                        throw new IOException(errorMessage, e);
                    }

                    // Else just keep going
                }
            }
#endif

            string? startArguments = this.descriptor.StartArguments;

            if (startArguments is null)
            {
                startArguments = this.descriptor.Arguments;
            }
            else
            {
                startArguments += " " + this.descriptor.Arguments;
            }

            // Converting newlines, line returns, tabs into a single
            // space. This allows users to provide multi-line arguments
            // in the xml for readability.
            startArguments = Regex.Replace(startArguments, @"\s*[\n\r]+\s*", " ");

            this.LogEvent("Starting " + this.descriptor.Executable + ' ' + startArguments);
            Log.Info("Starting " + this.descriptor.Executable + ' ' + startArguments);

            // Load and start extensions
            this.ExtensionManager.LoadExtensions();
            this.ExtensionManager.FireOnWrapperStarted();

            LogHandler executableLogHandler = this.CreateExecutableLogHandler();
            this.StartProcess(this.process, startArguments, this.descriptor.Executable, executableLogHandler, true);
            this.ExtensionManager.FireOnProcessStarted(this.process);

            this.process.StandardInput.Close(); // nothing for you to read!
        }

        protected override void OnShutdown()
        {
            // WriteEvent("OnShutdown");

            try
            {
                this.systemShuttingdown = true;
                this.StopIt();
            }
            catch (Exception ex)
            {
                Log.Error("Shutdown exception", ex);
            }
        }

        protected override void OnStop()
        {
            // WriteEvent("OnStop");

            try
            {
                this.StopIt();
            }
            catch (Exception ex)
            {
                Log.Error("Cannot stop exception", ex);
            }
        }

        internal void RaiseOnStart(string[] args) => this.OnStart(args);

        internal void RaiseOnStop() => this.OnStop();

        /// <summary>
        /// Called when we are told by Windows SCM to exit.
        /// </summary>
        private void StopIt()
        {
            string? stopArguments = this.descriptor.StopArguments;
            this.LogEvent("Stopping " + this.descriptor.Id);
            Log.Info("Stopping " + this.descriptor.Id);
            this.orderlyShutdown = true;

            if (stopArguments is null)
            {
                try
                {
                    Log.Debug("ProcessKill " + this.process.Id);
                    ProcessHelper.StopProcessAndChildren(this.process.Id, this.descriptor.StopTimeout, this.descriptor.StopParentProcessFirst);
                    this.ExtensionManager.FireOnProcessTerminated(this.process);
                }
                catch (InvalidOperationException)
                {
                    // already terminated
                }
            }
            else
            {
                this.SignalShutdownPending();

                stopArguments += " " + this.descriptor.Arguments;

                Process stopProcess = new Process();
                string? executable = this.descriptor.StopExecutable;

                executable ??= this.descriptor.Executable;

                // TODO: Redirect logging to Log4Net once https://github.com/kohsuke/winsw/pull/213 is integrated
                this.StartProcess(stopProcess, stopArguments, executable, null, false);

                Log.Debug("WaitForProcessToExit " + this.process.Id + "+" + stopProcess.Id);
                this.WaitForProcessToExit(this.process);
                this.WaitForProcessToExit(stopProcess);
            }

            // Stop extensions
            this.ExtensionManager.FireBeforeWrapperStopped();

            if (this.systemShuttingdown && this.descriptor.BeepOnShutdown)
            {
                Console.Beep();
            }

            Log.Info("Finished " + this.descriptor.Id);
        }

        private void WaitForProcessToExit(Process processoWait)
        {
            this.SignalShutdownPending();

            int effectiveProcessWaitSleepTime;
            if (this.descriptor.SleepTime.TotalMilliseconds > int.MaxValue)
            {
                Log.Warn("The requested sleep time " + this.descriptor.SleepTime.TotalMilliseconds + "is greater that the max value " +
                    int.MaxValue + ". The value will be truncated");
                effectiveProcessWaitSleepTime = int.MaxValue;
            }
            else
            {
                effectiveProcessWaitSleepTime = (int)this.descriptor.SleepTime.TotalMilliseconds;
            }

            try
            {
                // WriteEvent("WaitForProcessToExit [start]");

                while (!processoWait.WaitForExit(effectiveProcessWaitSleepTime))
                {
                    this.SignalShutdownPending();
                    // WriteEvent("WaitForProcessToExit [repeat]");
                }
            }
            catch (InvalidOperationException)
            {
                // already terminated
            }

            // WriteEvent("WaitForProcessToExit [finished]");
        }

        private void SignalShutdownPending()
        {
            int effectiveWaitHint;
            if (this.descriptor.WaitHint.TotalMilliseconds > int.MaxValue)
            {
                Log.Warn("The requested WaitHint value (" + this.descriptor.WaitHint.TotalMilliseconds + " ms)  is greater that the max value " +
                    int.MaxValue + ". The value will be truncated");
                effectiveWaitHint = int.MaxValue;
            }
            else
            {
                effectiveWaitHint = (int)this.descriptor.WaitHint.TotalMilliseconds;
            }

            this.RequestAdditionalTime(effectiveWaitHint);
        }

        private void SignalShutdownComplete()
        {
            IntPtr handle = this.ServiceHandle;
            this.wrapperServiceStatus.CheckPoint++;
            // WriteEvent("SignalShutdownComplete " + wrapperServiceStatus.checkPoint + ":" + wrapperServiceStatus.waitHint);
            this.wrapperServiceStatus.CurrentState = ServiceApis.ServiceState.STOPPED;
            ServiceApis.SetServiceStatus(handle, this.wrapperServiceStatus);
        }

        private void StartProcess(Process processToStart, string arguments, string executable, LogHandler? logHandler, bool redirectStdin)
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
                            this.SignalShutdownComplete();
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
                workingDirectory: this.descriptor.WorkingDirectory,
                priority: this.descriptor.Priority,
                callback: OnProcessCompleted,
                logHandler: logHandler,
                redirectStdin: redirectStdin,
                hideWindow: this.descriptor.HideWindow);
        }
    }
}

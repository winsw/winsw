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
using winsw.Extensions;
using winsw.Logging;
using winsw.Native;
using winsw.Util;

namespace winsw
{
    public class WrapperService : ServiceBase, EventLogger
    {
        private ServiceApis.SERVICE_STATUS _wrapperServiceStatus;

        private readonly Process _process = new Process();
        private readonly ServiceDescriptor _descriptor;
        private Dictionary<string, string>? _envs;

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
        private bool _orderlyShutdown;
        private bool _systemShuttingdown;

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
        public bool IsShuttingDown => _systemShuttingdown;

        public WrapperService(ServiceDescriptor descriptor)
        {
            _descriptor = descriptor;
            ServiceName = _descriptor.Id;
            ExtensionManager = new WinSWExtensionManager(_descriptor);
            CanShutdown = true;
            CanStop = true;
            CanPauseAndContinue = false;
            AutoLog = true;
            _systemShuttingdown = false;

            // Register the event log provider
            eventLogProvider.service = this;
        }

        public WrapperService() : this(new ServiceDescriptor())
        {
        }

        /// <summary>
        /// Process the file copy instructions, so that we can replace files that are always in use while
        /// the service runs.
        /// </summary>
        private void HandleFileCopies()
        {
            var file = _descriptor.BasePath + ".copies";
            if (!File.Exists(file))
                return; // nothing to handle

            try
            {
                using var tr = new StreamReader(file, Encoding.UTF8);
                string? line;
                while ((line = tr.ReadLine()) != null)
                {
                    LogEvent("Handling copy: " + line);
                    string[] tokens = line.Split('>');
                    if (tokens.Length > 2)
                    {
                        LogEvent("Too many delimiters in " + line);
                        continue;
                    }

                    MoveFile(tokens[0], tokens[1]);
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
                LogEvent("Failed to move :" + sourceFileName + " to " + destFileName + " because " + e.Message);
            }
        }

        /// <summary>
        /// Handle the creation of the logfiles based on the optional logmode setting.
        /// </summary>
        /// <returns>Log Handler, which should be used for the spawned process</returns>
        private LogHandler CreateExecutableLogHandler()
        {
            string logDirectory = _descriptor.LogDirectory;

            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            LogHandler logAppender = _descriptor.LogHandler;
            logAppender.EventLogger = this;
            return logAppender;
        }

        public void LogEvent(string message)
        {
            if (_systemShuttingdown)
            {
                /* NOP - cannot call EventLog because of shutdown. */
            }
            else
            {
                try
                {
                    EventLog.WriteEntry(message);
                }
                catch (Exception e)
                {
                    Log.Error("Failed to log event in Windows Event Log: " + message + "; Reason: ", e);
                }
            }
        }

        public void LogEvent(string message, EventLogEntryType type)
        {
            if (_systemShuttingdown)
            {
                /* NOP - cannot call EventLog because of shutdown. */
            }
            else
            {
                try
                {
                    EventLog.WriteEntry(message, type);
                }
                catch (Exception e)
                {
                    Log.Error("Failed to log event in Windows Event Log. Reason: ", e);
                }
            }
        }

        protected override void OnStart(string[] args)
        {
            _envs = _descriptor.EnvironmentVariables;
            // TODO: Disabled according to security concerns in https://github.com/kohsuke/winsw/issues/54
            // Could be restored, but unlikely it's required in event logs at all
            /**
            foreach (string key in _envs.Keys)
            {
                LogEvent("envar " + key + '=' + _envs[key]);
            }*/
            HandleFileCopies();

            // handle downloads
#if VNEXT
            List<Download> downloads = _descriptor.Downloads;
            Task[] tasks = new Task[downloads.Count];
            for (int i = 0; i < downloads.Count; i++)
            {
                Download download = downloads[i];
                string downloadMessage = $"Downloading: {download.From} to {download.To}. failOnError={download.FailOnError.ToString()}";
                LogEvent(downloadMessage);
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
                        LogEvent($"{errorMessage}. {exception.Message}");
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
            foreach (Download download in _descriptor.Downloads)
            {
                string downloadMessage = $"Downloading: {download.From} to {download.To}. failOnError={download.FailOnError.ToString()}";
                LogEvent(downloadMessage);
                Log.Info(downloadMessage);
                try
                {
                    download.Perform();
                }
                catch (Exception e)
                {
                    string errorMessage = $"Failed to download {download.From} to {download.To}";
                    LogEvent($"{errorMessage}. {e.Message}");
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

            string? startArguments = _descriptor.StartArguments;

            if (startArguments is null)
            {
                startArguments = _descriptor.Arguments;
            }
            else
            {
                startArguments += " " + _descriptor.Arguments;
            }

            // Converting newlines, line returns, tabs into a single 
            // space. This allows users to provide multi-line arguments
            // in the xml for readability.
            startArguments = Regex.Replace(startArguments, @"\s*[\n\r]+\s*", " ");

            LogEvent("Starting " + _descriptor.Executable + ' ' + startArguments);
            Log.Info("Starting " + _descriptor.Executable + ' ' + startArguments);

            // Load and start extensions
            ExtensionManager.LoadExtensions();
            ExtensionManager.FireOnWrapperStarted();

            LogHandler executableLogHandler = CreateExecutableLogHandler();
            StartProcess(_process, startArguments, _descriptor.Executable, executableLogHandler, true);
            ExtensionManager.FireOnProcessStarted(_process);

            _process.StandardInput.Close(); // nothing for you to read!
        }

        protected override void OnShutdown()
        {
            // WriteEvent("OnShutdown");

            try
            {
                _systemShuttingdown = true;
                StopIt();
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
                StopIt();
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
            string? stopArguments = _descriptor.StopArguments;
            LogEvent("Stopping " + _descriptor.Id);
            Log.Info("Stopping " + _descriptor.Id);
            _orderlyShutdown = true;

            if (stopArguments is null)
            {
                try
                {
                    Log.Debug("ProcessKill " + _process.Id);
                    ProcessHelper.StopProcessAndChildren(_process.Id, _descriptor.StopTimeout, _descriptor.StopParentProcessFirst);
                    ExtensionManager.FireOnProcessTerminated(_process);
                }
                catch (InvalidOperationException)
                {
                    // already terminated
                }
            }
            else
            {
                SignalShutdownPending();

                stopArguments += " " + _descriptor.Arguments;

                Process stopProcess = new Process();
                string? executable = _descriptor.StopExecutable;

                executable ??= _descriptor.Executable;

                // TODO: Redirect logging to Log4Net once https://github.com/kohsuke/winsw/pull/213 is integrated
                StartProcess(stopProcess, stopArguments, executable, null, false);

                Log.Debug("WaitForProcessToExit " + _process.Id + "+" + stopProcess.Id);
                WaitForProcessToExit(_process);
                WaitForProcessToExit(stopProcess);
            }

            // Stop extensions
            ExtensionManager.FireBeforeWrapperStopped();

            if (_systemShuttingdown && _descriptor.BeepOnShutdown)
            {
                Console.Beep();
            }

            Log.Info("Finished " + _descriptor.Id);
        }

        private void WaitForProcessToExit(Process processoWait)
        {
            SignalShutdownPending();

            int effectiveProcessWaitSleepTime;
            if (_descriptor.SleepTime.TotalMilliseconds > int.MaxValue)
            {
                Log.Warn("The requested sleep time " + _descriptor.SleepTime.TotalMilliseconds + "is greater that the max value " +
                    int.MaxValue + ". The value will be truncated");
                effectiveProcessWaitSleepTime = int.MaxValue;
            }
            else
            {
                effectiveProcessWaitSleepTime = (int)_descriptor.SleepTime.TotalMilliseconds;
            }

            try
            {
                // WriteEvent("WaitForProcessToExit [start]");

                while (!processoWait.WaitForExit(effectiveProcessWaitSleepTime))
                {
                    SignalShutdownPending();
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
            if (_descriptor.WaitHint.TotalMilliseconds > int.MaxValue)
            {
                Log.Warn("The requested WaitHint value (" + _descriptor.WaitHint.TotalMilliseconds + " ms)  is greater that the max value " +
                    int.MaxValue + ". The value will be truncated");
                effectiveWaitHint = int.MaxValue;
            }
            else
            {
                effectiveWaitHint = (int)_descriptor.WaitHint.TotalMilliseconds;
            }

            RequestAdditionalTime(effectiveWaitHint);
        }

        private void SignalShutdownComplete()
        {
            IntPtr handle = ServiceHandle;
            _wrapperServiceStatus.CheckPoint++;
            // WriteEvent("SignalShutdownComplete " + wrapperServiceStatus.checkPoint + ":" + wrapperServiceStatus.waitHint);
            _wrapperServiceStatus.CurrentState = ServiceControllerStatus.Stopped;
            ServiceApis.SetServiceStatus(handle, _wrapperServiceStatus);
        }

        private void StartProcess(Process processToStart, string arguments, string executable, LogHandler? logHandler, bool redirectStdin)
        {
            // Define handler of the completed process
            void OnProcessCompleted(Process proc)
            {
                string msg = processToStart.Id + " - " + processToStart.StartInfo.FileName + " " + processToStart.StartInfo.Arguments;
                try
                {
                    if (_orderlyShutdown)
                    {
                        LogEvent("Child process [" + msg + "] terminated with " + proc.ExitCode, EventLogEntryType.Information);
                    }
                    else
                    {
                        LogEvent("Child process [" + msg + "] finished with " + proc.ExitCode, EventLogEntryType.Warning);
                        // if we finished orderly, report that to SCM.
                        // by not reporting unclean shutdown, we let Windows SCM to decide if it wants to
                        // restart the service automatically
                        if (proc.ExitCode == 0)
                            SignalShutdownComplete();

                        Environment.Exit(proc.ExitCode);
                    }
                }
                catch (InvalidOperationException ioe)
                {
                    LogEvent("WaitForExit " + ioe.Message);
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
                envVars: _envs,
                workingDirectory: _descriptor.WorkingDirectory,
                priority: _descriptor.Priority,
                callback: OnProcessCompleted,
                logHandler: logHandler,
                redirectStdin: redirectStdin,
                hideWindow: _descriptor.HideWindow);
        }
    }
}

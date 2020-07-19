using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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

        internal WinSWExtensionManager ExtensionManager { get; }

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

            this.HandleFileCopies();

            // handle downloads
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

            try
            {
                string? prestartExecutable = this.descriptor.PrestartExecutable;
                if (prestartExecutable != null)
                {
                    using Process process = this.StartProcess(prestartExecutable, this.descriptor.PrestartArguments);
                    this.WaitForProcessToExit(process);
                    Log.Info($"Pre-start process '{GetDisplayName(process)}' exited with code {process.ExitCode}.");
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }

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

            try
            {
                string? poststartExecutable = this.descriptor.PoststartExecutable;
                if (poststartExecutable != null)
                {
                    using Process process = this.StartProcess(poststartExecutable, this.descriptor.PoststartArguments);
                    process.Exited += (sender, _) =>
                    {
                        Process process = (Process)sender!;
                        Log.Info($"Post-start process '{GetDisplayName(process)}' exited with code {process.ExitCode}.");
                    };

                    process.EnableRaisingEvents = true;
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
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
            try
            {
                string? prestopExecutable = this.descriptor.PrestopExecutable;
                if (prestopExecutable != null)
                {
                    using Process process = this.StartProcess(prestopExecutable, this.descriptor.PrestopArguments);
                    this.WaitForProcessToExit(process);
                    Log.Info($"Pre-stop process '{GetDisplayName(process)}' exited with code {process.ExitCode}.");
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }

            string? stopArguments = this.descriptor.StopArguments;
            this.LogEvent("Stopping " + this.descriptor.Id);
            Log.Info("Stopping " + this.descriptor.Id);
            this.orderlyShutdown = true;
            this.process.EnableRaisingEvents = false;

            if (stopArguments is null)
            {
                Log.Debug("ProcessKill " + this.process.Id);
                ProcessHelper.StopProcessTree(this.process, this.descriptor.StopTimeout);
                this.ExtensionManager.FireOnProcessTerminated(this.process);
            }
            else
            {
                this.SignalPending();

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

            try
            {
                string? poststopExecutable = this.descriptor.PoststopExecutable;
                if (poststopExecutable != null)
                {
                    using Process process = this.StartProcess(poststopExecutable, this.descriptor.PoststopArguments);
                    this.WaitForProcessToExit(process);
                    Log.Info($"Post-stop process '{GetDisplayName(process)}' exited with code {process.ExitCode}.");
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }

            // Stop extensions
            this.ExtensionManager.FireBeforeWrapperStopped();

            if (this.systemShuttingdown && this.descriptor.BeepOnShutdown)
            {
                Console.Beep();
            }

            Log.Info("Finished " + this.descriptor.Id);
        }

        private void WaitForProcessToExit(Process process)
        {
            this.SignalPending();

            int processWaitHint = (int)Math.Min(this.descriptor.SleepTime.TotalMilliseconds, int.MaxValue);

            while (!process.WaitForExit(processWaitHint))
            {
                this.SignalPending();
            }
        }

        private void SignalPending()
        {
            int serviceWaitHint = (int)Math.Min(this.descriptor.WaitHint.TotalMilliseconds, int.MaxValue);

            this.RequestAdditionalTime(serviceWaitHint);
        }

        private void SignalShutdownComplete()
        {
            IntPtr handle = this.ServiceHandle;
            this.wrapperServiceStatus.CheckPoint++;
            // WriteEvent("SignalShutdownComplete " + wrapperServiceStatus.checkPoint + ":" + wrapperServiceStatus.waitHint);
            this.wrapperServiceStatus.CurrentState = ServiceControllerStatus.Stopped;
            ServiceApis.SetServiceStatus(handle, this.wrapperServiceStatus);
        }

        private void StartProcess(Process processToStart, string arguments, string executable, LogHandler? logHandler, bool redirectStdin)
        {
            // Define handler of the completed process
            void OnProcessCompleted(Process process)
            {
                string msg = process.Id + " - " + process.StartInfo.FileName + " " + process.StartInfo.Arguments;

                if (this.orderlyShutdown)
                {
                    this.LogEvent("Child process [" + msg + "] terminated with " + process.ExitCode, EventLogEntryType.Information);
                }
                else
                {
                    this.LogEvent("Child process [" + msg + "] finished with " + process.ExitCode, EventLogEntryType.Warning);

                    // if we finished orderly, report that to SCM.
                    // by not reporting unclean shutdown, we let Windows SCM to decide if it wants to
                    // restart the service automatically
                    if (process.ExitCode == 0)
                    {
                        this.SignalShutdownComplete();
                    }

                    Environment.Exit(process.ExitCode);
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

        private Process StartProcess(string executable, string? arguments)
        {
            var info = new ProcessStartInfo(executable, arguments)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                WorkingDirectory = this.descriptor.WorkingDirectory,
            };

            Process process = Process.Start(info);
            process.StandardInput.Close();
            return process;
        }

        private static string GetDisplayName(Process process)
        {
            return $"{process.ProcessName} ({process.Id})";
        }
    }
}

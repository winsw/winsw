using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using log4net;
using WinSW.Extensions;
using WinSW.Logging;
using WinSW.Native;
using WinSW.Util;
using Messages = WinSW.ServiceMessages;

namespace WinSW
{
    public sealed class WrapperService : ServiceBase, IEventLogger, IServiceEventLog
    {
        internal static readonly WrapperServiceEventLogProvider EventLogProvider = new();

        private static readonly int AdditionalStopTimeout = 1_000;

        private static readonly ILog Log = LogManager.GetLogger(LoggerNames.Service);

        private readonly XmlServiceConfig config;

        private Process process = null!;
        private volatile Process? startingProcess;
        private volatile Process? stoppingProcess;

        internal WinSWExtensionManager ExtensionManager { get; }

        private SharedDirectoryMapper? sharedDirectoryMapper;

        private bool shuttingdown;

        /// <summary>
        /// Version of Windows service wrapper
        /// </summary>
        /// <remarks>
        /// The version will be taken from <see cref="AssemblyInfo"/>
        /// </remarks>
        public static Version Version => Assembly.GetExecutingAssembly().GetName().Version!;

        public WrapperService(XmlServiceConfig config)
        {
            this.ServiceName = config.Name;
            this.CanStop = true;
            this.AutoLog = false;

            this.config = config;
            this.ExtensionManager = new WinSWExtensionManager(config);

            // Register the event log provider
            EventLogProvider.Service = this;

            if (config.Preshutdown)
            {
                this.AcceptPreshutdown();
            }

            Environment.CurrentDirectory = config.WorkingDirectory;
        }

        /// <summary>
        /// Process the file copy instructions, so that we can replace files that are always in use while
        /// the service runs.
        /// </summary>
        private void HandleFileCopies()
        {
            string? file = this.config.BasePath + ".copies";
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
                    Log.Info("Handling copy: " + line);
                    string[] tokens = line.Split('>');
                    if (tokens.Length > 2)
                    {
                        Log.Error("Too many delimiters in " + line);
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
                Log.Error("Failed to move :" + sourceFileName + " to " + destFileName + " because " + e.Message);
            }
        }

        /// <summary>
        /// Handle the creation of the logfiles based on the optional logmode setting.
        /// </summary>
        /// <returns>Log Handler, which should be used for the spawned process</returns>
        private LogHandler CreateExecutableLogHandler()
        {
            string logDirectory = this.config.LogDirectory;

            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            var logAppender = this.config.LogHandler;
            logAppender.EventLogger = this;
            return logAppender;
        }

        public void WriteEntry(string message)
        {
            if (this.shuttingdown)
            {
                // The Event Log service exits earlier.
                return;
            }

            try
            {
                this.EventLog.WriteEntry(message);
            }
            catch (Exception e)
            {
                Log.Error("Failed to log event in Windows Event Log: " + message + "; Reason: ", e);
            }
        }

        public void WriteEntry(string message, EventLogEntryType type)
        {
            if (this.shuttingdown)
            {
                // The Event Log service exits earlier.
                return;
            }

            try
            {
                this.EventLog.WriteEntry(message, type);
            }
            catch (Exception e)
            {
                Log.Error("Failed to log event in Windows Event Log. Reason: ", e);
            }
        }

        void IServiceEventLog.WriteEntry(string message, EventLogEntryType type)
        {
            if (this.shuttingdown)
            {
                // The Event Log service exits earlier.
                return;
            }

            this.EventLog.WriteEntry(message, type);
        }

        private void LogExited(string message, int exitCode)
        {
            if (exitCode == 0)
            {
                Log.Info(message);
            }
            else
            {
                Log.Warn(message);
            }
        }

        private void LogMinimal(string message)
        {
            this.WriteEntry(message);
            Log.Info(message);
        }

        internal void RaiseOnStart(string[] args) => this.OnStart(args);

        internal void RaiseOnStop() => this.OnStop();

        protected override void OnStart(string[] args)
        {
            try
            {
                this.DoStart();
                this.LogMinimal(Messages.StartedSuccessfully);
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
                this.LogMinimal(Messages.StoppedSuccessfully);
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
                this.LogMinimal("Service was shut down successfully.");
            }
            catch (Exception e)
            {
                Log.Error("Failed to shut down service.", e);
                throw;
            }
        }

        protected override void OnCustomCommand(int command)
        {
            if (command == 0x0000000F)
            {
                // SERVICE_CONTROL_PRESHUTDOWN
                this.Stop();
            }
        }

        private void DoStart()
        {
            bool succeeded = ConsoleApis.FreeConsole();
            Debug.Assert(succeeded);
            succeeded = ConsoleApis.SetConsoleCtrlHandler(null, true);
            Debug.Assert(succeeded);

            this.HandleFileCopies();

            // handle downloads
            var downloads = this.config.Downloads;
            var tasks = new Task[downloads.Count];
            for (int i = 0; i < downloads.Count; i++)
            {
                var download = downloads[i];
                string downloadMessage = $"Downloading: {download.From} to {download.To}. failOnError={download.FailOnError.ToString()}";
                Log.Info(downloadMessage);
                tasks[i] = download.PerformAsync();
            }

            Task.WaitAll(tasks);

            var sharedDirectories = this.config.SharedDirectories;
            if (sharedDirectories.Count > 0)
            {
                this.sharedDirectoryMapper = new(sharedDirectories);
                this.sharedDirectoryMapper.Map();
            }

            var prestart = this.config.Prestart;
            string? prestartExecutable = prestart.Executable;
            if (prestartExecutable != null)
            {
                try
                {
                    using var process = this.StartProcess(prestartExecutable, prestart.Arguments, prestart.CreateLogHandler());
                    this.WaitForProcessToExit(process);
                    this.LogExited($"Pre-start process '{process.Format()}' exited with code {process.ExitCode}.", process.ExitCode);
                    process.StopDescendants(AdditionalStopTimeout);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }

            string startArguments = this.config.StartArguments ?? this.config.Arguments;

            Log.Info("Starting " + this.config.Executable);

            // Load and start extensions
            this.ExtensionManager.LoadExtensions();
            this.ExtensionManager.FireOnWrapperStarted();

            var executableLogHandler = this.CreateExecutableLogHandler();
            this.process = this.StartProcess(this.config.Executable, startArguments, executableLogHandler, this.OnMainProcessExited);
            this.ExtensionManager.FireOnProcessStarted(this.process);

            var poststart = this.config.Poststart;
            string? poststartExecutable = poststart.Executable;
            if (poststartExecutable != null)
            {
                try
                {
                    using var process = StartProcessLocked();
                    this.WaitForProcessToExit(process);
                    this.LogExited($"Post-start process '{process.Format()}' exited with code {process.ExitCode}.", process.ExitCode);
                    process.StopDescendants(AdditionalStopTimeout);
                    this.startingProcess = null;

                    Process StartProcessLocked()
                    {
                        lock (this)
                        {
                            return this.startingProcess = this.StartProcess(poststartExecutable, poststart.Arguments, poststart.CreateLogHandler());
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        /// <summary>
        /// Called when we are told by Windows SCM to exit.
        /// </summary>
        private void DoStop()
        {
            var prestop = this.config.Prestop;
            string? prestopExecutable = prestop.Executable;
            if (prestopExecutable != null)
            {
                try
                {
                    using var process = StartProcessLocked(prestopExecutable, prestop.Arguments, prestop.CreateLogHandler());
                    this.WaitForProcessToExit(process);
                    this.LogExited($"Pre-stop process '{process.Format()}' exited with code {process.ExitCode}.", process.ExitCode);
                    process.StopDescendants(AdditionalStopTimeout);
                    this.stoppingProcess = null;
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }

            Log.Info("Stopping " + this.config.Name);
            this.process.EnableRaisingEvents = false;

            string? stopExecutable = this.config.StopExecutable;
            string? stopArguments = this.config.StopArguments;
            if (stopExecutable is null && stopArguments is null)
            {
                var process = this.process;
                Log.Debug("ProcessKill " + process.Id);
                bool? result = process.Stop(this.config.StopTimeoutInMs);
                this.LogMinimal($"Child process '{process.Format()}' " + result switch
                {
                    true => $"canceled with code {process.ExitCode}.",
                    false => "terminated.",
                    null => $"finished with code '{process.ExitCode}'."
                });
                this.process.StopDescendants(this.config.StopTimeoutInMs);
                this.ExtensionManager.FireOnProcessTerminated(process);
            }
            else
            {
                this.SignalPending();

                stopExecutable ??= this.config.Executable;

                try
                {
                    // TODO: Redirect logging to Log4Net once https://github.com/kohsuke/winsw/pull/213 is integrated
                    using var stopProcess = StartProcessLocked(stopExecutable, stopArguments);

                    Log.Debug("WaitForProcessToExit " + this.process.Id + "+" + stopProcess.Id);
                    this.WaitForProcessToExit(stopProcess);
                    stopProcess.StopDescendants(AdditionalStopTimeout);
                    this.stoppingProcess = null;

                    this.WaitForProcessToExit(this.process);
                    this.process.StopDescendants(this.config.StopTimeoutInMs);
                }
                catch
                {
                    this.process.StopTree(this.config.StopTimeoutInMs);
                    throw;
                }
            }

            var poststop = this.config.Poststop;
            string? poststopExecutable = poststop.Executable;
            if (poststopExecutable != null)
            {
                try
                {
                    using var process = StartProcessLocked(poststopExecutable, poststop.Arguments, poststop.CreateLogHandler());
                    this.WaitForProcessToExit(process);
                    this.LogExited($"Post-stop process '{process.Format()}' exited with code {process.ExitCode}.", process.ExitCode);
                    process.StopDescendants(AdditionalStopTimeout);
                    this.stoppingProcess = null;
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }

            try
            {
                this.sharedDirectoryMapper?.Unmap();
            }
            catch (Exception e)
            {
                Log.Error(e);
            }

            // Stop extensions
            this.ExtensionManager.FireBeforeWrapperStopped();

            if (this.shuttingdown && this.config.BeepOnShutdown)
            {
                Console.Beep();
            }

            Log.Info("Finished " + this.config.Name);

            Process StartProcessLocked(string executable, string? arguments, LogHandler? logHandler = null)
            {
                lock (this)
                {
                    return this.stoppingProcess = this.StartProcess(executable, arguments, logHandler);
                }
            }
        }

        private void WaitForProcessToExit(Process process)
        {
            this.SignalPending();

            // A good interval is one-tenth of the wait hint but not less than 1 second and not more than 10 seconds.
            while (!process.WaitForExit(1_500))
            {
                this.SignalPending();
            }
        }

        /// <exception cref="MissingFieldException" />
        private void AcceptPreshutdown()
        {
            const string acceptedCommandsFieldName =
#if NET
                "_acceptedCommands";
#else
                "acceptedCommands";
#endif

            var acceptedCommandsField = typeof(ServiceBase).GetField(acceptedCommandsFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (acceptedCommandsField is null)
            {
                throw new MissingFieldException(nameof(ServiceBase), acceptedCommandsFieldName);
            }

            int acceptedCommands = (int)acceptedCommandsField.GetValue(this)!;
            acceptedCommands |= 0x00000100; // SERVICE_ACCEPT_PRESHUTDOWN
            acceptedCommandsField.SetValue(this, acceptedCommands);
        }

        private void SignalPending()
        {
            this.RequestAdditionalTime(15_000);
        }

        private void SignalStopped()
        {
            using var scm = ServiceManager.Open();
            using var sc = scm.OpenService(this.ServiceName, ServiceApis.ServiceAccess.QueryStatus);

            sc.SetStatus(this.ServiceHandle, ServiceControllerStatus.Stopped);
        }

        private void OnMainProcessExited(Process process)
        {
            lock (this)
            {
                try
                {
                    Log.Warn($"Child process '{process.Format()}' finished with code {process.ExitCode}.");

                    process.StopDescendants(this.config.StopTimeoutInMs);

                    this.startingProcess?.StopTree(AdditionalStopTimeout);
                    this.stoppingProcess?.StopTree(AdditionalStopTimeout);

                    // if we finished orderly, report that to SCM.
                    // by not reporting unclean shutdown, we let Windows SCM to decide if it wants to
                    // restart the service automatically
                    if (process.ExitCode == 0)
                    {
                        this.SignalStopped();
                    }
                }
                finally
                {
                    Environment.Exit(process.ExitCode);
                }
            }
        }

        /// <summary>
        /// <paramref name="onExited"/> will not be raised if <see cref="Process.EnableRaisingEvents"/> is <see langword="false"/>.
        /// </summary>
        private Process StartProcess(string executable, string? arguments, LogHandler? logHandler = null, Action<Process>? onExited = null)
        {
            var startInfo = new ProcessStartInfo(executable, arguments ?? string.Empty)
            {
                UseShellExecute = false,
                WorkingDirectory = this.config.WorkingDirectory,
                CreateNoWindow = this.config.HideWindow,
                RedirectStandardOutput = logHandler?.OutFileDisabled == false,
                RedirectStandardError = logHandler?.ErrFileDisabled == false,
            };

            var environment = this.config.EnvironmentVariables;
            if (environment.Count > 0)
            {
                var newEnvironment =
#if NET
                    startInfo.Environment;
#else
                    startInfo.EnvironmentVariables;
#endif
                foreach (var pair in environment)
                {
                    newEnvironment[pair.Key] = pair.Value;
                }
            }

            bool succeeded = ConsoleApis.AllocConsole(); // inherited
            Debug.Assert(succeeded);
            succeeded = ConsoleApis.SetConsoleCtrlHandler(null, false); // inherited
            Debug.Assert(succeeded);
            succeeded = ConsoleApis.SetConsoleOutputCP(ConsoleApis.CP_UTF8);
            Debug.Assert(succeeded);

            Process process;
            try
            {
                process = Process.Start(startInfo)!;
            }
            finally
            {
                succeeded = ConsoleApis.FreeConsole();
                Debug.Assert(succeeded);
                succeeded = ConsoleApis.SetConsoleCtrlHandler(null, true);
                Debug.Assert(succeeded);
            }

            Log.Info($"Started process {process.Format()}.");

            if (this.config.Priority is ProcessPriorityClass priority)
            {
                try
                {
                    process.PriorityClass = priority;
                }
                catch (InvalidOperationException)
                {
                    // exited
                }
            }

            if (logHandler != null)
            {
                logHandler.Log(
                    startInfo.RedirectStandardOutput ? process.StandardOutput : StreamReader.Null,
                    startInfo.RedirectStandardError ? process.StandardError : StreamReader.Null);
            }

            if (onExited != null)
            {
                process.Exited += (sender, _) =>
                {
                    var process = (Process)sender!;

                    if (!process.EnableRaisingEvents)
                    {
                        return;
                    }

                    try
                    {
                        onExited(process);
                    }
                    catch (Exception e)
                    {
                        Log.Error("Unhandled exception in event handler.", e);
                    }
                };

                process.EnableRaisingEvents = true;
            }

            return process;
        }
    }
}

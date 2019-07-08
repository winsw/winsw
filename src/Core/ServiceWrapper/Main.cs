using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
#if VNEXT
using System.Threading.Tasks;
#endif
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Layout;
using Microsoft.Win32;
using winsw.Extensions;
using winsw.Logging;
using winsw.Native;
using winsw.Util;
using WMI;
using ServiceType = WMI.ServiceType;

namespace winsw
{
    public class WrapperService : ServiceBase, EventLogger
    {
        private SERVICE_STATUS _wrapperServiceStatus;

        private readonly Process _process = new Process();
        private readonly ServiceDescriptor _descriptor;
        private Dictionary<string, string>? _envs;

        internal WinSWExtensionManager ExtensionManager { get; private set; }

        private static readonly ILog Log = LogManager.GetLogger(
#if NETCOREAPP
            Assembly.GetExecutingAssembly(),
#endif
            "WinSW");
        private static readonly WrapperServiceEventLogProvider eventLogProvider = new WrapperServiceEventLogProvider();

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

            Task.WhenAll(tasks);
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
                        throw new IOException(errorMessage, exception);
                    }
                }
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

            string? startarguments = _descriptor.Startarguments;

            if (startarguments is null)
            {
                startarguments = _descriptor.Arguments;
            }
            else
            {
                startarguments += " " + _descriptor.Arguments;
            }

            LogEvent("Starting " + _descriptor.Executable + ' ' + startarguments);
            Log.Info("Starting " + _descriptor.Executable + ' ' + startarguments);

            // Load and start extensions
            ExtensionManager.LoadExtensions();
            ExtensionManager.FireOnWrapperStarted();

            LogHandler executableLogHandler = CreateExecutableLogHandler();
            StartProcess(_process, startarguments, _descriptor.Executable, executableLogHandler, true);
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

        /// <summary>
        /// Called when we are told by Windows SCM to exit.
        /// </summary>
        private void StopIt()
        {
            string? stoparguments = _descriptor.Stoparguments;
            LogEvent("Stopping " + _descriptor.Id);
            Log.Info("Stopping " + _descriptor.Id);
            _orderlyShutdown = true;

            if (stoparguments is null)
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

                stoparguments += " " + _descriptor.Arguments;

                Process stopProcess = new Process();
                string? executable = _descriptor.StopExecutable;

                executable ??= _descriptor.Executable;

                // TODO: Redirect logging to Log4Net once https://github.com/kohsuke/winsw/pull/213 is integrated
                StartProcess(stopProcess, stoparguments, executable, null, false);

                Log.Debug("WaitForProcessToExit " + _process.Id + "+" + stopProcess.Id);
                WaitForProcessToExit(_process);
                WaitForProcessToExit(stopProcess);
                SignalShutdownComplete();
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

            IntPtr handle = ServiceHandle;
            _wrapperServiceStatus.checkPoint++;
            _wrapperServiceStatus.waitHint = effectiveWaitHint;
            // WriteEvent("SignalShutdownPending " + wrapperServiceStatus.checkPoint + ":" + wrapperServiceStatus.waitHint);
            _wrapperServiceStatus.currentState = (int)State.SERVICE_STOP_PENDING;
            Advapi32.SetServiceStatus(handle, ref _wrapperServiceStatus);
        }

        private void SignalShutdownComplete()
        {
            IntPtr handle = ServiceHandle;
            _wrapperServiceStatus.checkPoint++;
            // WriteEvent("SignalShutdownComplete " + wrapperServiceStatus.checkPoint + ":" + wrapperServiceStatus.waitHint);
            _wrapperServiceStatus.currentState = (int)State.SERVICE_STOPPED;
            Advapi32.SetServiceStatus(handle, ref _wrapperServiceStatus);
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

        public static int Main(string[] args)
        {
            // Run app
            try
            {
                Run(args);
                Log.Debug("Completed. Exit code is 0");
                return 0;
            }
            catch (InvalidDataException e)
            {
                string message = "The configuration file cound not be loaded. " + e.Message;
                Log.Fatal(message, e);
                Console.Error.WriteLine(message);
                return -1;
            }
            catch (WmiException e)
            {
                Log.Fatal("WMI Operation failure: " + e.ErrorCode, e);
                Console.Error.WriteLine(e);
                return (int)e.ErrorCode;
            }
            catch (Exception e)
            {
                Log.Fatal("Unhandled exception", e);
                Console.Error.WriteLine(e);
                return -1;
            }
        }

        [DoesNotReturn]
        private static void ThrowNoSuchService()
        {
            throw new WmiException(ReturnValue.NoSuchService);
        }

        // ReSharper disable once InconsistentNaming
        /// <summary>
        /// Runs the wrapper.
        /// </summary>
        /// <param name="_args">Arguments.</param>
        /// <param name="descriptor">Service descriptor. If null, it will be initialized within the method.
        ///                          In such case configs will be loaded from the XML Configuration File.</param>
        /// <exception cref="Exception">Any unhandled exception</exception>
        public static void Run(string[] _args, ServiceDescriptor? descriptor = null)
        {
            bool inCliMode = Console.OpenStandardInput() != Stream.Null;

            // If descriptor is not specified, initialize the new one (and load configs from there)
            descriptor ??= new ServiceDescriptor();

            // Configure the wrapper-internal logging.
            // STDIN and STDOUT of the child process will be handled independently.
            InitLoggers(descriptor, inCliMode);

            if (!inCliMode)
            {
                Log.Debug("Starting WinSW in the service mode");
                Run(new WrapperService(descriptor));
                return;
            }

            Log.Debug("Starting WinSW in the CLI mode");

            if (_args.Length == 0)
            {
                printHelp();
                return;
            }

            // Get service info for the future use
            Win32Services svc = new WmiRoot().GetCollection<Win32Services>();
            Win32Service? s = svc.Select(descriptor.Id);

            var args = new List<string>(Array.AsReadOnly(_args));
            if (args[0] == "/redirect")
            {
                // Redirect output
                // One might ask why we support this when the caller
                // can redirect the output easily. The answer is for supporting UAC.
                // On UAC-enabled Windows such as Vista, SCM operation requires
                // elevated privileges, thus winsw.exe needs to be launched
                // accordingly. This in turn limits what the caller can do,
                // and among other things it makes it difficult for the caller
                // to read stdout/stderr. Thus redirection becomes handy.
                var f = new FileStream(args[1], FileMode.Create);
                var w = new StreamWriter(f) { AutoFlush = true };
                Console.SetOut(w);
                Console.SetError(w);

                var handle = f.SafeFileHandle;
                Kernel32.SetStdHandle(-11, handle); // set stdout
                Kernel32.SetStdHandle(-12, handle); // set stder

                args = args.GetRange(2, args.Count - 2);
            }

            args[0] = args[0].ToLower();
            if (args[0] == "install")
            {
                Log.Info("Installing the service with id '" + descriptor.Id + "'");

                // Check if the service exists
                if (s != null)
                {
                    Console.WriteLine("Service with id '" + descriptor.Id + "' already exists");
                    Console.WriteLine("To install the service, delete the existing one or change service Id in the configuration file");
                    throw new Exception("Installation failure: Service with id '" + descriptor.Id + "' already exists");
                }

                string? username = null;
                string? password = null;
                bool setallowlogonasaserviceright = false; // This variable is very readable.
                if (args.Count > 1 && args[1] == "/p")
                {
                    // we expected username/password on stdin
                    Console.Write("Username: ");
                    username = Console.ReadLine();
                    Console.Write("Password: ");
                    password = ReadPassword();
                    Console.WriteLine();
                    Console.Write("Set Account rights to allow log on as a service (y/n)?: ");
                    var keypressed = Console.ReadKey();
                    Console.WriteLine();
                    if (keypressed.Key == ConsoleKey.Y)
                    {
                        setallowlogonasaserviceright = true;
                    }
                }
                else
                {
                    if (descriptor.HasServiceAccount())
                    {
                        username = descriptor.ServiceAccountUser;
                        password = descriptor.ServiceAccountPassword;
                        setallowlogonasaserviceright = descriptor.AllowServiceAcountLogonRight;
                    }
                }

                if (setallowlogonasaserviceright)
                {
                    LogonAsAService.AddLogonAsAServiceRight(username!);
                }

                svc.Create(
                    descriptor.Id,
                    descriptor.Caption,
                    "\"" + descriptor.ExecutablePath + "\"",
                    ServiceType.OwnProcess,
                    ErrorControl.UserNotified,
                    descriptor.StartMode.ToString(),
                    descriptor.Interactive,
                    username,
                    password,
                    descriptor.ServiceDependencies);

                // update the description
                /* Somehow this doesn't work, even though it doesn't report an error
                Win32Service s = svc.Select(d.Id);
                s.Description = d.Description;
                s.Commit();
                 */

                // so using a classic method to set the description. Ugly.
                Registry.LocalMachine
                    .OpenSubKey("System")
                    .OpenSubKey("CurrentControlSet")
                    .OpenSubKey("Services")
                    .OpenSubKey(descriptor.Id, true)
                    .SetValue("Description", descriptor.Description);

                var actions = descriptor.FailureActions;
                var isDelayedAutoStart = descriptor.StartMode == StartMode.Automatic && descriptor.DelayedAutoStart;
                if (actions.Count > 0 || isDelayedAutoStart)
                {
                    using ServiceManager scm = new ServiceManager();
                    using Service sc = scm.Open(descriptor.Id);

                    // Delayed auto start
                    if (isDelayedAutoStart)
                    {
                        sc.SetDelayedAutoStart(true);
                    }

                    // Set the failure actions
                    if (actions.Count > 0)
                    {
                        sc.ChangeConfig(descriptor.ResetFailureAfter, actions);
                    }
                }

                return;
            }

            if (args[0] == "uninstall")
            {
                Log.Info("Uninstalling the service with id '" + descriptor.Id + "'");
                if (s is null)
                {
                    Log.Warn("The service with id '" + descriptor.Id + "' does not exist. Nothing to uninstall");
                    return; // there's no such service, so consider it already uninstalled
                }

                if (s.Started)
                {
                    // We could fail the opeartion here, but it would be an incompatible change.
                    // So it is just a warning
                    Log.Warn("The service with id '" + descriptor.Id + "' is running. It may be impossible to uninstall it");
                }

                try
                {
                    s.Delete();
                }
                catch (WmiException e)
                {
                    if (e.ErrorCode == ReturnValue.ServiceMarkedForDeletion)
                    {
                        Log.Error("Failed to uninstall the service with id '" + descriptor.Id + "'"
                           + ". It has been marked for deletion.");

                        // TODO: change the default behavior to Error?
                        return; // it's already uninstalled, so consider it a success
                    }
                    else
                    {
                        Log.Fatal("Failed to uninstall the service with id '" + descriptor.Id + "'. WMI Error code is '" + e.ErrorCode + "'");
                    }

                    throw e;
                }

                return;
            }

            if (args[0] == "start")
            {
                Log.Info("Starting the service with id '" + descriptor.Id + "'");
                if (s is null)
                    ThrowNoSuchService();

                try
                {
                    s.StartService();
                }
                catch (WmiException e)
                {
                    if (e.ErrorCode == ReturnValue.ServiceAlreadyRunning)
                    {
                        Log.Info($"The service with ID '{descriptor.Id}' has already been started");
                    }
                    else
                    {
                        throw;
                    }
                }

                return;
            }

            if (args[0] == "stop")
            {
                Log.Info("Stopping the service with id '" + descriptor.Id + "'");
                if (s is null)
                    ThrowNoSuchService();

                s.StopService();
                return;
            }

            if (args[0] == "restart")
            {
                Log.Info("Restarting the service with id '" + descriptor.Id + "'");
                if (s is null)
                    ThrowNoSuchService();

                if (s.Started)
                    s.StopService();

                while (s.Started)
                {
                    Thread.Sleep(1000);
                    s = svc.Select(descriptor.Id)!;
                }

                s.StartService();
                return;
            }

            if (args[0] == "restart!")
            {
                Log.Info("Restarting the service with id '" + descriptor.Id + "'");

                // run restart from another process group. see README.md for why this is useful.

                STARTUPINFO si = default;
                bool result = Kernel32.CreateProcess(null, descriptor.ExecutablePath + " restart", IntPtr.Zero, IntPtr.Zero, false, 0x200/*CREATE_NEW_PROCESS_GROUP*/, IntPtr.Zero, null, ref si, out _);
                if (!result)
                {
                    throw new Exception("Failed to invoke restart: " + Marshal.GetLastWin32Error());
                }

                return;
            }

            if (args[0] == "status")
            {
                Log.Debug("User requested the status of the process with id '" + descriptor.Id + "'");
                if (s is null)
                    Console.WriteLine("NonExistent");
                else if (s.Started)
                    Console.WriteLine("Started");
                else
                    Console.WriteLine("Stopped");

                return;
            }

            if (args[0] == "test")
            {
                WrapperService wsvc = new WrapperService(descriptor);
                wsvc.OnStart(args.ToArray());
                Thread.Sleep(1000);
                wsvc.OnStop();
                return;
            }

            if (args[0] == "testwait")
            {
                WrapperService wsvc = new WrapperService(descriptor);
                wsvc.OnStart(args.ToArray());
                Console.WriteLine("Press any key to stop the service...");
                Console.Read();
                wsvc.OnStop();
                return;
            }

            if (args[0] == "help" || args[0] == "--help" || args[0] == "-h"
                || args[0] == "-?" || args[0] == "/?")
            {
                printHelp();
                return;
            }

            if (args[0] == "version")
            {
                printVersion();
                return;
            }

            Console.WriteLine("Unknown command: " + args[0]);
            printAvailableCommandsInfo();
            throw new Exception("Unknown command: " + args[0]);
        }

        private static void InitLoggers(ServiceDescriptor d, bool enableCLILogging)
        {
            // TODO: Make logging levels configurable
            Level logLevel = Level.Debug;
            // TODO: Debug should not be printed to console by default. Otherwise commands like 'status' will be pollutted
            // This is a workaround till there is a better command line parsing, which will allow determining
            Level consoleLogLevel = Level.Info;
            Level eventLogLevel = Level.Warn;

            // Legacy format from winsw-1.x: (DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - " + message);
            PatternLayout pl = new PatternLayout { ConversionPattern = "%d %-5p - %m%n" };
            pl.ActivateOptions();

            List<IAppender> appenders = new List<IAppender>();

            // wrapper.log
            string wrapperLogPath = Path.Combine(d.LogDirectory, d.BaseName + ".wrapper.log");
            var wrapperLog = new FileAppender
            {
                AppendToFile = true,
                File = wrapperLogPath,
                ImmediateFlush = true,
                Name = "Wrapper file log",
                Threshold = logLevel,
                LockingModel = new FileAppender.MinimalLock(),
                Layout = pl
            };
            wrapperLog.ActivateOptions();
            appenders.Add(wrapperLog);

            // Also display logs in CLI if required
            if (enableCLILogging)
            {
                var consoleAppender = new ConsoleAppender
                {
                    Name = "Wrapper console log",
                    Threshold = consoleLogLevel,
                    Layout = pl,
                };
                consoleAppender.ActivateOptions();
                appenders.Add(consoleAppender);
            }

            // System log
            var systemEventLogger = new ServiceEventLogAppender
            {
                Name = "System event log",
                Threshold = eventLogLevel,
                provider = eventLogProvider
            };
            systemEventLogger.ActivateOptions();
            appenders.Add(systemEventLogger);

            BasicConfigurator.Configure(
#if NETCOREAPP
                LogManager.GetRepository(Assembly.GetExecutingAssembly()),
#endif
                appenders.ToArray());
        }

        private static string ReadPassword()
        {
            StringBuilder buf = new StringBuilder();
            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                {
                    return buf.ToString();
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    buf.Remove(buf.Length - 1, 1);
                    Console.Write("\b \b");
                }
                else
                {
                    Console.Write('*');
                    buf.Append(key.KeyChar);
                }
            }
        }

        private static void printHelp()
        {
            Console.WriteLine("A wrapper binary that can be used to host executables as Windows services");
            Console.WriteLine();
            Console.WriteLine("Usage: winsw [/redirect file] <command> [<args>]");
            Console.WriteLine("       Missing arguments trigger the service mode");
            Console.WriteLine();
            printAvailableCommandsInfo();
            Console.WriteLine();
            Console.WriteLine("Extra options:");
            Console.WriteLine("  /redirect   redirect the wrapper's STDOUT and STDERR to the specified file");
            Console.WriteLine();
            printVersion();
            Console.WriteLine("More info: https://github.com/kohsuke/winsw");
            Console.WriteLine("Bug tracker: https://github.com/kohsuke/winsw/issues");
        }

        // TODO: Rework to enum in winsw-2.0
        private static void printAvailableCommandsInfo()
        {
            Console.WriteLine(
@"Available commands:
  install     install the service to Windows Service Controller
  uninstall   uninstall the service
  start       start the service (must be installed before)
  stop        stop the service
  restart     restart the service
  restart!    self-restart (can be called from child processes)
  status      check the current status of the service
  test        check if the service can be started and then stopped
  testwait    starts the service and waits until a key is pressed then stops the service
  version     print the version info
  help        print the help info (aliases: -h,--help,-?,/?)");
        }

        private static void printVersion()
        {
            Console.WriteLine("WinSW " + Version);
        }
    }
}

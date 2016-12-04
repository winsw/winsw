using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Microsoft.Win32;
using winsw.Extensions;
using winsw.Util;
using WMI;
using ServiceType = WMI.ServiceType;
using winsw.Native;
using System.Reflection;
using winsw.Logging;

namespace winsw
{
    public class WrapperService : ServiceBase, EventLogger, IEventWriter
    {
        private SERVICE_STATUS _wrapperServiceStatus;

        private readonly Process _process = new Process();
        private readonly ServiceDescriptor _descriptor;
        private Dictionary<string, string> _envs;

        internal WinSWExtensionManager ExtensionManager { private set; get; }

        private static readonly ILog Log = LogManager.GetLogger("WinSW");
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
        public static Version Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        /// <summary>
        /// Indicates that the system is shutting down.
        /// </summary>
        public bool IsShuttingDown {
            get { return _systemShuttingdown; }
        }

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

        public WrapperService() : this (new ServiceDescriptor())
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
                using (var tr = new StreamReader(file,Encoding.UTF8))
                {
                    string line;
                    while ((line = tr.ReadLine()) != null)
                    {
                        LogEvent("Handling copy: " + line);
                        string[] tokens = line.Split('>');
                        if (tokens.Length > 2)
                        {
                            LogEvent("Too many delimiters in " + line);
                            continue;
                        }

                        CopyFile(tokens[0], tokens[1]);
                    }
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
        private void CopyFile(string sourceFileName, string destFileName)
        {
            try
            {
                File.Delete(destFileName);
                File.Move(sourceFileName, destFileName);
            }
            catch (IOException e)
            {
                LogEvent("Failed to copy :" + sourceFileName + " to " + destFileName + " because " + e.Message);
            }
        }

        /// <summary>
        /// Starts a thread that protects the execution with a try/catch block.
        /// It appears that in .NET, unhandled exception in any thread causes the app to terminate
        /// http://msdn.microsoft.com/en-us/library/ms228965.aspx
        /// </summary>
        private void StartThread(ThreadStart main)
        {
            new Thread(delegate() {
                try
                {
                    main();
                }
                catch (Exception e)
                {
                    WriteEvent("Thread failed unexpectedly",e);
                }
            }).Start();
        }

        /// <summary>
        /// Handle the creation of the logfiles based on the optional logmode setting.
        /// </summary>
        private void HandleLogfiles()
        {
            string logDirectory = _descriptor.LogDirectory;

            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            LogHandler logAppender = _descriptor.LogHandler;
            logAppender.EventLogger = this;
            logAppender.log(_process.StandardOutput.BaseStream, _process.StandardError.BaseStream);
        }

        public void LogEvent(String message)
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
                    WriteEvent("Failed to log event in Windows Event Log: " + message + "; Reason: ", e);
                }
            }
        }

        public void LogEvent(String message, EventLogEntryType type)
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
                    WriteEvent("Failed to log event in Windows Event Log. Reason: ", e);
                }
            }
        }

        private void WriteEvent(Exception exception)
        {
            //TODO: pass exception to logger
            WriteEvent(exception.Message + "\nStacktrace:" + exception.StackTrace, Level.Error);
        }

        private void WriteEvent(String message, Exception exception)
        {
            //TODO: pass exception to logger
            WriteEvent(message + "\nMessage:" + exception.Message + "\nStacktrace:" + exception.StackTrace, Level.Error);
        }

        private void WriteEvent(String message, Level logLevel = null, Exception ex = null)
        {
            Log.Logger.Log(GetType(), logLevel ?? Level.Info, message, ex);
        }

        protected override void OnStart(string[] _)
        {
            _envs = _descriptor.EnvironmentVariables;
            foreach (string key in _envs.Keys)
            {
                LogEvent("envar " + key + '=' + _envs[key]);
            }

            HandleFileCopies();

            // handle downloads
            foreach (Download d in _descriptor.Downloads)
            {
                LogEvent("Downloading: " + d.From+ " to "+d.To);
                try
                {
                    d.Perform();
                }
                catch (Exception e)
                {
                    LogEvent("Failed to download " + d.From + " to " + d.To + "\n" + e.Message);
                    WriteEvent("Failed to download " + d.From +" to "+d.To, e);
                    // but just keep going
                }
            }

            string startarguments = _descriptor.Startarguments;

            if (startarguments == null)
            {
                startarguments = _descriptor.Arguments;
            }
            else
            {
                startarguments += " " + _descriptor.Arguments;
            }

            LogEvent("Starting " + _descriptor.Executable + ' ' + startarguments);
            WriteEvent("Starting " + _descriptor.Executable + ' ' + startarguments);

            // Load and start extensions
            ExtensionManager.LoadExtensions(this);
            try
            {
                ExtensionManager.OnStart(this);
            }
            catch (ExtensionException ex)
            {
                LogEvent("Failed to start extension  " + ex.ExtensionId + "\n" + ex.Message, EventLogEntryType.Error);
                WriteEvent("Failed to start extension  " + ex.ExtensionId, ex);
                //TODO: Exit on error?
            }

            LogEvent("Starting " + _descriptor.Executable + ' ' + startarguments);
            WriteEvent("Starting " + _descriptor.Executable + ' ' + startarguments);

            StartProcess(_process, startarguments, _descriptor.Executable);
            ExtensionManager.FireOnProcessStarted(_process);

            // send stdout and stderr to its respective output file.
            HandleLogfiles();

            _process.StandardInput.Close(); // nothing for you to read!
        }

        protected override void OnShutdown()
        {
//            WriteEvent("OnShutdown");

            try
            {
                _systemShuttingdown = true;
                StopIt();
            }
            catch (Exception ex)
            {
                WriteEvent("Shutdown exception", ex);
            }
        }

        protected override void OnStop()
        {
//            WriteEvent("OnStop");

            try
            {
                StopIt();
            }
            catch (Exception ex)
            {
                WriteEvent("Stop exception", ex);
            }
        }

        /// <summary>
        /// Called when we are told by Windows SCM to exit.
        /// </summary>
        private void StopIt()
        {
            string stoparguments = _descriptor.Stoparguments;
            LogEvent("Stopping " + _descriptor.Id);
            WriteEvent("Stopping " + _descriptor.Id);
            _orderlyShutdown = true;

            if (stoparguments == null)
            {
                try
                {
                    WriteEvent("ProcessKill " + _process.Id);
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
                String executable = _descriptor.StopExecutable;

                if (executable == null)
                {
                    executable = _descriptor.Executable;
                }

                StartProcess(stopProcess, stoparguments, executable);

                WriteEvent("WaitForProcessToExit "+_process.Id+"+"+stopProcess.Id);
                WaitForProcessToExit(_process);
                WaitForProcessToExit(stopProcess);
                SignalShutdownComplete();
            }

            // Stop extensions      
            try
            {
                ExtensionManager.OnStop(this);
            }
            catch (ExtensionException ex)
            {
                LogEvent("Failed to stop extension  " + ex.ExtensionId + "\n" + ex.Message, EventLogEntryType.Error);
                WriteEvent("Failed to stop extension  " + ex.ExtensionId, ex);
            }

            if (_systemShuttingdown && _descriptor.BeepOnShutdown) 
            {
                Console.Beep();
            }

            WriteEvent("Finished " + _descriptor.Id);
        }

        private void WaitForProcessToExit(Process processoWait)
        {
            SignalShutdownPending();
            
            int effectiveProcessWaitSleepTime;
            if (_descriptor.SleepTime.TotalMilliseconds > Int32.MaxValue)
            {
                Log.Warn("The requested sleep time " + _descriptor.SleepTime.TotalMilliseconds + "is greater that the max value " + 
                    Int32.MaxValue + ". The value will be truncated");
                effectiveProcessWaitSleepTime = Int32.MaxValue;
            }
            else
            {
                effectiveProcessWaitSleepTime = (int)_descriptor.SleepTime.TotalMilliseconds;
            }

            try
            {
//                WriteEvent("WaitForProcessToExit [start]");

                while (!processoWait.WaitForExit(effectiveProcessWaitSleepTime))
                {
                    SignalShutdownPending();
//                    WriteEvent("WaitForProcessToExit [repeat]");
                }
            }
            catch (InvalidOperationException)
            {
                // already terminated
            }

//            WriteEvent("WaitForProcessToExit [finished]");
        }

        private void SignalShutdownPending()
        {
            int effectiveWaitHint;
            if (_descriptor.WaitHint.TotalMilliseconds > Int32.MaxValue)
            {
                Log.Warn("The requested WaitHint value (" + _descriptor.WaitHint.TotalMilliseconds + " ms)  is greater that the max value " + 
                    Int32.MaxValue + ". The value will be truncated");
                effectiveWaitHint = Int32.MaxValue;
            }
            else
            {
                effectiveWaitHint = (int)_descriptor.WaitHint.TotalMilliseconds;
            }


            IntPtr handle = ServiceHandle;
            _wrapperServiceStatus.checkPoint++;
            _wrapperServiceStatus.waitHint = effectiveWaitHint;
//            WriteEvent("SignalShutdownPending " + wrapperServiceStatus.checkPoint + ":" + wrapperServiceStatus.waitHint);
            _wrapperServiceStatus.currentState = (int)State.SERVICE_STOP_PENDING;
            Advapi32.SetServiceStatus(handle, ref _wrapperServiceStatus);
        }

        private void SignalShutdownComplete()
        {
            IntPtr handle = ServiceHandle;
            _wrapperServiceStatus.checkPoint++;
//            WriteEvent("SignalShutdownComplete " + wrapperServiceStatus.checkPoint + ":" + wrapperServiceStatus.waitHint);
            _wrapperServiceStatus.currentState = (int)State.SERVICE_STOPPED;
            Advapi32.SetServiceStatus(handle, ref _wrapperServiceStatus);
        }

        private void StartProcess(Process processToStart, string arguments, String executable)
        {
            var ps = processToStart.StartInfo;
            ps.FileName = executable;
            ps.Arguments = arguments;
            ps.WorkingDirectory = _descriptor.WorkingDirectory;
            ps.CreateNoWindow = false;
            ps.UseShellExecute = false;
            ps.RedirectStandardInput = true; // this creates a pipe for stdin to the new process, instead of having it inherit our stdin.
            ps.RedirectStandardOutput = true;
            ps.RedirectStandardError = true;

            foreach (string key in _envs.Keys)
            {
                Environment.SetEnvironmentVariable(key, _envs[key]);
                // ps.EnvironmentVariables[key] = envs[key]; // bugged (lower cases all variable names due to StringDictionary being used, see http://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=326163)
            }

            // TODO: Make it generic via extension points. The issue mentioned above should be ideally worked around somehow
            ps.EnvironmentVariables[WinSWSystem.ENVVAR_NAME_SERVICE_ID.ToLower()] = _descriptor.Id;

            processToStart.Start();
            WriteEvent("Started " + processToStart.Id);

            var priority = _descriptor.Priority;
            if (priority != ProcessPriorityClass.Normal)
                processToStart.PriorityClass = priority;

            // monitor the completion of the process
            StartThread(delegate
            {
                string msg = processToStart.Id + " - " + processToStart.StartInfo.FileName + " " + processToStart.StartInfo.Arguments;
                processToStart.WaitForExit();

                try
                {
                    if (_orderlyShutdown)
                    {
                        LogEvent("Child process [" + msg + "] terminated with " + processToStart.ExitCode, EventLogEntryType.Information);
                    }
                    else
                    {
                        LogEvent("Child process [" + msg + "] finished with " + processToStart.ExitCode, EventLogEntryType.Warning);
                        // if we finished orderly, report that to SCM.
                        // by not reporting unclean shutdown, we let Windows SCM to decide if it wants to
                        // restart the service automatically
                        if (processToStart.ExitCode == 0)
                            SignalShutdownComplete();
                        Environment.Exit(processToStart.ExitCode);
                    }
                }
                catch (InvalidOperationException ioe)
                {
                    LogEvent("WaitForExit " + ioe.Message);
                }

                try
                {
                    processToStart.Dispose();
                }
                catch (InvalidOperationException ioe)
                {
                    LogEvent("Dispose " + ioe.Message);
                }
            });
        }

        public static int Main(string[] args)
        {
            // Run app
            try
            {
                Run(args);
                Log.Info("Completed. Exit code is 0");
                return 0;
            }
            catch (WmiException e)
            {
                Console.Error.WriteLine(e);
                return (int)e.ErrorCode;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return -1;
            }
        }

        private static void ThrowNoSuchService()
        {
            throw new WmiException(ReturnValue.NoSuchService);
        }

        // ReSharper disable once InconsistentNaming
        public static void Run(string[] _args, ServiceDescriptor descriptor = null)
        {
            bool isCLIMode = _args.Length > 0;
            var d = descriptor ?? new ServiceDescriptor();

            // Configure the wrapper-internal logging
            // STDIN and STDOUT of the child process will be handled independently
            InitLoggers(d, isCLIMode);

            if (isCLIMode) // CLI mode
            {               
                Log.Info("Starting ServiceWrapper in the CLI mode");

                // Get service info for the future use
                Win32Services svc = new WmiRoot().GetCollection<Win32Services>();
                Win32Service s = svc.Select(d.Id);

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
                    var w = new StreamWriter(f) {AutoFlush = true};
                    Console.SetOut(w);
                    Console.SetError(w);

                    var handle = f.Handle;
                    Kernel32.SetStdHandle(-11, handle); // set stdout
                    Kernel32.SetStdHandle(-12, handle); // set stder

                    args = args.GetRange(2, args.Count - 2);
                }

                args[0] = args[0].ToLower();
                if (args[0] == "install")
                {
                    // Check if the service exists
                    if (s != null)
                    {
                        Console.WriteLine("Service with id '" + d.Id + "' already exists");
                        Console.WriteLine("To install the service, delete the existing one or change service Id in the configuration file");
                        throw new Exception("Installation failure: Service with id '" + d.Id + "' already exists");
                    }

                    string username=null, password=null;
                    bool setallowlogonasaserviceright = false;
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
                        if (d.HasServiceAccount())
                        {
                            username = d.ServiceAccountUser;
                            password = d.ServiceAccountPassword;
                            setallowlogonasaserviceright = d.AllowServiceAcountLogonRight;
                        }
                    }
                    
                    if (setallowlogonasaserviceright)
                    {
                        LogonAsAService.AddLogonAsAServiceRight(username);
                    }

                    svc.Create (
                        d.Id,
                        d.Caption,
                        "\"" + d.ExecutablePath + "\"",
                        ServiceType.OwnProcess,
                        ErrorControl.UserNotified,
                        d.StartMode,
                        d.Interactive,
                        username,
                        password,
                        d.ServiceDependencies);

                    // update the description
                    /* Somehow this doesn't work, even though it doesn't report an error
                    Win32Service s = svc.Select(d.Id);
                    s.Description = d.Description;
                    s.Commit();
                     */

                    // so using a classic method to set the description. Ugly.
                    Registry.LocalMachine.OpenSubKey("System").OpenSubKey("CurrentControlSet").OpenSubKey("Services")
                        .OpenSubKey(d.Id, true).SetValue("Description", d.Description);

                    var actions = d.FailureActions;
                    if (actions.Count > 0)
                    {// set the failure actions
                        using (ServiceManager scm = new ServiceManager())
                        {
                            using (Service sc = scm.Open(d.Id))
                            {
                                sc.ChangeConfig(d.ResetFailureAfter, actions);
                            }
                        }
                    }
                    return;
                }
                if (args[0] == "uninstall")
                {
                    if (s == null)
                    {
                        Console.WriteLine("Warning! The service with id '" + d.Id + "' does not exist. Nothing to uninstall");
                        return; // there's no such service, so consider it already uninstalled
                    }
                    try
                    {
                        s.Delete();
                    }
                    catch (WmiException e)
                    {
                        if (e.ErrorCode == ReturnValue.ServiceMarkedForDeletion)
                            return; // it's already uninstalled, so consider it a success
                        throw e;
                    }
                    return;
                }
                if (args[0] == "start")
                {
                    if (s == null) ThrowNoSuchService();
                    s.StartService();
                    return;
                }
                if (args[0] == "stop")
                {
                    if (s == null) ThrowNoSuchService();
                    s.StopService();
                    return;
                }
                if (args[0] == "restart")
                {
                    if (s == null) 
                        ThrowNoSuchService();

                    if(s.Started)
                        s.StopService();

                    while (s.Started)
                    {
                        Thread.Sleep(1000);
                        s = svc.Select(d.Id);
                    }

                    s.StartService();
                    return;
                }
                if (args[0] == "restart!")
                {
                    // run restart from another process group. see README.md for why this is useful.

                    STARTUPINFO si = new STARTUPINFO();
                    PROCESS_INFORMATION pi = new PROCESS_INFORMATION();

                    bool result = Kernel32.CreateProcess(null, d.ExecutablePath+" restart", IntPtr.Zero, IntPtr.Zero, false, 0x200/*CREATE_NEW_PROCESS_GROUP*/, IntPtr.Zero, null, ref si, out pi);
                    if (!result)
                    {
                        throw new Exception("Failed to invoke restart: "+Marshal.GetLastWin32Error());
                    }
                    return;
                }
                if (args[0] == "status")
                {
                    Log.Warn("User requested the status");
                    if (s == null)
                        Console.WriteLine("NonExistent");
                    else if (s.Started)
                        Console.WriteLine("Started");
                    else
                        Console.WriteLine("Stopped");
                    return;
                }
                if (args[0] == "test")
                {
                    WrapperService wsvc = new WrapperService();
                    wsvc.OnStart(args.ToArray());
                    Thread.Sleep(1000);
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
            else
            {
                Log.Info("Starting ServiceWrapper in the service mode");
            }
            Run(new WrapperService());
        }

        private static void InitLoggers(ServiceDescriptor d, bool enableCLILogging)
        {
            // TODO: Make logging levels configurable
            Level logLevel = Level.Debug;
            Level eventLogLevel = Level.Warn;

            // Legacy format from winsw-1.x: (DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - " + message);
            PatternLayout pl = new PatternLayout { ConversionPattern = "%d %-5p - %m%n" };
            pl.ActivateOptions();

            List<IAppender> appenders = new List<IAppender>();

            // wrapper.log
            String wrapperLogPath = Path.Combine(d.LogDirectory, d.BaseName + ".wrapper.log");
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
                    Threshold = logLevel,
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
                provider  = eventLogProvider
            };
            systemEventLogger.ActivateOptions();
            appenders.Add(systemEventLogger);

            BasicConfigurator.Configure(appenders.ToArray());
        }

        private static string ReadPassword()
        {
            StringBuilder buf = new StringBuilder();
            ConsoleKeyInfo key;
            while (true)
            {
                key = Console.ReadKey(true);
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
            Console.WriteLine("");
            Console.WriteLine("Usage: winsw [/redirect file] <command> [<args>]");
            Console.WriteLine("       Missing arguments trigger the service mode");
            Console.WriteLine("");
            printAvailableCommandsInfo();
            Console.WriteLine("");
            Console.WriteLine("Extra options:");
            Console.WriteLine("- '/redirect' - redirect the wrapper's STDOUT and STDERR to the specified file");
            Console.WriteLine("");
            printVersion();
            Console.WriteLine("More info: https://github.com/kohsuke/winsw");
            Console.WriteLine("Bug tracker: https://github.com/kohsuke/winsw/issues");
        }

        //TODO: Rework to enum in winsw-2.0
        private static void printAvailableCommandsInfo()
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine("- 'install'   - install the service to Windows Service Controller");
            Console.WriteLine("- 'uninstall' - uninstall the service");
            Console.WriteLine("- 'start'     - start the service (must be installed before)");
            Console.WriteLine("- 'stop'      - stop the service");
            Console.WriteLine("- 'restart'   - restart the service");
            Console.WriteLine("- 'restart!'  - self-restart (can be called from child processes)");
            Console.WriteLine("- 'status'    - check the current status of the service");
            Console.WriteLine("- 'test'      - check if the service can be started and then stopped");  
            Console.WriteLine("- 'version'   - print the version info");
            Console.WriteLine("- 'help'      - print the help info (aliases: -h,--help,-?,/?)");
        }

        private static void printVersion()
        {
            Console.WriteLine("WinSW " + Version);
        }
    }
}

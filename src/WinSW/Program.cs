using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
#if VNEXT
using System.IO.Pipes;
#endif
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Layout;
using WinSW.Configuration;
using WinSW.Logging;
using WinSW.Native;
using static WinSW.FormatExtensions;
using static WinSW.ServiceControllerExtension;
using static WinSW.Native.ServiceApis;
using TimeoutException = System.ServiceProcess.TimeoutException;

namespace WinSW
{
    public static class Program
    {
#if VNEXT
        private const string NoPipe = "-";
#endif

        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        private static string ExecutablePath
        {
            get
            {
                using var current = Process.GetCurrentProcess();
                return current.MainModule!.FileName!;
            }
        }

        public static int Main(string[] args)
        {
            try
            {
                Run(args);
                return 0;
            }
            catch (InvalidDataException e)
            {
                string message = "The configuration file cound not be loaded. " + e.Message;
                Log.Fatal(message, e);
                Console.Error.WriteLine(message);
                return -1;
            }
            catch (CommandException e)
            {
                string message = e.Message;
                Log.Fatal(message);
                return e.InnerException is Win32Exception inner ? inner.NativeErrorCode : -1;
            }
            catch (InvalidOperationException e) when (e.InnerException is Win32Exception inner)
            {
                string message = e.Message;
                Log.Fatal(message);
                return inner.NativeErrorCode;
            }
            catch (Win32Exception e)
            {
                string message = e.Message;
                Log.Fatal(message, e);
                return e.NativeErrorCode;
            }
            catch (Exception e)
            {
                Log.Fatal("Unhandled exception", e);
                return -1;
            }
        }

        public static void Run(string[] argsArray, IServiceConfig? config = null)
        {
            bool inConsoleMode = argsArray.Length > 0;

            config ??= LoadConfigAndInitLoggers(inConsoleMode);

            if (!inConsoleMode)
            {
                Log.Debug("Starting WinSW in service mode");
                using var service = new WrapperService(config);
                try
                {
                    ServiceBase.Run(service);
                }
                catch
                {
                    // handled in OnStart
                }

                return;
            }

            Log.Debug("Starting WinSW in console mode");

            if (argsArray.Length == 0)
            {
                PrintHelp();
                return;
            }

            var args = new List<string>(Array.AsReadOnly(argsArray));
            if (args[0] == "/redirect")
            {
                var f = new FileStream(args[1], FileMode.Create);
                var w = new StreamWriter(f) { AutoFlush = true };
                Console.SetOut(w);
                Console.SetError(w);

                var handle = f.SafeFileHandle;
                _ = Kernel32.SetStdHandle(-11, handle); // set stdout
                _ = Kernel32.SetStdHandle(-12, handle); // set stder

                args = args.GetRange(2, args.Count - 2);
            }

            bool elevated;
            if (args[0] == "/elevated")
            {
                elevated = true;

                _ = ConsoleApis.FreeConsole();
                _ = ConsoleApis.AttachConsole(ConsoleApis.ATTACH_PARENT_PROCESS);

#if VNEXT
                string stdinName = args[1];
                if (stdinName != NoPipe)
                {
                    var stdin = new NamedPipeClientStream(".", stdinName, PipeDirection.In, PipeOptions.Asynchronous);
                    stdin.Connect();
                    Console.SetIn(new StreamReader(stdin));
                }

                string stdoutName = args[2];
                if (stdoutName != NoPipe)
                {
                    var stdout = new NamedPipeClientStream(".", stdoutName, PipeDirection.Out, PipeOptions.Asynchronous);
                    stdout.Connect();
                    Console.SetOut(new StreamWriter(stdout) { AutoFlush = true });
                }

                string stderrName = args[3];
                if (stderrName != NoPipe)
                {
                    var stderr = new NamedPipeClientStream(".", stderrName, PipeDirection.Out, PipeOptions.Asynchronous);
                    stderr.Connect();
                    Console.SetError(new StreamWriter(stderr) { AutoFlush = true });
                }

                args = args.GetRange(4, args.Count - 4);
#else
                args = args.GetRange(1, args.Count - 1);
#endif
            }
            else if (Environment.OSVersion.Version.Major == 5)
            {
                // Windows XP
                elevated = true;
            }
            else
            {
                elevated = IsProcessElevated();
            }

            switch (args[0].ToLower())
            {
                case "install":
                    Install();
                    return;

                case "uninstall":
                    Uninstall();
                    return;

                case "start":
                    Start();
                    return;

                case "stop":
                    Stop(true);
                    return;

                case "stopwait":
                    Stop(false);
                    return;

                case "restart":
                    Restart();
                    return;

                case "restart!":
                    RestartSelf();
                    return;

                case "status":
                    Status();
                    return;

                case "test":
                    Test();
                    return;

                case "testwait":
                    TestWait();
                    return;

                case "help":
                case "--help":
                case "-h":
                case "-?":
                case "/?":
                    PrintHelp();
                    return;

                case "version":
                    PrintVersion();
                    return;

                default:
                    Console.WriteLine("Unknown command: " + args[0]);
                    PrintAvailableCommands();
                    throw new Exception("Unknown command: " + args[0]);
            }

            void Install()
            {
                if (!elevated)
                {
                    Elevate();
                    return;
                }

                Log.Info($"Installing service '{Format(config)}'...");

                using var scm = ServiceManager.Open(ServiceManagerAccess.CreateService);

                if (scm.ServiceExists(config.Name))
                {

                    Log.Error($"A service with ID '{config.Name}' already exists.");
                    Throw.Command.Win32Exception(Errors.ERROR_SERVICE_EXISTS, "Failed to install the service.");
                }

                string? username = null;
                string? password = null;
                bool allowServiceLogonRight = false;
                if (args.Count > 1 && args[1] == "/p")
                {
                    Credentials.PromptForCredentialsConsole(ref username, ref password);
                    Console.Write("Set Account rights to allow log on as a service (y/n)?: ");
                    var keypressed = Console.ReadKey();
                    Console.WriteLine();
                    if (keypressed.Key == ConsoleKey.Y)
                    {
                        allowServiceLogonRight = true;
                    }
                }
                else
                {
                    if (config.ServiceAccount.HasServiceAccount())
                    {
                        username = config.ServiceAccount.FullUser;
                        password = config.ServiceAccount.Password;
                        allowServiceLogonRight = config.ServiceAccount.AllowServiceLogonRight;
                    }
                }

                if (allowServiceLogonRight)
                {
                    Security.AddServiceLogonRight(config.ServiceAccount.Domain!, config.ServiceAccount.User!);
                }

                using var sc = scm.CreateService(
                    config.Name,
                    config.DisplayName,
                    config.Interactive,
                    config.StartMode,
                    $"\"{config.ExecutablePath}\"",
                    config.ServiceDependencies,
                    username,
                    password);

                string description = config.Description;
                if (description.Length != 0)
                {
                    sc.SetDescription(description);
                }

                var actions = config.FailureActions;
                if (actions.Length > 0)
                {
                    sc.SetFailureActions(config.ResetFailureAfter, actions);
                }

                bool isDelayedAutoStart = config.StartMode == ServiceStartMode.Automatic && config.DelayedAutoStart;
                if (isDelayedAutoStart)
                {
                    sc.SetDelayedAutoStart(true);
                }

                string? securityDescriptor = config.SecurityDescriptor;
                if (securityDescriptor != null)
                {
                    // throws ArgumentException
                    sc.SetSecurityDescriptor(new RawSecurityDescriptor(securityDescriptor));
                }

                string eventLogSource = config.Name;
                if (!EventLog.SourceExists(eventLogSource))
                {
                    EventLog.CreateEventSource(eventLogSource, "Application");
                }

                Log.Info($"Service '{Format(config)}' was installed successfully.");
            }

            void Uninstall()
            {
                if (!elevated)
                {
                    Elevate();
                    return;
                }

                Log.Info($"Uninstalling service '{Format(config)}'...");

                using var scm = ServiceManager.Open(ServiceManagerAccess.Connect);
                try
                {
                    using var sc = scm.OpenService(config.Name);

                    if (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        // We could fail the opeartion here, but it would be an incompatible change.
                        // So it is just a warning
                        Log.Warn($"Service '{Format(config)}' is started. It may be impossible to uninstall it.");
                    }

                    sc.Delete();

                    Log.Info($"Service '{Format(config)}' was uninstalled successfully.");
                }
                catch (CommandException e) when (e.InnerException is Win32Exception inner)
                {
                    switch (inner.NativeErrorCode)
                    {
                        case Errors.ERROR_SERVICE_DOES_NOT_EXIST:
                            Log.Warn($"Service '{Format(config)}' does not exist.");
                            break; // there's no such service, so consider it already uninstalled

                        case Errors.ERROR_SERVICE_MARKED_FOR_DELETE:
                            Log.Error(e.Message);

                            // TODO: change the default behavior to Error?
                            break; // it's already uninstalled, so consider it a success

                        default:
                            Throw.Command.Exception("Failed to uninstall the service.", inner);
                            break;
                    }
                }
            }

            void Start()
            {
                if (!elevated)
                {
                    Elevate();
                    return;
                }

                using var svc = new ServiceController(config.Name);

                try
                {
                    Log.Info($"Starting service '{Format(svc)}'...");
                    svc.Start();

                    Log.Info($"Service '{Format(svc)}' started successfully.");
                }
                catch (InvalidOperationException e)
                when (e.InnerException is Win32Exception inner && inner.NativeErrorCode == Errors.ERROR_SERVICE_DOES_NOT_EXIST)
                {
                    Throw.Command.Exception(inner);
                }
                catch (InvalidOperationException e)
                when (e.InnerException is Win32Exception inner && inner.NativeErrorCode == Errors.ERROR_SERVICE_ALREADY_RUNNING)
                {
                    Log.Info($"Service '{Format(svc)}' has already started.");
                }
            }

            void Stop(bool noWait)
            {
                if (!elevated)
                {
                    Elevate();
                    return;
                }

                using var svc = new ServiceController(config.Name);

                try
                {
                    Log.Info($"Stopping service '{Format(svc)}'...");
                    svc.Stop();

                    if (!noWait)
                    {
                        try
                        {
                            WaitForStatus(svc, ServiceControllerStatus.Stopped, ServiceControllerStatus.StopPending);
                        }
                        catch (TimeoutException)
                        {
                            Throw.Command.Exception("Failed to stop the service.");
                        }
                    }

                    Log.Info($"Service '{Format(svc)}' stopped successfully.");
                }
                catch (InvalidOperationException e)
                when (e.InnerException is Win32Exception inner && inner.NativeErrorCode == Errors.ERROR_SERVICE_DOES_NOT_EXIST)
                {
                    Throw.Command.Exception(inner);
                }
                catch (InvalidOperationException e)
                when (e.InnerException is Win32Exception inner && inner.NativeErrorCode == Errors.ERROR_SERVICE_NOT_ACTIVE)
                {
                    Log.Info($"Service '{Format(svc)}' has already stopped.");
                }
            }

            void Restart()
            {
                if (!elevated)
                {
                    Elevate();
                    return;
                }


                using var svc = new ServiceController(config.Name);

                List<ServiceController>? startedDependentServices = null;

                try
                {
                    if (HasAnyStartedDependentService(svc))
                    {
                        startedDependentServices = new();
                        foreach (var service in svc.DependentServices)
                        {
                            if (service.Status != ServiceControllerStatus.Stopped)
                            {
                                startedDependentServices.Add(service);
                            }
                        }
                    }

                    Log.Info($"Stopping service '{Format(svc)}'...");
                    svc.Stop();

                    try
                    {
                        WaitForStatus(svc, ServiceControllerStatus.Stopped, ServiceControllerStatus.StopPending);
                    }
                    catch (TimeoutException)
                    {
                        Throw.Command.Exception("Failed to stop the service.");
                    }
                }
                catch (InvalidOperationException e)
                when (e.InnerException is Win32Exception inner && inner.NativeErrorCode == Errors.ERROR_SERVICE_DOES_NOT_EXIST)
                {
                    Throw.Command.Exception(inner);
                }
                catch (InvalidOperationException e)
                when (e.InnerException is Win32Exception inner && inner.NativeErrorCode == Errors.ERROR_SERVICE_NOT_ACTIVE)
                {
                }

                Log.Info($"Starting service '{Format(svc)}'...");
                svc.Start();

                try
                {
                    WaitForStatus(svc, ServiceControllerStatus.Running, ServiceControllerStatus.StartPending);
                }
                catch (TimeoutException)
                {
                    Throw.Command.Exception("Failed to start the service.");
                }

                if (startedDependentServices != null)
                {
                    foreach (var service in startedDependentServices)
                    {
                        if (service.Status == ServiceControllerStatus.Stopped)
                        {
                            Log.Info($"Starting service '{Format(service)}'...");
                            service.Start();
                        }
                    }
                }

                Log.Info($"Service '{Format(svc)}' restarted successfully.");
            }

            void RestartSelf()
            {
                if (!elevated)
                {
                    Throw.Command.Win32Exception(Errors.ERROR_ACCESS_DENIED);
                }

                Log.Info("Restarting the service with id '" + config.Name + "'");

                // run restart from another process group. see README.md for why this is useful.
                if (!ProcessApis.CreateProcess(
                    null,
                    config.ExecutablePath + " restart",
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    ProcessApis.CREATE_NEW_PROCESS_GROUP,
                    IntPtr.Zero,
                    null,
                    default,
                    out var processInfo))
                {
                    Throw.Command.Win32Exception("Failed to invoke restart.");
                }

                _ = HandleApis.CloseHandle(processInfo.ProcessHandle);
                _ = HandleApis.CloseHandle(processInfo.ThreadHandle);
            }

            void Status()
            {
                using var svc = new ServiceController(config.Name);
                try
                {
                    Console.WriteLine(svc.Status != ServiceControllerStatus.Stopped ? "Started" : "Stopped");
                }
                catch (InvalidOperationException e)
                when (e.InnerException is Win32Exception inner && inner.NativeErrorCode == Errors.ERROR_SERVICE_DOES_NOT_EXIST)
                {
                    Console.WriteLine("NonExistent");
                }
            }

            void Test()
            {
                if (!elevated)
                {
                    Elevate();
                    return;
                }

                var wsvc = new WrapperService(config);
                wsvc.RaiseOnStart(args.ToArray());
                Thread.Sleep(1000);
                wsvc.RaiseOnStop();
            }

            void TestWait()
            {
                if (!elevated)
                {
                    Elevate();
                    return;
                }

                var wsvc = new WrapperService(config);
                wsvc.RaiseOnStart(args.ToArray());
                Console.WriteLine("Press any key to stop the service...");
                _ = Console.Read();
                wsvc.RaiseOnStop();
            }

            // [DoesNotReturn]
            void Elevate()
            {
#if VNEXT
                string? stdinName = Console.IsInputRedirected ? Guid.NewGuid().ToString() : null;
                string? stdoutName = Console.IsOutputRedirected ? Guid.NewGuid().ToString() : null;
                string? stderrName = Console.IsErrorRedirected ? Guid.NewGuid().ToString() : null;
#endif

                string exe = Environment.GetCommandLineArgs()[0];
                string commandLine = Environment.CommandLine;
                string arguments = "/elevated" +
#if VNEXT
                    " " + (stdinName ?? NoPipe) +
                    " " + (stdoutName ?? NoPipe) +
                    " " + (stderrName ?? NoPipe) +
#endif
                    commandLine.Remove(commandLine.IndexOf(exe), exe.Length).TrimStart('"');

                var startInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    FileName = ExecutablePath,
                    Arguments = arguments,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };

                try
                {
                    using var elevated = Process.Start(startInfo)!;

#if VNEXT
                    if (stdinName != null)
                    {
                        var stdin = new NamedPipeServerStream(stdinName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                        stdin.WaitForConnectionAsync().ContinueWith(_ => Console.OpenStandardInput().CopyToAsync(stdin));
                    }

                    if (stdoutName != null)
                    {
                        var stdout = new NamedPipeServerStream(stdoutName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                        stdout.WaitForConnectionAsync().ContinueWith(_ => stdout.CopyToAsync(Console.OpenStandardOutput()));
                    }

                    if (stderrName != null)
                    {
                        var stderr = new NamedPipeServerStream(stderrName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                        stderr.WaitForConnectionAsync().ContinueWith(_ => stderr.CopyToAsync(Console.OpenStandardError()));
                    }
#endif

                    elevated.WaitForExit();
                    Environment.Exit(elevated.ExitCode);
                }
                catch (Win32Exception e) when (e.NativeErrorCode == Errors.ERROR_CANCELLED)
                {
                    Log.Fatal(e.Message);
                    Environment.Exit(e.ErrorCode);
                }
            }
        }

        private static IServiceConfig LoadConfigAndInitLoggers(bool inConsoleMode)
        {
            // TODO: Make logging levels configurable
            var fileLogLevel = Level.Debug;

            // TODO: Debug should not be printed to console by default. Otherwise commands like 'status' will be pollutted
            // This is a workaround till there is a better command line parsing, which will allow determining
            var consoleLogLevel = Level.Info;
            var eventLogLevel = Level.Warn;

            var layout = new PatternLayout { ConversionPattern = "%d %-5p - %m%n" };
            layout.ActivateOptions();

            var repository = LogManager.GetRepository(Assembly.GetExecutingAssembly());

            if (inConsoleMode)
            {
                var consoleAppender = new ConsoleAppender
                {
                    Name = "Wrapper console log",
                    Threshold = consoleLogLevel,
                    Layout = layout,
                };
                consoleAppender.ActivateOptions();

                BasicConfigurator.Configure(repository, consoleAppender);
            }
            else
            {
                var eventLogAppender = new ServiceEventLogAppender(WrapperService.eventLogProvider)
                {
                    Name = "Wrapper event log",
                    Threshold = eventLogLevel,
                };
                eventLogAppender.ActivateOptions();

                BasicConfigurator.Configure(repository, eventLogAppender);
            }

            string executablePath = ExecutablePath;
            string directory = Path.GetDirectoryName(executablePath)!;
            string baseName = Path.GetFileNameWithoutExtension(executablePath);

            IServiceConfig config =
                File.Exists(Path.Combine(directory, baseName + ".xml")) ? new XmlServiceConfig(baseName, directory) :
                File.Exists(Path.Combine(directory, baseName + ".yml")) ? new YamlServiceConfig(baseName, directory) :
                throw new FileNotFoundException($"Unable to locate {baseName}.[xml|yml] file within executable directory");

            // .wrapper.log
            string wrapperLogPath = Path.Combine(config.LogDirectory, config.BaseName + ".wrapper.log");
            var fileAppender = new FileAppender
            {
                AppendToFile = true,
                File = wrapperLogPath,
                ImmediateFlush = true,
                Name = "Wrapper file log",
                Threshold = fileLogLevel,
                LockingModel = new FileAppender.MinimalLock(),
                Layout = layout,
            };
            fileAppender.ActivateOptions();

            BasicConfigurator.Configure(repository, fileAppender);

            return config;
        }

        internal static unsafe bool IsProcessElevated()
        {
            var process = ProcessApis.GetCurrentProcess();
            if (!ProcessApis.OpenProcessToken(process, TokenAccessLevels.Read, out var token))
            {
                Throw.Command.Win32Exception("Failed to open process token.");
            }

            try
            {
                if (!SecurityApis.GetTokenInformation(
                    token,
                    SecurityApis.TOKEN_INFORMATION_CLASS.TokenElevation,
                    out var elevation,
                    sizeof(SecurityApis.TOKEN_ELEVATION),
                    out _))
                {
                    Throw.Command.Win32Exception("Failed to get token information");
                }

                return elevation.TokenIsElevated != 0;
            }
            finally
            {
                _ = HandleApis.CloseHandle(token);
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("A wrapper binary that can be used to host executables as Windows services");
            Console.WriteLine();
            Console.WriteLine("Usage: winsw <command> [<args>]");
            Console.WriteLine("       Missing arguments triggers the service mode");
            Console.WriteLine();
            PrintAvailableCommands();
            Console.WriteLine();
            Console.WriteLine("Extra options:");
            Console.WriteLine("  /redirect   redirect the wrapper's STDOUT and STDERR to the specified file");
            Console.WriteLine();
            PrintVersion();
            Console.WriteLine("More info: https://github.com/winsw/winsw");
            Console.WriteLine("Bug tracker: https://github.com/winsw/winsw/issues");
        }

        // TODO: Rework to enum in winsw-2.0
        private static void PrintAvailableCommands()
        {
            Console.WriteLine(
@"Available commands:
  install     install the service to Windows Service Controller
  uninstall   uninstall the service
  start       start the service (must be installed before)
  stop        stop the service
  stopwait    stop the service and wait until it's actually stopped
  restart     restart the service
  restart!    self-restart (can be called from child processes)
  status      check the current status of the service
  test        check if the service can be started and then stopped
  testwait    starts the service and waits until a key is pressed then stops the service
  version     print the version info
  help        print the help info (aliases: -h,--help,-?,/?)");
        }

        private static void PrintVersion()
        {
            Console.WriteLine("WinSW " + WrapperService.Version);
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
#if NET
using System.Reflection;
#endif
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
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
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

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

        public static void Run(string[] argsArray, IWinSWConfiguration? descriptor = null)
        {
            bool inConsoleMode = argsArray.Length > 0;

            // If descriptor is not specified, initialize the new one (and load configs from there)
            descriptor ??= GetServiceDescriptor();

            // Configure the wrapper-internal logging.
            // STDOUT and STDERR of the child process will be handled independently.
            InitLoggers(descriptor, inConsoleMode);

            if (!inConsoleMode)
            {
                Log.Debug("Starting WinSW in service mode");
                using var service = new WrapperService(descriptor);
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

                args = args.GetRange(1, args.Count - 1);
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

                Log.Info($"Installing service '{Format(descriptor)}'...");

                using var scm = ServiceManager.Open(ServiceManagerAccess.CreateService);

                if (scm.ServiceExists(descriptor.Name))
                {

                    Log.Error($"A service with ID '{descriptor.Name}' already exists.");
                    Throw.Command.Win32Exception(Errors.ERROR_SERVICE_EXISTS, "Failed to install the service.");
                }

                string? username = null;
                string? password = null;
                bool allowServiceLogonRight = false;
                if (args.Count > 1 && args[1] == "/p")
                {
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
                        allowServiceLogonRight = true;
                    }
                }
                else
                {
                    if (descriptor.ServiceAccount.HasServiceAccount())
                    {
                        username = descriptor.ServiceAccount.ServiceAccountUser;
                        password = descriptor.ServiceAccount.ServiceAccountPassword;
                        allowServiceLogonRight = descriptor.ServiceAccount.AllowServiceAcountLogonRight;
                    }
                }

                if (allowServiceLogonRight)
                {
                    Security.AddServiceLogonRight(descriptor.ServiceAccount.ServiceAccountDomain!, descriptor.ServiceAccount.ServiceAccountName!);
                }

                using var sc = scm.CreateService(
                    descriptor.Name,
                    descriptor.DisplayName,
                    descriptor.Interactive,
                    descriptor.StartMode,
                    $"\"{descriptor.ExecutablePath}\"",
                    descriptor.ServiceDependencies,
                    username,
                    password);

                string description = descriptor.Description;
                if (description.Length != 0)
                {
                    sc.SetDescription(description);
                }

                var actions = descriptor.FailureActions;
                if (actions.Length > 0)
                {
                    sc.SetFailureActions(descriptor.ResetFailureAfter, actions);
                }

                bool isDelayedAutoStart = descriptor.StartMode == ServiceStartMode.Automatic && descriptor.DelayedAutoStart;
                if (isDelayedAutoStart)
                {
                    sc.SetDelayedAutoStart(true);
                }

                string? securityDescriptor = descriptor.SecurityDescriptor;
                if (securityDescriptor != null)
                {
                    // throws ArgumentException
                    sc.SetSecurityDescriptor(new RawSecurityDescriptor(securityDescriptor));
                }

                string eventLogSource = descriptor.Name;
                if (!EventLog.SourceExists(eventLogSource))
                {
                    EventLog.CreateEventSource(eventLogSource, "Application");
                }

                Log.Info($"Service '{Format(descriptor)}' was installed successfully.");
            }

            void Uninstall()
            {
                if (!elevated)
                {
                    Elevate();
                    return;
                }

                Log.Info($"Uninstalling service '{Format(descriptor)}'...");

                using var scm = ServiceManager.Open(ServiceManagerAccess.Connect);
                try
                {
                    using var sc = scm.OpenService(descriptor.Name);

                    if (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        // We could fail the opeartion here, but it would be an incompatible change.
                        // So it is just a warning
                        Log.Warn($"Service '{Format(descriptor)}' is started. It may be impossible to uninstall it.");
                    }

                    sc.Delete();

                    Log.Info($"Service '{Format(descriptor)}' was uninstalled successfully.");
                }
                catch (CommandException e) when (e.InnerException is Win32Exception inner)
                {
                    switch (inner.NativeErrorCode)
                    {
                        case Errors.ERROR_SERVICE_DOES_NOT_EXIST:
                            Log.Warn($"Service '{Format(descriptor)}' does not exist.");
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

                using var svc = new ServiceController(descriptor.Name);

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

                using var svc = new ServiceController(descriptor.Name);

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


                using var svc = new ServiceController(descriptor.Name);

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

                Log.Info("Restarting the service with id '" + descriptor.Name + "'");

                // run restart from another process group. see README.md for why this is useful.
                if (!ProcessApis.CreateProcess(
                    null,
                    descriptor.ExecutablePath + " restart",
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
                using var svc = new ServiceController(descriptor.Name);
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

                var wsvc = new WrapperService(descriptor);
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

                var wsvc = new WrapperService(descriptor);
                wsvc.RaiseOnStart(args.ToArray());
                Console.WriteLine("Press any key to stop the service...");
                _ = Console.Read();
                wsvc.RaiseOnStop();
            }

            // [DoesNotReturn]
            void Elevate()
            {
                using var current = Process.GetCurrentProcess();

                var startInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    FileName = current.MainModule!.FileName!,
#if NET
                    Arguments = "/elevated " + string.Join(' ', args),
#elif !NET20
                    Arguments = "/elevated " + string.Join(" ", args),
#else
                    Arguments = "/elevated " + string.Join(" ", args.ToArray()),
#endif
                    WindowStyle = ProcessWindowStyle.Hidden,
                };

                try
                {
                    using var elevated = Process.Start(startInfo)!;

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

        private static void InitLoggers(IWinSWConfiguration descriptor, bool enableConsoleLogging)
        {
            // TODO: Make logging levels configurable
            var fileLogLevel = Level.Debug;

            // TODO: Debug should not be printed to console by default. Otherwise commands like 'status' will be pollutted
            // This is a workaround till there is a better command line parsing, which will allow determining
            var consoleLogLevel = Level.Info;
            var eventLogLevel = Level.Warn;

            // Legacy format from winsw-1.x: (DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - " + message);
            var layout = new PatternLayout { ConversionPattern = "%d %-5p - %m%n" };
            layout.ActivateOptions();

            var appenders = new List<IAppender>();

            // .wrapper.log
            string wrapperLogPath = Path.Combine(descriptor.LogDirectory, descriptor.BaseName + ".wrapper.log");
            var wrapperLog = new FileAppender
            {
                AppendToFile = true,
                File = wrapperLogPath,
                ImmediateFlush = true,
                Name = "Wrapper file log",
                Threshold = fileLogLevel,
                LockingModel = new FileAppender.MinimalLock(),
                Layout = layout,
            };
            wrapperLog.ActivateOptions();
            appenders.Add(wrapperLog);

            // console log
            if (enableConsoleLogging)
            {
                var consoleAppender = new ConsoleAppender
                {
                    Name = "Wrapper console log",
                    Threshold = consoleLogLevel,
                    Layout = layout,
                };
                consoleAppender.ActivateOptions();
                appenders.Add(consoleAppender);
            }

            // event log
            var systemEventLogger = new ServiceEventLogAppender
            {
                Name = "Wrapper event log",
                Threshold = eventLogLevel,
                Provider = WrapperService.eventLogProvider,
            };
            systemEventLogger.ActivateOptions();
            appenders.Add(systemEventLogger);

            BasicConfigurator.Configure(
#if NET
                LogManager.GetRepository(Assembly.GetExecutingAssembly()),
#endif
                appenders.ToArray());
        }

        internal static unsafe bool IsProcessElevated()
        {
            var process = ProcessApis.GetCurrentProcess();
            if (!ProcessApis.OpenProcessToken(process, TokenAccessLevels.Read, out var token))
            {
                ThrowWin32Exception("Failed to open process token.");
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
                    ThrowWin32Exception("Failed to get token information");
                }

                return elevation.TokenIsElevated != 0;
            }
            finally
            {
                _ = HandleApis.CloseHandle(token);
            }

            static void ThrowWin32Exception(string message)
            {
                var inner = new Win32Exception();
                throw new Win32Exception(inner.NativeErrorCode, message + ' ' + inner.Message);
            }
        }

        private static string ReadPassword()
        {
            var buf = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                {
                    return buf.ToString();
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    _ = buf.Remove(buf.Length - 1, 1);
                    Console.Write("\b \b");
                }
                else
                {
                    Console.Write('*');
                    _ = buf.Append(key.KeyChar);
                }
            }
        }

        private static IWinSWConfiguration GetServiceDescriptor()
        {
            string executablePath = new DefaultWinSWSettings().ExecutablePath;
            string baseName = Path.GetFileNameWithoutExtension(executablePath);

            var d = new DirectoryInfo(Path.GetDirectoryName(executablePath)!);

            if (File.Exists(Path.Combine(d.FullName, baseName + ".xml")))
            {
                return new ServiceDescriptor(baseName, d);
            }

            if (File.Exists(Path.Combine(d.FullName, baseName + ".yml")))
            {
                return new ServiceDescriptorYaml(baseName, d).Configurations;
            }

            throw new FileNotFoundException($"Unable to locate { baseName }.[xml|yml] file within executable directory");
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

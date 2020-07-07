using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
#if NETCOREAPP
using System.Reflection;
#endif
using System.Runtime.InteropServices;
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
using WinSW.Logging;
using WinSW.Native;
using WinSW.Util;
using WMI;
using ServiceType = WMI.ServiceType;

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

        public static void Run(string[] argsArray, ServiceDescriptor? descriptor = null)
        {
            bool inConsoleMode = argsArray.Length > 0;

            // If descriptor is not specified, initialize the new one (and load configs from there)
            descriptor ??= new ServiceDescriptor();

            // Configure the wrapper-internal logging.
            // STDOUT and STDERR of the child process will be handled independently.
            InitLoggers(descriptor, inConsoleMode);

            if (!inConsoleMode)
            {
                Log.Debug("Starting WinSW in service mode");
                ServiceBase.Run(new WrapperService(descriptor));
                return;
            }

            Log.Debug("Starting WinSW in console mode");

            if (argsArray.Length == 0)
            {
                PrintHelp();
                return;
            }

            // Get service info for the future use
            IWin32Services svcs = new WmiRoot().GetCollection<IWin32Services>();
            IWin32Service? svc = svcs.Select(descriptor.Id);

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
                    Stop();
                    return;

                case "stopwait":
                    StopWait();
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

                Log.Info("Installing the service with id '" + descriptor.Id + "'");

                // Check if the service exists
                if (svc != null)
                {
                    Console.WriteLine("Service with id '" + descriptor.Id + "' already exists");
                    Console.WriteLine("To install the service, delete the existing one or change service Id in the configuration file");
                    throw new Exception("Installation failure: Service with id '" + descriptor.Id + "' already exists");
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
                    if (descriptor.HasServiceAccount())
                    {
                        username = descriptor.ServiceAccountUser;
                        password = descriptor.ServiceAccountPassword;
                        allowServiceLogonRight = descriptor.AllowServiceAcountLogonRight;
                    }
                }

                if (allowServiceLogonRight)
                {
                    Security.AddServiceLogonRight(descriptor.ServiceAccountDomain!, descriptor.ServiceAccountName!);
                }

                svcs.Create(
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

                using ServiceManager scm = ServiceManager.Open();
                using Service sc = scm.OpenService(descriptor.Id);

                sc.SetDescription(descriptor.Description);

                SC_ACTION[] actions = descriptor.FailureActions;
                if (actions.Length > 0)
                {
                    sc.SetFailureActions(descriptor.ResetFailureAfter, actions);
                }

                bool isDelayedAutoStart = descriptor.StartMode == StartMode.Automatic && descriptor.DelayedAutoStart;
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

                string eventLogSource = descriptor.Id;
                if (!EventLog.SourceExists(eventLogSource))
                {
                    EventLog.CreateEventSource(eventLogSource, "Application");
                }
            }

            void Uninstall()
            {
                if (!elevated)
                {
                    Elevate();
                    return;
                }

                Log.Info("Uninstalling the service with id '" + descriptor.Id + "'");
                if (svc is null)
                {
                    Log.Warn("The service with id '" + descriptor.Id + "' does not exist. Nothing to uninstall");
                    return; // there's no such service, so consider it already uninstalled
                }

                if (svc.Started)
                {
                    // We could fail the opeartion here, but it would be an incompatible change.
                    // So it is just a warning
                    Log.Warn("The service with id '" + descriptor.Id + "' is running. It may be impossible to uninstall it");
                }

                try
                {
                    svc.Delete();
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
            }

            void Start()
            {
                if (!elevated)
                {
                    Elevate();
                    return;
                }

                Log.Info("Starting the service with id '" + descriptor.Id + "'");
                if (svc is null)
                {
                    ThrowNoSuchService();
                }

                try
                {
                    svc.StartService();
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
            }

            void Stop()
            {
                if (!elevated)
                {
                    Elevate();
                    return;
                }

                Log.Info("Stopping the service with id '" + descriptor.Id + "'");
                if (svc is null)
                {
                    ThrowNoSuchService();
                }

                try
                {
                    svc.StopService();
                }
                catch (WmiException e)
                {
                    if (e.ErrorCode == ReturnValue.ServiceCannotAcceptControl)
                    {
                        Log.Info($"The service with ID '{descriptor.Id}' is not running");
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            void StopWait()
            {
                if (!elevated)
                {
                    Elevate();
                    return;
                }

                Log.Info("Stopping the service with id '" + descriptor.Id + "'");
                if (svc is null)
                {
                    ThrowNoSuchService();
                }

                if (svc.Started)
                {
                    svc.StopService();
                }

                while (svc != null && svc.Started)
                {
                    Log.Info("Waiting the service to stop...");
                    Thread.Sleep(1000);
                    svc = svcs.Select(descriptor.Id);
                }

                Log.Info("The service stopped.");
            }

            void Restart()
            {
                if (!elevated)
                {
                    Elevate();
                    return;
                }

                Log.Info("Restarting the service with id '" + descriptor.Id + "'");
                if (svc is null)
                {
                    ThrowNoSuchService();
                }

                if (svc.Started)
                {
                    svc.StopService();
                }

                while (svc.Started)
                {
                    Thread.Sleep(1000);
                    svc = svcs.Select(descriptor.Id)!;
                }

                svc.StartService();
            }

            void RestartSelf()
            {
                if (!elevated)
                {
                    throw new UnauthorizedAccessException("Access is denied.");
                }

                Log.Info("Restarting the service with id '" + descriptor.Id + "'");

                // run restart from another process group. see README.md for why this is useful.
                bool result = ProcessApis.CreateProcess(null, descriptor.ExecutablePath + " restart", IntPtr.Zero, IntPtr.Zero, false, ProcessApis.CREATE_NEW_PROCESS_GROUP, IntPtr.Zero, null, default, out _);
                if (!result)
                {
                    throw new Exception("Failed to invoke restart: " + Marshal.GetLastWin32Error());
                }
            }

            void Status()
            {
                Log.Debug("User requested the status of the process with id '" + descriptor.Id + "'");
                Console.WriteLine(svc is null ? "NonExistent" : svc.Started ? "Started" : "Stopped");
            }

            void Test()
            {
                if (!elevated)
                {
                    Elevate();
                    return;
                }

                WrapperService wsvc = new WrapperService(descriptor);
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

                WrapperService wsvc = new WrapperService(descriptor);
                wsvc.RaiseOnStart(args.ToArray());
                Console.WriteLine("Press any key to stop the service...");
                _ = Console.Read();
                wsvc.RaiseOnStop();
            }

            // [DoesNotReturn]
            void Elevate()
            {
                using Process current = Process.GetCurrentProcess();

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    FileName = current.MainModule.FileName,
#if NETCOREAPP
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
                    using Process elevated = Process.Start(startInfo);

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

        [DoesNotReturn]
        private static void ThrowNoSuchService() => throw new WmiException(ReturnValue.NoSuchService);

        private static void InitLoggers(ServiceDescriptor descriptor, bool enableConsoleLogging)
        {
            // TODO: Make logging levels configurable
            Level fileLogLevel = Level.Debug;

            // TODO: Debug should not be printed to console by default. Otherwise commands like 'status' will be pollutted
            // This is a workaround till there is a better command line parsing, which will allow determining
            Level consoleLogLevel = Level.Info;
            Level eventLogLevel = Level.Warn;

            // Legacy format from winsw-1.x: (DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - " + message);
            PatternLayout layout = new PatternLayout { ConversionPattern = "%d %-5p - %m%n" };
            layout.ActivateOptions();

            List<IAppender> appenders = new List<IAppender>();

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
#if NETCOREAPP
                LogManager.GetRepository(Assembly.GetExecutingAssembly()),
#endif
                appenders.ToArray());
        }

        internal static unsafe bool IsProcessElevated()
        {
            IntPtr process = ProcessApis.GetCurrentProcess();
            if (!ProcessApis.OpenProcessToken(process, TokenAccessLevels.Read, out IntPtr token))
            {
                ThrowWin32Exception("Failed to open process token.");
            }

            try
            {
                if (!SecurityApis.GetTokenInformation(
                    token,
                    SecurityApis.TOKEN_INFORMATION_CLASS.TokenElevation,
                    out SecurityApis.TOKEN_ELEVATION elevation,
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
                Win32Exception inner = new Win32Exception();
                throw new Win32Exception(inner.NativeErrorCode, message + ' ' + inner.Message);
            }
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

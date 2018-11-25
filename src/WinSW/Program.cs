﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
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
            catch (CommandException e)
            {
                string message = e.Message;
                Log.Fatal(message);
                Console.Error.WriteLine(message);
                return e.InnerException is Win32Exception inner ? inner.NativeErrorCode : -1;
            }
            catch (InvalidOperationException e) when (e.InnerException is Win32Exception inner)
            {
                string message = e.Message;
                Log.Fatal(message, e);
                Console.Error.WriteLine(message);
                return inner.NativeErrorCode;
            }
            catch (Win32Exception e)
            {
                string message = e.Message;
                Log.Fatal(message, e);
                Console.Error.WriteLine(message);
                return e.NativeErrorCode;
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

            var args = new List<string>(argsArray);

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

                using ServiceManager scm = ServiceManager.Open();

                if (scm.ServiceExists(descriptor.Id))
                {
                    Console.WriteLine("Service with id '" + descriptor.Id + "' already exists");
                    Console.WriteLine("To install the service, delete the existing one or change service Id in the configuration file");
                    throw new CommandException("Installation failure: Service with id '" + descriptor.Id + "' already exists");
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
                else if (descriptor.HasServiceAccount())
                {
                    username = descriptor.ServiceAccountUserName;
                    password = descriptor.ServiceAccountPassword;
                    allowServiceLogonRight = descriptor.AllowServiceAcountLogonRight;

                    if (username is null || password is null)
                    {
                        switch (descriptor.ServiceAccountPrompt)
                        {
                            case "dialog":
                                PropmtForCredentialsDialog();
                                break;

                            case "console":
                                PromptForCredentialsConsole();
                                break;
                        }
                    }
                }

                if (allowServiceLogonRight)
                {
                    Security.AddServiceLogonRight(descriptor.ServiceAccountUserName!);
                }

                using Service sc = scm.CreateService(
                    descriptor.Id,
                    descriptor.Caption,
                    descriptor.StartMode,
                    "\"" + descriptor.ExecutablePath + "\"",
                    descriptor.ServiceDependencies,
                    username,
                    password);

                sc.SetDescription(descriptor.Description);

                SC_ACTION[] actions = descriptor.FailureActions;
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

                string eventLogSource = descriptor.Id;
                if (!EventLog.SourceExists(eventLogSource))
                {
                    EventLog.CreateEventSource(eventLogSource, "Application");
                }

                void PropmtForCredentialsDialog()
                {
                    username ??= string.Empty;
                    password ??= string.Empty;

                    int inBufferSize = 0;
                    _ = CredentialApis.CredPackAuthenticationBuffer(
                        0,
                        username,
                        password,
                        IntPtr.Zero,
                        ref inBufferSize);

                    IntPtr inBuffer = Marshal.AllocCoTaskMem(inBufferSize);
                    try
                    {
                        if (!CredentialApis.CredPackAuthenticationBuffer(
                            0,
                            username,
                            password,
                            inBuffer,
                            ref inBufferSize))
                        {
                            Throw.Command.Win32Exception("Failed to pack auth buffer.");
                        }

                        CredentialApis.CREDUI_INFO info = new CredentialApis.CREDUI_INFO
                        {
                            Size = Marshal.SizeOf(typeof(CredentialApis.CREDUI_INFO)),
                            CaptionText = "Windows Service Wrapper", // TODO
                            MessageText = "service account credentials", // TODO
                        };
                        uint authPackage = 0;
                        bool save = false;
                        int error = CredentialApis.CredUIPromptForWindowsCredentials(
                            info,
                            0,
                            ref authPackage,
                            inBuffer,
                            inBufferSize,
                            out IntPtr outBuffer,
                            out uint outBufferSize,
                            ref save,
                            CredentialApis.CREDUIWIN_GENERIC);

                        if (error != Errors.ERROR_SUCCESS)
                        {
                            throw new Win32Exception(error);
                        }

                        try
                        {
                            int userNameLength = 0;
                            int passwordLength = 0;
                            _ = CredentialApis.CredUnPackAuthenticationBuffer(
                                0,
                                outBuffer,
                                outBufferSize,
                                null,
                                ref userNameLength,
                                default,
                                default,
                                null,
                                ref passwordLength);

                            username = userNameLength == 0 ? null : new string('\0', userNameLength - 1);
                            password = passwordLength == 0 ? null : new string('\0', passwordLength - 1);

                            if (!CredentialApis.CredUnPackAuthenticationBuffer(
                                0,
                                outBuffer,
                                outBufferSize,
                                username,
                                ref userNameLength,
                                default,
                                default,
                                password,
                                ref passwordLength))
                            {
                                Throw.Command.Win32Exception("Failed to unpack auth buffer.");
                            }
                        }
                        finally
                        {
                            Marshal.FreeCoTaskMem(outBuffer);
                        }
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(inBuffer);
                    }
                }

                void PromptForCredentialsConsole()
                {
                    if (username is null)
                    {
                        Console.Write("Username: ");
                        username = Console.ReadLine();
                    }

                    if (password is null)
                    {
                        Console.Write("Password: ");
                        password = ReadPassword();
                    }

                    Console.WriteLine();
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

                using ServiceManager scm = ServiceManager.Open();
                try
                {
                    using Service sc = scm.OpenService(descriptor.Id);

                    if (sc.Status == ServiceControllerStatus.Running)
                    {
                        // We could fail the opeartion here, but it would be an incompatible change.
                        // So it is just a warning
                        Log.Warn("The service with id '" + descriptor.Id + "' is running. It may be impossible to uninstall it");
                    }

                    sc.Delete();
                }
                catch (CommandException e) when (e.InnerException is Win32Exception inner)
                {
                    switch (inner.NativeErrorCode)
                    {
                        case Errors.ERROR_SERVICE_DOES_NOT_EXIST:
                            Log.Warn("The service with id '" + descriptor.Id + "' does not exist. Nothing to uninstall");
                            break; // there's no such service, so consider it already uninstalled

                        case Errors.ERROR_SERVICE_MARKED_FOR_DELETE:
                            Log.Error("Failed to uninstall the service with id '" + descriptor.Id + "'"
                               + ". It has been marked for deletion.");

                            // TODO: change the default behavior to Error?
                            break; // it's already uninstalled, so consider it a success

                        default:
                            Log.Fatal("Failed to uninstall the service with id '" + descriptor.Id + "'. Error code is '" + inner.NativeErrorCode + "'");
                            throw;
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

                Log.Info("Starting the service with id '" + descriptor.Id + "'");

                using var svc = new ServiceController(descriptor.Id);

                try
                {
                    svc.Start();
                }
                catch (InvalidOperationException e) when (e.InnerException is Win32Exception inner)
                {
                    switch (inner.NativeErrorCode)
                    {
                        case Errors.ERROR_SERVICE_DOES_NOT_EXIST:
                            ThrowNoSuchService(inner);
                            break;

                        case Errors.ERROR_SERVICE_ALREADY_RUNNING:
                            Log.Info($"The service with ID '{descriptor.Id}' has already been started");
                            break;

                        default:
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

                using var svc = new ServiceController(descriptor.Id);

                try
                {
                    svc.Stop();
                }
                catch (InvalidOperationException e) when (e.InnerException is Win32Exception inner)
                {
                    switch (inner.NativeErrorCode)
                    {
                        case Errors.ERROR_SERVICE_DOES_NOT_EXIST:
                            ThrowNoSuchService(inner);
                            break;

                        case Errors.ERROR_SERVICE_NOT_ACTIVE:
                            Log.Info($"The service with ID '{descriptor.Id}' is not running");
                            break;

                        default:
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

                using var svc = new ServiceController(descriptor.Id);

                try
                {
                    svc.Stop();

                    while (!ServiceControllerExtension.TryWaitForStatus(svc, ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(1)))
                    {
                        Log.Info("Waiting the service to stop...");
                    }
                }
                catch (InvalidOperationException e) when (e.InnerException is Win32Exception inner)
                {
                    switch (inner.NativeErrorCode)
                    {
                        case Errors.ERROR_SERVICE_DOES_NOT_EXIST:
                            ThrowNoSuchService(inner);
                            break;

                        case Errors.ERROR_SERVICE_NOT_ACTIVE:
                            break;

                        default:
                            throw;
                    }
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

                using var svc = new ServiceController(descriptor.Id);

                try
                {
                    svc.Stop();

                    while (!ServiceControllerExtension.TryWaitForStatus(svc, ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(1)))
                    {
                    }
                }
                catch (InvalidOperationException e) when (e.InnerException is Win32Exception inner)
                {
                    switch (inner.NativeErrorCode)
                    {
                        case Errors.ERROR_SERVICE_DOES_NOT_EXIST:
                            ThrowNoSuchService(inner);
                            break;

                        case Errors.ERROR_SERVICE_NOT_ACTIVE:
                            break;

                        default:
                            throw;

                    }
                }

                svc.Start();
            }

            void RestartSelf()
            {
                if (!elevated)
                {
                    throw new UnauthorizedAccessException("Access is denied.");
                }

                Log.Info("Restarting the service with id '" + descriptor.Id + "'");

                // run restart from another process group. see README.md for why this is useful.

                if (!ProcessApis.CreateProcess(null, descriptor.ExecutablePath + " restart", IntPtr.Zero, IntPtr.Zero, false, ProcessApis.CREATE_NEW_PROCESS_GROUP, IntPtr.Zero, null, default, out _))
                {
                    throw new CommandException("Failed to invoke restart: " + Marshal.GetLastWin32Error());
                }
            }

            void Status()
            {
                Log.Debug("User requested the status of the process with id '" + descriptor.Id + "'");
                using var svc = new ServiceController(descriptor.Id);
                try
                {
                    Console.WriteLine(svc.Status == ServiceControllerStatus.Running ? "Started" : "Stopped");
                }
                catch (InvalidOperationException e) when (e.InnerException is Win32Exception inner && inner.NativeErrorCode == Errors.ERROR_SERVICE_DOES_NOT_EXIST)
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

                using WrapperService wsvc = new WrapperService(descriptor);
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

                using WrapperService wsvc = new WrapperService(descriptor);
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
#else
                    Arguments = "/elevated " + string.Join(" ", args),
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

        /// <exception cref="CommandException" />
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowNoSuchService(Win32Exception inner) => throw new CommandException(inner);

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
                LogManager.GetRepository(System.Reflection.Assembly.GetExecutingAssembly()),
#endif
                appenders.ToArray());
        }

        /// <exception cref="CommandException" />
        internal static bool IsProcessElevated()
        {
            IntPtr process = ProcessApis.GetCurrentProcess();
            if (!ProcessApis.OpenProcessToken(process, TokenAccessLevels.Read, out IntPtr token))
            {
                Throw.Command.Win32Exception("Failed to open process token.");
            }

            try
            {
                unsafe
                {
                    if (!SecurityApis.GetTokenInformation(
                        token,
                        SecurityApis.TOKEN_INFORMATION_CLASS.TokenElevation,
                        out SecurityApis.TOKEN_ELEVATION elevation,
                        sizeof(SecurityApis.TOKEN_ELEVATION),
                        out _))
                    {
                        Throw.Command.Win32Exception("Failed to get token information.");
                    }

                    return elevation.TokenIsElevated != 0;
                }
            }
            finally
            {
                _ = HandleApis.CloseHandle(token);
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

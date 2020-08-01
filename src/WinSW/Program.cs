using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Layout;
using WinSW.Logging;
using WinSW.Native;
using WinSW.Util;
using Process = System.Diagnostics.Process;
using TimeoutException = System.ServiceProcess.TimeoutException;

namespace WinSW
{
    // NOTE: Keep description strings in sync with docs.
    public static class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        internal static Action<Exception, InvocationContext>? TestExceptionHandler;

        private static int Main(string[] args)
        {
            int exitCode = Run(args);
            Log.Debug("Completed. Exit code is " + exitCode);
            return exitCode;
        }

        internal static int Run(string[] args)
        {
            bool elevated;
            if (args[0] == "--elevated")
            {
                elevated = true;

                _ = ConsoleApis.FreeConsole();
                _ = ConsoleApis.AttachConsole(ConsoleApis.ATTACH_PARENT_PROCESS);

                args = new List<string>(args).GetRange(1, args.Length - 1).ToArray();
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

            var root = new RootCommand("A wrapper binary that can be used to host executables as Windows services. https://github.com/winsw/winsw")
            {
                Handler = CommandHandler.Create((string? pathToConfig) =>
                {
                    XmlServiceConfig config;
                    try
                    {
                        config = XmlServiceConfig.Create(pathToConfig);
                    }
                    catch (FileNotFoundException)
                    {
                        throw new CommandException("The specified command or file was not found.");
                    }

                    InitLoggers(config, enableConsoleLogging: false);

                    Log.Debug("Starting WinSW in service mode");
                    ServiceBase.Run(new WrapperService(config));
                }),
            };

            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                if (principal.IsInRole(new SecurityIdentifier(WellKnownSidType.ServiceSid, null)) ||
                    principal.IsInRole(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null)) ||
                    principal.IsInRole(new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null)) ||
                    principal.IsInRole(new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null)))
                {
                    root.Add(new Argument<string?>("path-to-config")
                    {
                        Arity = ArgumentArity.ZeroOrOne,
                        IsHidden = true,
                    });
                }
            }

            var config = new Argument<string?>("path-to-config", "The path to the configuration file.")
            {
                Arity = ArgumentArity.ZeroOrOne,
            };

            var noElevate = new Option("--no-elevate", "Doesn't automatically trigger a UAC prompt.");

            {
                var install = new Command("install", "Installs the service.")
                {
                    Handler = CommandHandler.Create<string?, bool, string?, string?>(Install),
                };

                install.Add(config);
                install.Add(noElevate);
                install.Add(new Option<string?>(new[] { "--username", "--user" }, "Specifies the user name of the service account."));
                install.Add(new Option<string?>(new[] { "--password", "--pass" }, "Specifies the password of the service account."));

                root.Add(install);
            }

            {
                var uninstall = new Command("uninstall", "Uninstalls the service.")
                {
                    Handler = CommandHandler.Create<string?, bool>(Uninstall),
                };

                uninstall.Add(config);
                uninstall.Add(noElevate);

                root.Add(uninstall);
            }

            {
                var start = new Command("start", "Starts the service.")
                {
                    Handler = CommandHandler.Create<string?, bool>(Start),
                };

                start.Add(config);
                start.Add(noElevate);

                root.Add(start);
            }

            {
                var stop = new Command("stop", "Stops the service.")
                {
                    Handler = CommandHandler.Create<string?, bool, bool, bool>(Stop),
                };

                stop.Add(config);
                stop.Add(noElevate);
                stop.Add(new Option("--no-wait", "Doesn't wait for the service to actually stop."));
                stop.Add(new Option("--force", "Stops the service even if it has started dependent services."));

                root.Add(stop);
            }

            {
                var restart = new Command("restart", "Stops and then starts the service.")
                {
                    Handler = CommandHandler.Create<string?, bool, bool>(Restart),
                };

                restart.Add(config);
                restart.Add(noElevate);
                restart.Add(new Option("--force", "Restarts the service even if it has started dependent services."));

                root.Add(restart);
            }

            {
                var restartSelf = new Command("restart!", "self-restart (can be called from child processes)")
                {
                    Handler = CommandHandler.Create<string?>(RestartSelf),
                };

                restartSelf.Add(config);

                root.Add(restartSelf);
            }

            {
                var status = new Command("status", "Checks the status of the service.")
                {
                    Handler = CommandHandler.Create<string?>(Status),
                };

                status.Add(config);

                root.Add(status);
            }

            {
                var test = new Command("test", "Checks if the service can be started and then stopped without installation.")
                {
                    Handler = CommandHandler.Create<string?, bool, int?, bool>(Test),
                };

                test.Add(config);
                test.Add(noElevate);

                const int minTimeout = -1;
                const int maxTimeout = int.MaxValue / 1000;

                var timeout = new Option<int>("--timeout", "Specifies the number of seconds to wait before the service is stopped.");
                timeout.Argument.AddValidator(argument =>
                {
                    string token = argument.Tokens.Single().Value;
                    return !int.TryParse(token, out int value) ? null :
                        value < minTimeout ? $"Argument '{token}' must be greater than or equal to {minTimeout}." :
                        value > maxTimeout ? $"Argument '{token}' must be less than or equal to {maxTimeout}." :
                        null;
                });

                test.Add(timeout);
                test.Add(new Option("--no-break", "Ignores keystrokes."));

                root.Add(test);
            }

            {
                var refresh = new Command("refresh", "Refreshes the service properties without reinstallation.")
                {
                    Handler = CommandHandler.Create<string?, bool>(Refresh),
                };

                refresh.Add(config);
                refresh.Add(noElevate);

                root.Add(refresh);
            }

            {
                var dev = new Command("dev");

                dev.Add(config);
                dev.Add(noElevate);

                root.Add(dev);

                var ps = new Command("ps")
                {
                    Handler = CommandHandler.Create<string?, bool>(DevPs),
                };

                dev.Add(ps);
            }

            return new CommandLineBuilder(root)

                // see UseDefaults
                .UseVersionOption()
                .UseHelp()
                /* .UseEnvironmentVariableDirective() */
                .UseParseDirective()
                .UseDebugDirective()
                .UseSuggestDirective()
                .RegisterWithDotnetSuggest()
                .UseTypoCorrections()
                .UseParseErrorReporting()
                .UseExceptionHandler(TestExceptionHandler ?? OnException)
                .CancelOnProcessTermination()
                .Build()
                .Invoke(args);

            static void OnException(Exception exception, InvocationContext context)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                try
                {
                    IStandardStreamWriter error = context.Console.Error;

                    Debug.Assert(exception is TargetInvocationException);
                    Debug.Assert(exception.InnerException != null);
                    exception = exception.InnerException!;
                    switch (exception)
                    {
                        case InvalidDataException e:
                            {
                                string message = "The configuration file cound not be loaded. " + e.Message;
                                Log.Fatal(message, e);
                                error.WriteLine(message);
                                context.ResultCode = -1;
                                break;
                            }

                        case CommandException e:
                            {
                                string message = e.Message;
                                Log.Fatal(message);
                                error.WriteLine(message);
                                context.ResultCode = e.InnerException is Win32Exception inner ? inner.NativeErrorCode : -1;
                                break;
                            }

                        case InvalidOperationException e when e.InnerException is Win32Exception inner:
                            {
                                string message = e.Message;
                                Log.Fatal(message, e);
                                error.WriteLine(message);
                                context.ResultCode = inner.NativeErrorCode;
                                break;
                            }

                        case Win32Exception e:
                            {
                                string message = e.Message;
                                Log.Fatal(message, e);
                                error.WriteLine(message);
                                context.ResultCode = e.NativeErrorCode;
                                break;
                            }

                        default:
                            {
                                Log.Fatal("Unhandled exception", exception);
                                error.WriteLine(exception.ToString());
                                context.ResultCode = -1;
                                break;
                            }
                    }
                }
                finally
                {
                    Console.ResetColor();
                }
            }

            void Install(string? pathToConfig, bool noElevate, string? username, string? password)
            {
                XmlServiceConfig config = XmlServiceConfig.Create(pathToConfig);
                InitLoggers(config, enableConsoleLogging: true);

                if (!elevated)
                {
                    Elevate(noElevate);
                    return;
                }

                Log.Info("Installing the service with id '" + config.Id + "'");

                using ServiceManager scm = ServiceManager.Open();

                if (scm.ServiceExists(config.Id))
                {
                    Console.WriteLine("Service with id '" + config.Id + "' already exists");
                    Console.WriteLine("To install the service, delete the existing one or change service Id in the configuration file");
                    throw new CommandException("Installation failure: Service with id '" + config.Id + "' already exists");
                }

                if (config.HasServiceAccount())
                {
                    username = config.ServiceAccountUserName ?? username;
                    password = config.ServiceAccountPassword ?? password;

                    if (username is null || password is null && !IsSpecialAccount(username))
                    {
                        switch (config.ServiceAccountPrompt)
                        {
                            case "dialog":
                                Credentials.PropmtForCredentialsDialog(
                                    ref username,
                                    ref password,
                                    "Windows Service Wrapper",
                                    "service account credentials"); // TODO
                                break;

                            case "console":
                                PromptForCredentialsConsole();
                                break;
                        }
                    }
                }

                if (username != null && !IsSpecialAccount(username))
                {
                    Security.AddServiceLogonRight(ref username);
                }

                using Service sc = scm.CreateService(
                    config.Id,
                    config.Caption,
                    config.StartMode,
                    "\"" + config.ExecutablePath + "\"" + (pathToConfig != null ? " \"" + Path.GetFullPath(pathToConfig) + "\"" : null),
                    config.ServiceDependencies,
                    username,
                    password);

                string description = config.Description;
                if (description.Length != 0)
                {
                    sc.SetDescription(description);
                }

                SC_ACTION[] actions = config.FailureActions;
                if (actions.Length > 0)
                {
                    sc.SetFailureActions(config.ResetFailureAfter, actions);
                }

                bool isDelayedAutoStart = config.StartMode == ServiceStartMode.Automatic && config.DelayedAutoStart;
                if (isDelayedAutoStart)
                {
                    sc.SetDelayedAutoStart(true);
                }

                if (config.PreshutdownTimeout is TimeSpan preshutdownTimeout)
                {
                    sc.SetPreshutdownTimeout(preshutdownTimeout);
                }

                string? securityDescriptor = config.SecurityDescriptor;
                if (securityDescriptor != null)
                {
                    // throws ArgumentException
                    sc.SetSecurityDescriptor(new RawSecurityDescriptor(securityDescriptor));
                }

                string eventLogSource = config.Id;
                if (!EventLog.SourceExists(eventLogSource))
                {
                    EventLog.CreateEventSource(eventLogSource, "Application");
                }

                void PromptForCredentialsConsole()
                {
                    if (username is null)
                    {
                        Console.Write("Username: ");
                        username = Console.ReadLine();
                    }

                    if (password is null && !IsSpecialAccount(username))
                    {
                        Console.Write("Password: ");
                        password = ReadPassword();
                    }

                    Console.WriteLine();
                }

                static bool IsSpecialAccount(string accountName) => accountName switch
                {
                    @"LocalSystem" => true,
                    @".\LocalSystem" => true,
                    @"NT AUTHORITY\LocalService" => true,
                    @"NT AUTHORITY\NetworkService" => true,
                    string name when name == $@"{Environment.MachineName}\LocalSystem" => true,
                    _ => false
                };
            }

            void Uninstall(string? pathToConfig, bool noElevate)
            {
                XmlServiceConfig config = XmlServiceConfig.Create(pathToConfig);
                InitLoggers(config, enableConsoleLogging: true);

                if (!elevated)
                {
                    Elevate(noElevate);
                    return;
                }

                Log.Info("Uninstalling the service with id '" + config.Id + "'");

                using ServiceManager scm = ServiceManager.Open();
                try
                {
                    using Service sc = scm.OpenService(config.Id);

                    if (sc.Status == ServiceControllerStatus.Running)
                    {
                        // We could fail the opeartion here, but it would be an incompatible change.
                        // So it is just a warning
                        Log.Warn("The service with id '" + config.Id + "' is running. It may be impossible to uninstall it");
                    }

                    sc.Delete();
                }
                catch (CommandException e) when (e.InnerException is Win32Exception inner)
                {
                    switch (inner.NativeErrorCode)
                    {
                        case Errors.ERROR_SERVICE_DOES_NOT_EXIST:
                            Log.Warn("The service with id '" + config.Id + "' does not exist. Nothing to uninstall");
                            break; // there's no such service, so consider it already uninstalled

                        case Errors.ERROR_SERVICE_MARKED_FOR_DELETE:
                            Log.Error("Failed to uninstall the service with id '" + config.Id + "'"
                               + ". It has been marked for deletion.");

                            // TODO: change the default behavior to Error?
                            break; // it's already uninstalled, so consider it a success

                        default:
                            Log.Fatal("Failed to uninstall the service with id '" + config.Id + "'. Error code is '" + inner.NativeErrorCode + "'");
                            throw;
                    }
                }
            }

            void Start(string? pathToConfig, bool noElevate)
            {
                XmlServiceConfig config = XmlServiceConfig.Create(pathToConfig);
                InitLoggers(config, enableConsoleLogging: true);

                if (!elevated)
                {
                    Elevate(noElevate);
                    return;
                }

                Log.Info("Starting the service with id '" + config.Id + "'");

                using var svc = new ServiceController(config.Id);

                try
                {
                    svc.Start();
                }
                catch (InvalidOperationException e)
                when (e.InnerException is Win32Exception inner && inner.NativeErrorCode == Errors.ERROR_SERVICE_DOES_NOT_EXIST)
                {
                    ThrowNoSuchService(inner);
                }
                catch (InvalidOperationException e)
                when (e.InnerException is Win32Exception inner && inner.NativeErrorCode == Errors.ERROR_SERVICE_ALREADY_RUNNING)
                {
                    Log.Info($"The service with ID '{config.Id}' has already been started");
                }
            }

            void Stop(string? pathToConfig, bool noElevate, bool noWait, bool force)
            {
                XmlServiceConfig config = XmlServiceConfig.Create(pathToConfig);
                InitLoggers(config, enableConsoleLogging: true);

                if (!elevated)
                {
                    Elevate(noElevate);
                    return;
                }

                Log.Info("Stopping the service with id '" + config.Id + "'");

                using var svc = new ServiceController(config.Id);

                try
                {
                    if (!force)
                    {
                        if (svc.HasAnyStartedDependentService())
                        {
                            throw new CommandException("Failed to stop the service because it has started dependent services. Specify '--force' to proceed.");
                        }
                    }

                    svc.Stop();

                    if (!noWait)
                    {
                        Log.Info("Waiting for the service to stop...");
                        try
                        {
                            svc.WaitForStatus(ServiceControllerStatus.Stopped, ServiceControllerStatus.StopPending);
                        }
                        catch (TimeoutException e)
                        {
                            throw new CommandException("Failed to stop the service.", e);
                        }
                    }
                }
                catch (InvalidOperationException e)
                when (e.InnerException is Win32Exception inner && inner.NativeErrorCode == Errors.ERROR_SERVICE_DOES_NOT_EXIST)
                {
                    ThrowNoSuchService(inner);
                }
                catch (InvalidOperationException e)
                when (e.InnerException is Win32Exception inner && inner.NativeErrorCode == Errors.ERROR_SERVICE_NOT_ACTIVE)
                {
                }

                Log.Info("The service stopped.");
            }

            void Restart(string? pathToConfig, bool noElevate, bool force)
            {
                XmlServiceConfig config = XmlServiceConfig.Create(pathToConfig);
                InitLoggers(config, enableConsoleLogging: true);

                if (!elevated)
                {
                    Elevate(noElevate);
                    return;
                }

                Log.Info("Restarting the service with id '" + config.Id + "'");

                using var svc = new ServiceController(config.Id);

                List<ServiceController>? startedDependentServices = null;

                try
                {
                    if (svc.HasAnyStartedDependentService())
                    {
                        if (!force)
                        {
                            throw new CommandException("Failed to restart the service because it has started dependent services. Specify '--force' to proceed.");
                        }

                        startedDependentServices = svc.DependentServices.Where(service => service.Status != ServiceControllerStatus.Stopped).ToList();
                    }

                    svc.Stop();

                    Log.Info("Waiting for the service to stop...");
                    try
                    {
                        svc.WaitForStatus(ServiceControllerStatus.Stopped, ServiceControllerStatus.StopPending);
                    }
                    catch (TimeoutException e)
                    {
                        throw new CommandException("Failed to stop the service.", e);
                    }
                }
                catch (InvalidOperationException e)
                when (e.InnerException is Win32Exception inner && inner.NativeErrorCode == Errors.ERROR_SERVICE_DOES_NOT_EXIST)
                {
                    ThrowNoSuchService(inner);
                }
                catch (InvalidOperationException e)
                when (e.InnerException is Win32Exception inner && inner.NativeErrorCode == Errors.ERROR_SERVICE_NOT_ACTIVE)
                {
                }

                svc.Start();

                if (startedDependentServices != null)
                {
                    foreach (ServiceController service in startedDependentServices)
                    {
                        if (service.Status == ServiceControllerStatus.Stopped)
                        {
                            service.Start();
                        }
                    }
                }
            }

            void RestartSelf(string? pathToConfig)
            {
                XmlServiceConfig config = XmlServiceConfig.Create(pathToConfig);
                InitLoggers(config, enableConsoleLogging: true);

                if (!elevated)
                {
                    throw new CommandException(new Win32Exception(Errors.ERROR_ACCESS_DENIED));
                }

                Log.Info("Restarting the service with id '" + config.Id + "'");

                // run restart from another process group. see README.md for why this is useful.

                if (!ProcessApis.CreateProcess(null, config.ExecutablePath + " restart", IntPtr.Zero, IntPtr.Zero, false, ProcessApis.CREATE_NEW_PROCESS_GROUP, IntPtr.Zero, null, default, out _))
                {
                    throw new CommandException("Failed to invoke restart: " + Marshal.GetLastWin32Error());
                }
            }

            static void Status(string? pathToConfig)
            {
                XmlServiceConfig config = XmlServiceConfig.Create(pathToConfig);
                InitLoggers(config, enableConsoleLogging: true);

                Log.Debug("User requested the status of the process with id '" + config.Id + "'");
                using var svc = new ServiceController(config.Id);
                try
                {
                    Console.WriteLine(svc.Status == ServiceControllerStatus.Running ? "Started" : "Stopped");
                }
                catch (InvalidOperationException e)
                when (e.InnerException is Win32Exception inner && inner.NativeErrorCode == Errors.ERROR_SERVICE_DOES_NOT_EXIST)
                {
                    Console.WriteLine("NonExistent");
                }
            }

            void Test(string? pathToConfig, bool noElevate, int? timeout, bool noBreak)
            {
                XmlServiceConfig config = XmlServiceConfig.Create(pathToConfig);
                InitLoggers(config, enableConsoleLogging: true);

                if (!elevated)
                {
                    Elevate(noElevate);
                    return;
                }

                using WrapperService wsvc = new WrapperService(config);
                wsvc.RaiseOnStart(args);
                try
                {
                    // validated [-1, int.MaxValue / 1000]
                    int millisecondsTimeout = timeout is int secondsTimeout && secondsTimeout >= 0 ? secondsTimeout * 1000 : -1;

                    if (!noBreak)
                    {
                        Console.WriteLine("Press any key to stop the service...");
                        _ = Task.Run(() => _ = Console.ReadKey()).Wait(millisecondsTimeout);
                    }
                    else
                    {
                        using ManualResetEventSlim evt = new ManualResetEventSlim();

                        Console.WriteLine("Press Ctrl+C to stop the service...");
                        Console.CancelKeyPress += CancelKeyPress;

                        _ = evt.Wait(millisecondsTimeout);
                        Console.CancelKeyPress -= CancelKeyPress;

                        void CancelKeyPress(object sender, ConsoleCancelEventArgs e)
                        {
                            evt.Set();
                        }
                    }
                }
                finally
                {
                    wsvc.RaiseOnStop();
                }
            }

            void Refresh(string? pathToConfig, bool noElevate)
            {
                XmlServiceConfig config = XmlServiceConfig.Create(pathToConfig);
                InitLoggers(config, enableConsoleLogging: true);

                if (!elevated)
                {
                    Elevate(noElevate);
                    return;
                }

                using ServiceManager scm = ServiceManager.Open();
                try
                {
                    using Service sc = scm.OpenService(config.Id);

                    sc.ChangeConfig(config.Caption, config.StartMode, config.ServiceDependencies);

                    sc.SetDescription(config.Description);

                    SC_ACTION[] actions = config.FailureActions;
                    if (actions.Length > 0)
                    {
                        sc.SetFailureActions(config.ResetFailureAfter, actions);
                    }

                    bool isDelayedAutoStart = config.StartMode == ServiceStartMode.Automatic && config.DelayedAutoStart;
                    if (isDelayedAutoStart)
                    {
                        sc.SetDelayedAutoStart(true);
                    }

                    if (config.PreshutdownTimeout is TimeSpan preshutdownTimeout)
                    {
                        sc.SetPreshutdownTimeout(preshutdownTimeout);
                    }

                    string? securityDescriptor = config.SecurityDescriptor;
                    if (securityDescriptor != null)
                    {
                        // throws ArgumentException
                        sc.SetSecurityDescriptor(new RawSecurityDescriptor(securityDescriptor));
                    }
                }
                catch (CommandException e)
                when (e.InnerException is Win32Exception inner && inner.NativeErrorCode == Errors.ERROR_SERVICE_DOES_NOT_EXIST)
                {
                    ThrowNoSuchService(inner);
                }
            }

            void DevPs(string? pathToConfig, bool noElevate)
            {
                XmlServiceConfig config = XmlServiceConfig.Create(pathToConfig);

                if (!elevated)
                {
                    Elevate(noElevate);
                    return;
                }

                using ServiceManager scm = ServiceManager.Open();
                using Service sc = scm.OpenService(config.Id);

                int processId = sc.ProcessId;
                if (processId >= 0)
                {
                    const string Vertical = " \u2502 ";
                    const string Corner = " \u2514\u2500";
                    const string Cross = " \u251c\u2500";
                    const string Space = "   ";

                    using Process process = Process.GetProcessById(processId);
                    Draw(process, string.Empty, true);

                    static void Draw(Process process, string indentation, bool isLastChild)
                    {
                        Console.Write(indentation);

                        if (isLastChild)
                        {
                            Console.Write(Corner);
                            indentation += Space;
                        }
                        else
                        {
                            Console.Write(Cross);
                            indentation += Vertical;
                        }

                        Console.WriteLine(process.Format());

                        List<Process> children = process.GetChildren();
                        int count = children.Count;
                        for (int i = 0; i < count; i++)
                        {
                            using Process child = children[i];
                            Draw(child, indentation, i == count - 1);
                        }
                    }
                }
            }

            // [DoesNotReturn]
            static void Elevate(bool noElevate)
            {
                if (noElevate)
                {
                    throw new CommandException(new Win32Exception(Errors.ERROR_ACCESS_DENIED));
                }

                using Process current = Process.GetCurrentProcess();

                string exe = Environment.GetCommandLineArgs()[0];
                string commandLine = Environment.CommandLine;
                string arguments = "--elevated" + commandLine.Remove(commandLine.IndexOf(exe), exe.Length).TrimStart('"');

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    FileName = current.MainModule.FileName,
                    Arguments = arguments,
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

        private static void InitLoggers(XmlServiceConfig config, bool enableConsoleLogging)
        {
            if (XmlServiceConfig.TestConfig != null)
            {
                return;
            }

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
            string wrapperLogPath = Path.Combine(config.LogDirectory, config.BaseName + ".wrapper.log");
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
            var systemEventLogger = new ServiceEventLogAppender(WrapperService.eventLogProvider)
            {
                Name = "Wrapper event log",
                Threshold = eventLogLevel,
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
    }
}

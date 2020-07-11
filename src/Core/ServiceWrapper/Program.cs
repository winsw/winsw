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
using winsw.Logging;
using winsw.Native;
using winsw.Util;
using WMI;
using ServiceType = WMI.ServiceType;
using CommandLine;
using System.Reflection;
using System.Linq;
using winsw.CLI;

namespace winsw
{
    public static class Program
    {
        public static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        public static bool elevated;

        public static CLICommand cliOption;

        public static int Main(string[] args)
        {
            var types = LoadVerbs();

            try
            {
                Parser.Default.ParseArguments(args, types)
                        .WithParsed(RunParsed)
                        .WithNotParsed(HandleErrors);

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

        private static Type[] LoadVerbs()
        {
            return Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.GetCustomAttribute<VerbAttribute>() != null).ToArray();
        }

        private static void HandleErrors(IEnumerable<Error> errors)
        {

        }

        public static void RunParsed(object obj)
        {
            Run(obj, null);
        }

        public static void Run(object obj, ServiceDescriptor? descriptor = null)
        {
            cliOption = (CLICommand)obj;

            bool inConsoleMode = obj.GetType().Name != "DefaultVerb";

            Console.WriteLine(inConsoleMode);

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


            // Get service info for the future use
            Win32Services svcs = new WmiRoot().GetCollection<Win32Services>();
            Win32Service? svc = svcs.Select(descriptor.Id);

            if (!string.IsNullOrEmpty(cliOption.RedirectPath))
            {
                cliOption.redirect();
            }


            if (cliOption.Elevated)
            {
                elevated = true;

                _ = ConsoleApis.FreeConsole();
                _ = ConsoleApis.AttachConsole(ConsoleApis.ATTACH_PARENT_PROCESS);

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

            // Run the Command
            cliOption.Run(descriptor, svcs, svc);

        }

        [DoesNotReturn]
        public static void ThrowNoSuchService() => throw new WmiException(ReturnValue.NoSuchService);

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
                provider = WrapperService.eventLogProvider,
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

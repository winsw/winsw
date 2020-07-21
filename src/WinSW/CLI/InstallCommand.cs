using CommandLine;
using System;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Text;
using winsw.Native;
using WMI;

namespace winsw.CLI
{
    [Verb("install", HelpText = "install the service to Windows Service Controller")]
    public class InstallCommand : CLICommand
    {
        [Option('p', "profile", Required = false, HelpText = "Service Account Profile")]
        public bool profile { get; set; }


        public override void Run(ServiceDescriptor descriptor, Win32Services svcs, Win32Service? svc)
        {
            if (!Program.elevated)
            {
                Elevate();
                return;
            }

            Program.Log.Info("Installing the service with id '" + descriptor.Id + "'");

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
            if (this.profile)
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

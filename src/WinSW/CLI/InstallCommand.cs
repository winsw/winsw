using CommandLine;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.ServiceProcess;
using System.Text;
using WinSW.Native;

namespace WinSW.CLI
{
    [Verb("install", HelpText = "install the service to Windows Service Controller")]
    public class InstallCommand : CliCommand
    {
        [Option('p', "profile", Required = false, HelpText = "Service Account Profile")]
        public bool profile { get; set; }


        public override void Run(ServiceDescriptor descriptor)
        {
            if (!Program.elevated)
            {
                Elevate();
                return;
            }

            Program.Log.Info("Installing the service with id '" + descriptor.Id + "'");

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

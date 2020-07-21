using CommandLine;
using System;
using System.ComponentModel;
using System.Diagnostics;
using winsw.Native;
using WMI;

namespace winsw.CLI
{
    public abstract class CLICommand
    {
        [Option("configFile", HelpText = "Configurations File")]
        public string ConfigFile { get; set; }

        [Option("elevated", HelpText = "Elevated Command Prompt", Default = false)]
        public bool Elevated { get; set; }

        [Option("skipConfigValidation", HelpText = "Enable configurations schema validation", Default = false)]
        public bool validation { get; set; }

        public abstract void Run(ServiceDescriptor descriptor, Win32Services svcs, Win32Service? svc);

        public void Elevate()
        {
            using Process current = Process.GetCurrentProcess();

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                Verb = "runas",
                FileName = current.MainModule.FileName,
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
                Program.Log.Fatal(e.Message);
                Environment.Exit(e.ErrorCode);
            }
        }
    }
}

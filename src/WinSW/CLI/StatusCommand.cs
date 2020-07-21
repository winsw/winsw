using CommandLine;
using System;
using System.ComponentModel;
using System.ServiceProcess;
using WinSW.Native;

namespace WinSW.CLI
{
    [Verb("status", HelpText = "check the current status of the service")]
    public class StatusCommand : CliCommand
    {
        public override void Run(ServiceDescriptor descriptor)
        {
            Program.Log.Debug("User requested the status of the process with id '" + descriptor.Id + "'");
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
    }
}

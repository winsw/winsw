using CommandLine;
using System;
using WMI;

namespace winsw.CLI
{
    [Verb("status", HelpText = "check the current status of the service")]
    public class StatusCommand : CLICommand
    {
        public override void Run(ServiceDescriptor descriptor, Win32Services svcs, Win32Service? svc)
        {
            Program.Log.Debug("User requested the status of the process with id '" + descriptor.Id + "'");
            Console.WriteLine(svc is null ? "NonExistent" : svc.Started ? "Started" : "Stopped");
        }
    }
}

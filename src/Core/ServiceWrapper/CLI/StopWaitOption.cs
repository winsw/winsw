using CommandLine;
using System.Threading;
using WMI;

namespace winsw.CLI
{
    [Verb("stopwait", HelpText = "stop the service and wait until it's actually stopped")]
    public class StopWaitOption : CliOption
    {
        public override void Run(ServiceDescriptor descriptor, Win32Services svcs, Win32Service? svc)
        {
            var Log = Program.Log;

            if (!Program.elevated)
            {
                Elevate();
                return;
            }

            Log.Info("Stopping the service with id '" + descriptor.Id + "'");
            if (svc is null)
            {
                Program.ThrowNoSuchService();
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
    }
}

using CommandLine;
using System.Threading;
using WMI;

namespace winsw.CLI
{
    [Verb("restart", HelpText = "restart the service")]
    public class RestartOption : CliOption
    {
        public override void Run(ServiceDescriptor descriptor, Win32Services svcs, Win32Service? svc)
        {
            var Log = Program.Log;

            if (!Program.elevated)
            {
                Elevate();
                return;
            }

            Log.Info("Restarting the service with id '" + descriptor.Id + "'");
            if (svc is null)
            {
                Program.ThrowNoSuchService();
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
    }
}

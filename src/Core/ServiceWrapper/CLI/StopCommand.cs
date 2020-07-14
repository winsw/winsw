using CommandLine;
using log4net;
using System.Threading;
using WMI;

namespace winsw.CLI
{
    [Verb("stop", HelpText = "stop the service")]
    public class StopCommand : CLICommand
    {
        [Option("wait", HelpText = "Stop Wait")]
        public bool Wait { get; set; }

        private ILog Log;

        public override void Run(ServiceDescriptor descriptor, Win32Services svcs, Win32Service? svc)
        {
            Log = Program.Log;

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

            if (this.Wait)
            {
                this.StopWait(descriptor, svcs, svc);
            }
            else
            {
                this.Stop(descriptor, svc);
            }
        }

        private void Stop(ServiceDescriptor descriptor, Win32Service? svc)
        {
            try
            {
                svc.StopService();
            }
            catch (WmiException e)
            {
                if (e.ErrorCode == ReturnValue.ServiceCannotAcceptControl)
                {
                    Log.Info($"The service with ID '{descriptor.Id}' is not running");
                }
                else
                {
                    throw;
                }
            }
        }

        private void StopWait(ServiceDescriptor descriptor, Win32Services svcs, Win32Service? svc)
        {
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

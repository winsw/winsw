using CommandLine;
using System;
using System.ComponentModel;
using System.ServiceProcess;
using WinSW.Native;

namespace WinSW.CLI
{
    [Verb("restart", HelpText = "restart the service")]
    public class RestartCommand : CliCommand
    {
        public override void Run(ServiceDescriptor descriptor)
        {
            if (!Program.elevated)
            {
                Elevate();
                return;
            }

            Program.Log.Info("Restarting the service with id '" + descriptor.Id + "'");

            using var svc = new ServiceController(descriptor.Id);

            try
            {
                svc.Stop();

                while (!ServiceControllerExtension.TryWaitForStatus(svc, ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(1)))
                {
                }
            }
            catch (InvalidOperationException e) when (e.InnerException is Win32Exception inner)
            {
                switch (inner.NativeErrorCode)
                {
                    case Errors.ERROR_SERVICE_DOES_NOT_EXIST:
                        Program.ThrowNoSuchService(inner);
                        break;

                    case Errors.ERROR_SERVICE_NOT_ACTIVE:
                        break;

                    default:
                        throw;

                }
            }

            svc.Start();
        }
    }
}

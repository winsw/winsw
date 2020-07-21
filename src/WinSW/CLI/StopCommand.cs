using CommandLine;
using System;
using System.ComponentModel;
using System.ServiceProcess;
using WinSW.Native;

namespace WinSW.CLI
{
    [Verb("stop", HelpText = "stop the service")]
    public class StopCommand : CliCommand
    {
        [Option("wait", HelpText = "Stop Wait", Default = false)]
        public bool Wait { get; set; }

        public override void Run(ServiceDescriptor descriptor)
        {
            if (!Program.elevated)
            {
                Elevate();
                return;
            }

            Program.Log.Info("Stopping the service with id '" + descriptor.Id + "'");

            if (this.Wait)
            {
                this.StopWait(descriptor);
            }
            else
            {
                this.Stop(descriptor);
            }
        }

        private void Stop(ServiceDescriptor descriptor)
        {
            using var svc = new ServiceController(descriptor.Id);

            try
            {
                svc.Stop();
            }
            catch (InvalidOperationException e) when (e.InnerException is Win32Exception inner)
            {
                switch (inner.NativeErrorCode)
                {
                    case Errors.ERROR_SERVICE_DOES_NOT_EXIST:
                        Program.ThrowNoSuchService(inner);
                        break;

                    case Errors.ERROR_SERVICE_NOT_ACTIVE:
                        Program.Log.Info($"The service with ID '{descriptor.Id}' is not running");
                        break;

                    default:
                        throw;

                }
            }
        }

        private void StopWait(ServiceDescriptor descriptor)
        {
            using var svc = new ServiceController(descriptor.Id);

            try
            {
                svc.Stop();

                while (!ServiceControllerExtension.TryWaitForStatus(svc, ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(1)))
                {
                    Program.Log.Info("Waiting the service to stop...");
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

            Program.Log.Info("The service stopped.");
        }
    }
}

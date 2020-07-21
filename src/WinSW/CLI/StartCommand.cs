using CommandLine;
using System;
using System.ComponentModel;
using System.ServiceProcess;
using WinSW.Native;

namespace WinSW.CLI
{
    [Verb("start", HelpText = "start the service (must be installed before)")]
    public class StartCommand : CliCommand
    {
        public override void Run(ServiceDescriptor descriptor)
        {
            if (!Program.elevated)
            {
                Elevate();
                return;
            }

            Program.Log.Info("Starting the service with id '" + descriptor.Id + "'");

            using var svc = new ServiceController(descriptor.Id);

            try
            {
                svc.Start();
            }
            catch (InvalidOperationException e) when (e.InnerException is Win32Exception inner)
            {
                switch (inner.NativeErrorCode)
                {
                    case Errors.ERROR_SERVICE_DOES_NOT_EXIST:
                        Program.ThrowNoSuchService(inner);
                        break;

                    case Errors.ERROR_SERVICE_ALREADY_RUNNING:
                        Program.Log.Info($"The service with ID '{descriptor.Id}' has already been started");
                        break;

                    default:
                        throw;
                }
            }
        }

    }
}

using CommandLine;
using System.ComponentModel;
using System.ServiceProcess;
using WinSW.Native;

namespace WinSW.CLI
{
    [Verb("uninstall", HelpText = "uninstall the service")]
    public class UninstallCommand : CliCommand
    {
        public override void Run(ServiceDescriptor descriptor)
        {
            if (!Program.elevated)
            {
                Elevate();
                return;
            }

            Program.Log.Info("Uninstalling the service with id '" + descriptor.Id + "'");

            using ServiceManager scm = ServiceManager.Open();
            try
            {
                using Service sc = scm.OpenService(descriptor.Id);

                if (sc.Status == ServiceControllerStatus.Running)
                {
                    // We could fail the opeartion here, but it would be an incompatible change.
                    // So it is just a warning
                    Program.Log.Warn("The service with id '" + descriptor.Id + "' is running. It may be impossible to uninstall it");
                }

                sc.Delete();
            }
            catch (CommandException e) when (e.InnerException is Win32Exception inner)
            {
                switch (inner.NativeErrorCode)
                {
                    case Errors.ERROR_SERVICE_DOES_NOT_EXIST:
                        Program.Log.Warn("The service with id '" + descriptor.Id + "' does not exist. Nothing to uninstall");
                        break; // there's no such service, so consider it already uninstalled

                    case Errors.ERROR_SERVICE_MARKED_FOR_DELETE:
                        Program.Log.Error("Failed to uninstall the service with id '" + descriptor.Id + "'"
                           + ". It has been marked for deletion.");

                        // TODO: change the default behavior to Error?
                        break; // it's already uninstalled, so consider it a success

                    default:
                        Program.Log.Fatal("Failed to uninstall the service with id '" + descriptor.Id + "'. Error code is '" + inner.NativeErrorCode + "'");
                        throw;
                }
            }
        }
    }
}

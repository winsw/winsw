using CommandLine;
using WMI;

namespace winsw.CLI
{
    [Verb("uninstall", HelpText = "uninstall the service")]
    public class UninstallOption : CliOption
    {
        public override void Run(ServiceDescriptor descriptor, Win32Services svcs, Win32Service? svc)
        {
            var Log = Program.Log;

            if (!Program.elevated)
            {
                Elevate();
                return;
            }

            Log.Info("Uninstalling the service with id '" + descriptor.Id + "'");
            if (svc is null)
            {
                Log.Warn("The service with id '" + descriptor.Id + "' does not exist. Nothing to uninstall");
                return; // there's no such service, so consider it already uninstalled
            }

            if (svc.Started)
            {
                // We could fail the opeartion here, but it would be an incompatible change.
                // So it is just a warning
                Log.Warn("The service with id '" + descriptor.Id + "' is running. It may be impossible to uninstall it");
            }

            try
            {
                svc.Delete();
            }
            catch (WmiException e)
            {
                if (e.ErrorCode == ReturnValue.ServiceMarkedForDeletion)
                {
                    Log.Error("Failed to uninstall the service with id '" + descriptor.Id + "'"
                       + ". It has been marked for deletion.");

                    // TODO: change the default behavior to Error?
                    return; // it's already uninstalled, so consider it a success
                }
                else
                {
                    Log.Fatal("Failed to uninstall the service with id '" + descriptor.Id + "'. WMI Error code is '" + e.ErrorCode + "'");
                }

                throw e;
            }
        }
    }
}

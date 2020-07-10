using CommandLine;
using WMI;

namespace winsw.CLI
{
    [Verb("start", HelpText = "start the service (must be installed before)")]
    public class StartOption : CliOption
    {
        public override void Run(ServiceDescriptor descriptor, Win32Services svcs, Win32Service? svc)
        {
            var Log = Program.Log;

            if (!Program.elevated)
            {
                Elevate();
                return;
            }

            Log.Info("Starting the service with id '" + descriptor.Id + "'");
            if (svc is null)
            {
                Program.ThrowNoSuchService();
            }

            try
            {
                svc.StartService();
            }
            catch (WmiException e)
            {
                if (e.ErrorCode == ReturnValue.ServiceAlreadyRunning)
                {
                    Log.Info($"The service with ID '{descriptor.Id}' has already been started");
                }
                else
                {
                    throw;
                }
            }
        }

    }
}

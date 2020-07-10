using CommandLine;
using WMI;

namespace winsw.CLI
{
    [Verb("stop", HelpText = "stop the service")]
    public class StopOption : CliOption
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
    }
}

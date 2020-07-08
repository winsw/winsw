using CommandLine;
using WMI;

namespace winsw.CLI
{
    [Verb("status", HelpText = "check the current status of the service")]
    public class StatusOption : CliOption
    {
        public override void Run(ServiceDescriptor descriptor, Win32Services svcs, Win32Service? svc)
        {
            throw new System.NotImplementedException();
        }
    }
}

using CommandLine;
using WMI;

namespace winsw.CLI
{
    [Verb("start", HelpText = "start the service (must be installed before)")]
    public class StartOption : CliOption
    {
        public override void Run(ServiceDescriptor descriptor, Win32Services svcs, Win32Service? svc)
        {
            throw new System.NotImplementedException();
        }
    }
}

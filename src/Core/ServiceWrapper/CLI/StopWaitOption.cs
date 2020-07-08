using CommandLine;
using WMI;

namespace winsw.CLI
{
    [Verb("stopwait", HelpText = "stop the service and wait until it's actually stopped")]
    public class StopWaitOption : CliOption
    {
        public override void Run(ServiceDescriptor descriptor, Win32Services svcs, Win32Service? svc)
        {
            throw new System.NotImplementedException();
        }
    }
}

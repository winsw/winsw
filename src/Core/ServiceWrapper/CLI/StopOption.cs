using CommandLine;
using WMI;

namespace winsw.CLI
{
    [Verb("stop", HelpText = "stop the service")]
    public class StopOption : CliOption
    {
        public override void Run(ServiceDescriptor descriptor, Win32Services svcs, Win32Service? svc)
        {
            throw new System.NotImplementedException();
        }
    }
}

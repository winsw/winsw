using CommandLine;
using WMI;

namespace winsw.CLI
{
    [Verb("restart", HelpText = "restart the service")]
    public class RestartOption : CliOption
    {
        public override void Run(ServiceDescriptor descriptor, Win32Services svcs, Win32Service? svc)
        {
            throw new System.NotImplementedException();
        }
    }
}

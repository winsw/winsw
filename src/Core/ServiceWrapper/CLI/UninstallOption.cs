using CommandLine;
using WMI;

namespace winsw.CLI
{
    [Verb("uninstall", HelpText = "uninstall the service")]
    public class UninstallOption : CliOption
    {
        public override void Run(ServiceDescriptor descriptor, Win32Services svcs, Win32Service? svc)
        {
            throw new System.NotImplementedException();
        }
    }
}

using CommandLine;
using WMI;

namespace winsw.CLI
{
    [Verb("default", isDefault: true)]
    public class DefaultCommand : CLICommand
    {
        public override void Run(ServiceDescriptor descriptor, Win32Services svcs, Win32Service? svc)
        {
            throw new System.NotImplementedException();
        }
    }
}

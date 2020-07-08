using CommandLine;
using WMI;

namespace winsw.CLI
{
    [Verb("test", HelpText = "check if the service can be started and then stopped")]
    public class TestOption : CliOption
    {
        public override void Run(ServiceDescriptor descriptor, Win32Services svcs, Win32Service? svc)
        {
            throw new System.NotImplementedException();
        }
    }
}

using CommandLine;
using WMI;

namespace winsw.CLI
{
    [Verb("testwait", HelpText = "starts the service and waits until a key is pressed then stops the service")]
    public class TestWaitOption : CliOption
    {
        public override void Run(ServiceDescriptor descriptor, Win32Services svcs, Win32Service? svc)
        {
            throw new System.NotImplementedException();
        }
    }
}

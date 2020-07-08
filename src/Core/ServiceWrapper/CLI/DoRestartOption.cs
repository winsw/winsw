using CommandLine;
using WMI;

namespace winsw.CLI
{
    [Verb("restart!", HelpText = "self-restart (can be called from child processes)")]
    public class DoRestartOption : CliOption
    {
        public override void Run(ServiceDescriptor descriptor, Win32Services svcs, Win32Service? svc)
        {
            throw new System.NotImplementedException();
        }
    }
}

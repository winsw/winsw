using CommandLine;

namespace WinSW.CLI
{
    [Verb("default", isDefault: true)]
    public class DefaultCommand : CliCommand
    {
        public override void Run(ServiceDescriptor descriptor)
        {
            throw new System.NotImplementedException();
        }
    }
}

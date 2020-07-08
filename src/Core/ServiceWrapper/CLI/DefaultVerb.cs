using CommandLine;
using winsw.Configuration;
using WMI;

namespace winsw.CLI
{
    [Verb("default", isDefault: true)]
    public class DefaultVerb : CliOption
    {
        public override void Run(ServiceDescriptor descriptor, Win32Services svcs, Win32Service? svc)
        {
            throw new System.NotImplementedException();
        }
    }
}

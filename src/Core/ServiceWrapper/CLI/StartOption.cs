using CommandLine;

namespace winsw.CLI
{
    [Verb("start", HelpText = "start the service (must be installed before)")]
    public class StartOption : CliOption
    {
    }
}

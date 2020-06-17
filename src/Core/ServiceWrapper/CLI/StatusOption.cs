using CommandLine;

namespace winsw.CLI
{
    [Verb("status", HelpText = "check the current status of the service")]
    public class StatusOption : CliOption
    {
    }
}

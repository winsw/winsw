using CommandLine;

namespace winsw.CLI
{
    [Verb("restart", HelpText = "restart the service")]
    public class RestartOption : CliOption
    {
    }
}

using CommandLine;

namespace winsw.CLI
{
    [Verb("stop", HelpText = "stop the service")]
    public class StopOption : CliOption
    {
    }
}

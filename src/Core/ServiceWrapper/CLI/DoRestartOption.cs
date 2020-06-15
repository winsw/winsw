using CommandLine;

namespace winsw.CLI
{
    [Verb("restart!", HelpText = "Force Restart")]
    public class DoRestartOption : CliOption
    {
    }
}

using CommandLine;

namespace winsw.CLI
{
    [Verb("restart!", HelpText = "self-restart (can be called from child processes)")]
    public class DoRestartOption : CliOption
    {
    }
}

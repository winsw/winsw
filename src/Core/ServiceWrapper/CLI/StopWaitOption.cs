using CommandLine;

namespace winsw.CLI
{
    [Verb("stopwait", HelpText = "stop the service and wait until it's actually stopped")]
    public class StopWaitOption : CliOption
    {
    }
}

using CommandLine;

namespace winsw.CLI
{
    [Verb("test", HelpText = "check if the service can be started and then stopped")]
    public class TestOption : CliOption
    {
    }
}

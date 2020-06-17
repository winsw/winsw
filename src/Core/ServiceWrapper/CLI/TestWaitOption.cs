using CommandLine;

namespace winsw.CLI
{
    [Verb("testwait", HelpText = "starts the service and waits until a key is pressed then stops the service")]
    public class TestWaitOption : CliOption
    {
    }
}

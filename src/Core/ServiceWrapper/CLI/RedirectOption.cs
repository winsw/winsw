using CommandLine;

namespace winsw.CLI
{
    [Verb("redirect", HelpText = "Redirect Target")]
    public class RedirectOption : CliOption
    {
        [Option("target", Required = true, HelpText = "Redirect Target")]
        public string redirectTarget { get; set; }
    }
}

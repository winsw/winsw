using CommandLine;

namespace winsw.CLI
{
    [Verb("install", HelpText = "Install Windows Service Wrapper")]
    public class InstallOption : CliOption
    {
        [Option('p', "profile", Required = false, HelpText = "Service Account Profile")]
        public bool profile { get; set; }
    }
}

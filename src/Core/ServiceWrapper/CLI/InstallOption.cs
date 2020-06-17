using CommandLine;

namespace winsw.CLI
{
    [Verb("install", HelpText = "install the service to Windows Service Controller")]
    public class InstallOption : CliOption
    {
        [Option('p', "profile", Required = false, HelpText = "Service Account Profile")]
        public bool profile { get; set; }
    }
}

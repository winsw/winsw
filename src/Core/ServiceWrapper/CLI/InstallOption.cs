using CommandLine;

namespace winsw.CLI
{
    [Verb("install", HelpText = "Install Windows Service Wrapper")]
    public class InstallOption : CliOption
    {
        [Option("configFile", Required = false, HelpText = "Specify the Configuration file")]
        public string configFile { get; set; }

        [Option("skipConfigValidation", Required = false, HelpText = "Enable configurations schema validation")]
        public bool validation { get; set; }

        [Option('p', "profile", Required = false, HelpText = "Service Account Profile")]
        public bool profile { get; set; }
    }
}

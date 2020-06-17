using CommandLine;

namespace winsw.CLI
{
    public class CliOption
    {
        [Option("configFile", HelpText = "Configurations File")]
        public string ConfigFile { get; set; }

        [Option("elevate", HelpText = "Elevate")]
        public bool Elevate { get; set; }

        [Option("redirect", HelpText = "Redirect Logs")]
        public string RedirectPath { get; set; }

        [Option("skipConfigValidation", Required = false, HelpText = "Enable configurations schema validation")]
        public bool validation { get; set; }
    }
}

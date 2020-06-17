using CommandLine;

namespace winsw.CLI
{
    public class CliOption
    {
        [Option("configFile", HelpText = "Configurations File")]
        public string ConfigFile { get; set; }

        [Option("elevated", HelpText = "Elevated Command Prompt", Default = false)]
        public bool Elevate { get; set; }

        [Option("redirect", HelpText = "Redirect Logs")]
        public string RedirectPath { get; set; }

        [Option("skipConfigValidation", HelpText = "Enable configurations schema validation", Default = false)]
        public bool validation { get; set; }
    }
}

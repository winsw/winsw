using CommandLine;

namespace winsw.CLI
{
    public class CliOption
    {
        [Option('c', "configFile", HelpText = "Configurations File")]
        public string? ConfigFile { get; set; }

        [Option('e', "elevated", HelpText = "Elevated Command Prompt", Default = false)]
        public bool Elevate { get; set; }

        [Option('r', "redirect", HelpText = "Redirect Logs")]
        public string? RedirectPath { get; set; }

        [Option('s', "skipConfigValidation", HelpText = "Enable configurations schema validation", Default = false)]
        public bool validation { get; set; }
    }
}

using System;
using System.IO;
using WinSW.Configuration;
using YamlDotNet.Serialization;

namespace WinSW
{
    public class ServiceDescriptorYaml
    {
        public readonly YamlConfiguration Configurations;

        public static DefaultWinSWSettings Defaults { get; } = new DefaultWinSWSettings();

        public ServiceDescriptorYaml(string baseName, DirectoryInfo d)
        {
            string basepath = Path.Combine(d.FullName, baseName);

            using (var reader = new StreamReader(basepath + ".yml"))
            {
                string file = reader.ReadToEnd();
                var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();

                this.Configurations = deserializer.Deserialize<YamlConfiguration>(file);
            }

            Environment.SetEnvironmentVariable("BASE", d.FullName);

            // ditto for ID
            Environment.SetEnvironmentVariable("SERVICE_ID", this.Configurations.Id);

            // New name
            Environment.SetEnvironmentVariable(WinSWSystem.EnvVarNameExecutablePath, Defaults.ExecutablePath);

            // Also inject system environment variables
            Environment.SetEnvironmentVariable(WinSWSystem.EnvVarNameServiceId, this.Configurations.Id);

            this.Configurations.LoadEnvironmentVariables();
        }

        public ServiceDescriptorYaml(YamlConfiguration configs)
        {
            this.Configurations = configs;
            this.Configurations.LoadEnvironmentVariables();
        }

        public static ServiceDescriptorYaml FromYaml(string yaml)
        {
            var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
            var configs = deserializer.Deserialize<YamlConfiguration>(yaml);
            return new ServiceDescriptorYaml(configs);
        }
    }
}

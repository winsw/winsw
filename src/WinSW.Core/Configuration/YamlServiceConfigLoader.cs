using System;
using System.IO;
using WinSW.Configuration;
using YamlDotNet.Serialization;

namespace WinSW
{
    public class YamlServiceConfigLoader
    {
        public readonly YamlServiceConfig Config;

        public YamlServiceConfigLoader(string baseName, string directory)
        {
            string basepath = Path.Combine(directory, baseName);

            using (var reader = new StreamReader(basepath + ".yml"))
            {
                string file = reader.ReadToEnd();
                var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();

                this.Config = deserializer.Deserialize<YamlServiceConfig>(file);
            }

            Environment.SetEnvironmentVariable("BASE", directory);

            // ditto for ID
            Environment.SetEnvironmentVariable("SERVICE_ID", this.Config.Name);

            // New name
            Environment.SetEnvironmentVariable(WinSWSystem.EnvVarNameExecutablePath, new DefaultSettings().ExecutablePath);

            // Also inject system environment variables
            Environment.SetEnvironmentVariable(WinSWSystem.EnvVarNameServiceId, this.Config.Name);

            this.Config.LoadEnvironmentVariables();
        }

        public YamlServiceConfigLoader(YamlServiceConfig configs)
        {
            this.Config = configs;
            this.Config.LoadEnvironmentVariables();
        }

        public static YamlServiceConfigLoader FromYaml(string yaml)
        {
            var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
            var configs = deserializer.Deserialize<YamlServiceConfig>(yaml);
            return new YamlServiceConfigLoader(configs);
        }
    }
}

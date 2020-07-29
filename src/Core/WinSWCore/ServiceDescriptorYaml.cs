using System;
using System.IO;
using WinSW.Configuration;
using YamlDotNet.Serialization;

namespace WinSW
{
    public class ServiceDescriptorYaml
    {
        public readonly YamlConfiguration Configurations = new YamlConfiguration();

        public static DefaultWinSWSettings Defaults { get; } = new DefaultWinSWSettings();

        public ServiceDescriptorYaml()
        {
            string p = Defaults.ExecutablePath;
            string baseName = Path.GetFileNameWithoutExtension(p);
            if (baseName.EndsWith(".vshost"))
            {
                baseName = baseName.Substring(0, baseName.Length - 7);
            }

            DirectoryInfo d = new DirectoryInfo(Path.GetDirectoryName(p));
            while (true)
            {
                if (File.Exists(Path.Combine(d.FullName, baseName + ".yml")))
                {
                    break;
                }

                if (d.Parent is null)
                {
                    throw new FileNotFoundException("Unable to locate " + baseName + ".yml file within executable directory or any parents");
                }

                d = d.Parent;
            }

            var basepath = Path.Combine(d.FullName, baseName);

            using (var reader = new StreamReader(basepath + ".yml"))
            {
                var file = reader.ReadToEnd();
                var deserializer = new DeserializerBuilder().Build();

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

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public ServiceDescriptorYaml(YamlConfiguration configs)
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        {
            this.Configurations = configs;
            this.Configurations.LoadEnvironmentVariables();
        }

        public static ServiceDescriptorYaml FromYaml(string yaml)
        {
            var deserializer = new DeserializerBuilder().Build();
            var configs = deserializer.Deserialize<YamlConfiguration>(yaml);
            return new ServiceDescriptorYaml(configs);
        }
    }
}

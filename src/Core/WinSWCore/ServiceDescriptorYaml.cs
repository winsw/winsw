using System;
using System.Collections.Generic;
using System.IO;
using winsw.Configuration;
using YamlDotNet.Serialization;

namespace winsw
{
    public class ServiceDescriptorYaml
    {
        public readonly YamlConfiguration configurations = new YamlConfiguration();

        public static DefaultWinSWSettings Defaults { get; } = new DefaultWinSWSettings();

        private readonly Dictionary<string, string> environmentVariables;

        public string BasePath { get; set; }

        public virtual string ExecutablePath => Defaults.ExecutablePath;

        public ServiceDescriptorYaml()
        {
            string p = ExecutablePath;
            string baseName = Path.GetFileNameWithoutExtension(p);
            if (baseName.EndsWith(".vshost"))
                baseName = baseName.Substring(0, baseName.Length - 7);

            DirectoryInfo d = new DirectoryInfo(Path.GetDirectoryName(p));
            while (true)
            {
                if (File.Exists(Path.Combine(d.FullName, baseName + ".yml")))
                    break;

                if (d.Parent is null)
                    throw new FileNotFoundException("Unable to locate " + baseName + ".yml file within executable directory or any parents");

                d = d.Parent;
            }

            BasePath = Path.Combine(d.FullName, baseName);

            using(var reader = new StreamReader(BasePath + ".yml"))
            {
                var file = reader.ReadToEnd();
                var deserializer = new DeserializerBuilder().Build();

                configurations = deserializer.Deserialize<YamlConfiguration>(file);
            }

            Environment.SetEnvironmentVariable("BASE", d.FullName);

            // ditto for ID
            Environment.SetEnvironmentVariable("SERVICE_ID", configurations.Id);

            // New name
            Environment.SetEnvironmentVariable(WinSWSystem.ENVVAR_NAME_EXECUTABLE_PATH, ExecutablePath);

            // Also inject system environment variables
            Environment.SetEnvironmentVariable(WinSWSystem.ENVVAR_NAME_SERVICE_ID, configurations.Id);

            this.environmentVariables = configurations.EnvironmentVariables;
        }


        public ServiceDescriptorYaml(YamlConfiguration configs)
        {
            configurations = configs;

            this.environmentVariables = configurations.EnvironmentVariables;
        }

        public static ServiceDescriptorYaml FromYaml(string yaml)
        {
            var deserializer = new DeserializerBuilder().Build();
            var configs = deserializer.Deserialize<YamlConfiguration>(yaml);
            return new ServiceDescriptorYaml(configs);
        }

    }
}

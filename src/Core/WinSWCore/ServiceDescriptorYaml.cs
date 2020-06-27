using System;
using System.IO;
using winsw.Configuration;
using YamlDotNet.Serialization;

namespace winsw
{
    public class ServiceDescriptorYaml
    {
        public readonly YamlConfiguration configurations;

        public static DefaultWinSWSettings Defaults { get; } = new DefaultWinSWSettings();

        public ServiceDescriptorYaml()
        {
            var baseName = Defaults.BaseName;
            var basePath = Defaults.BasePath;

            DirectoryInfo d = new DirectoryInfo(Path.GetDirectoryName(Defaults.ExecutablePath));
            while (true)
            {
                if (File.Exists(Path.Combine(d.FullName, baseName + ".yml")))
                    break;

                if (d.Parent is null)
                    throw new FileNotFoundException("Unable to locate " + baseName + ".yml file within executable directory or any parents");

                d = d.Parent;
            }

            using (var reader = new StreamReader(basePath + ".yml"))
            {
                var file = reader.ReadToEnd();
                var deserializer = new DeserializerBuilder().Build();

                configurations = deserializer.Deserialize<YamlConfiguration>(file);
            }

            Environment.SetEnvironmentVariable("BASE", d.FullName);

            // ditto for ID
            Environment.SetEnvironmentVariable("SERVICE_ID", configurations.Id);

            // New name
            Environment.SetEnvironmentVariable(WinSWSystem.ENVVAR_NAME_EXECUTABLE_PATH, Defaults.ExecutablePath);

            // Also inject system environment variables
            Environment.SetEnvironmentVariable(WinSWSystem.ENVVAR_NAME_SERVICE_ID, configurations.Id);

        }


        public ServiceDescriptorYaml(YamlConfiguration configs)
        {
            configurations = configs;
        }

        public static ServiceDescriptorYaml FromYaml(string yaml)
        {
            var deserializer = new DeserializerBuilder().Build();
            var configs = deserializer.Deserialize<YamlConfiguration>(yaml);
            return new ServiceDescriptorYaml(configs);
        }

    }
}

using System;
using System.Collections.Generic;
using System.IO;
using winsw.Configuration;
using winsw.Native;
using YamlDotNet.Serialization;

namespace winsw
{
    public class ServiceDescriptorYaml
    {
        public readonly YamlConfiguration configurations = new YamlConfiguration();

        public static DefaultWinSWSettings Defaults { get; } = new DefaultWinSWSettings();

        public string BasePath { get; set; }

        public string BaseName { get; set; }

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

            BaseName = baseName;
            BasePath = Path.Combine(d.FullName, BaseName);

            using(var reader = new StreamReader(BasePath + ".yml"))
            {
                var file = reader.ReadToEnd();
                var deserializer = new DeserializerBuilder().Build();

                configurations = deserializer.Deserialize<YamlConfiguration>(file);
            }
        }

        public ServiceDescriptorYaml(YamlConfiguration _configurations)
        {
            configurations = _configurations;
        }

        public static ServiceDescriptorYaml FromYaml(string yaml)
        {
            var deserializer = new DeserializerBuilder().Build();
            var configs = deserializer.Deserialize<YamlConfiguration>(yaml);
            return new ServiceDescriptorYaml(configs);
        }

        public string ID => configurations.Id;

        public string Caption => configurations.Caption;

        public string Description => configurations.Description;

        public string Executable => configurations.Executable;

        public bool HideWindow => configurations.HideWindow;

        public string? StopExecutable => configurations.StopExecutable != null ? configurations.StopExecutable : Defaults.StopExecutable;

        //TODO
        public string? Arguments => configurations.Arguments != null ? configurations.Arguments : Defaults.Arguments;

        //TODO
        public string? StartArguments;

        //TODO
        public string? StopArguments;

        public string WorkingDirectory => configurations.WorkingDirectory != null ? configurations.WorkingDirectory : Defaults.WorkingDirectory;









        public SC_ACTION[] FailureActions {
            get
            {
                var arr = new List<SC_ACTION>();

                foreach(var item in configurations.YamlFailureActions)
                {
                    arr.Add(new SC_ACTION(item.Type, item.Delay));
                }

                return arr.ToArray();
            }
        }
    }
}

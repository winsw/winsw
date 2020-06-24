using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using winsw.Configuration;
using winsw.Native;
using WMI;
using YamlDotNet.Serialization;

namespace winsw
{
    public class ServiceDescriptorYaml : IWinSWConfiguration
    {
        public readonly YamlConfiguration configurations = new YamlConfiguration();

        public static DefaultWinSWSettings Defaults { get; } = new DefaultWinSWSettings();

        private readonly Dictionary<string, string> environmentVariables;

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

            Environment.SetEnvironmentVariable("BASE", d.FullName);

            // ditto for ID
            Environment.SetEnvironmentVariable("SERVICE_ID", Id);

            // New name
            Environment.SetEnvironmentVariable(WinSWSystem.ENVVAR_NAME_EXECUTABLE_PATH, ExecutablePath);

            // Also inject system environment variables
            Environment.SetEnvironmentVariable(WinSWSystem.ENVVAR_NAME_SERVICE_ID, Id);

            this.environmentVariables = this.LoadEnvironmentVariables();
        }


        public ServiceDescriptorYaml(YamlConfiguration _configurations)
        {
            configurations = _configurations;

            this.environmentVariables = this.LoadEnvironmentVariables();
        }

        public static ServiceDescriptorYaml FromYaml(string yaml)
        {
            var deserializer = new DeserializerBuilder().Build();
            var configs = deserializer.Deserialize<YamlConfiguration>(yaml);
            return new ServiceDescriptorYaml(configs);
        }

        public string Id => configurations.Id;

        public string Caption => configurations.Caption;

        public string Description => configurations.Description;

        public string Executable => configurations.Executable;

        public bool HideWindow => configurations.HideWindow;

        public string? StopExecutable => configurations.StopExecutable != null ? configurations.StopExecutable : Defaults.StopExecutable;

        public bool DelayedAutoStart => configurations.DelayedAutoStart;

        public bool BeepOnShutdown => configurations.BeepOnShutdown;

        public TimeSpan WaitHint => configurations.WaitHint != TimeSpan.Zero ? configurations.WaitHint : Defaults.WaitHint;

        public TimeSpan SleepTime => configurations.SleepTime != TimeSpan.Zero ? configurations.SleepTime : Defaults.SleepTime;

        public bool Interactive => configurations.Interactive;

        public Dictionary<string, string> EnvironmentVariables => new Dictionary<string, string>(this.environmentVariables);

        public string WorkingDirectory => configurations.WorkingDirectory != null ? configurations.WorkingDirectory : Defaults.WorkingDirectory;

        public List<Download> Downloads => configurations.Downloads;

        public TimeSpan ResetFailureAfter => configurations.ResetFailureAfter != TimeSpan.Zero ? configurations.ResetFailureAfter : Defaults.ResetFailureAfter;

        //service account
        public bool AllowServiceAcountLogonRight
        {
            get
            {
                if(configurations.ServiceAccount.AllowServiceAcountLogonRight is null)
                {
                    return Defaults.AllowServiceAcountLogonRight;
                }

                return (bool)configurations.ServiceAccount.AllowServiceAcountLogonRight;
            }
        }

        protected internal string? ServiceAccountDomain => configurations.ServiceAccount.Domain;

        protected internal string? ServiceAccountName => configurations.ServiceAccount.Name;

        public string? ServiceAccountPassword => configurations.ServiceAccount.Password;

        public string? ServiceAccountUser => ServiceAccountName is null ? null : (ServiceAccountDomain ?? ".") + "\\" + ServiceAccountName;

        public bool HasServiceAccount()
        {
            return !(configurations.ServiceAccount is null);
        }


        public TimeSpan StopTimeout => configurations.StopTimeout;


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


        public string Arguments => GetArguments(configurations.Arguments, ArgType.arg);


        public string? StartArguments => GetArguments(configurations.StartArguments, ArgType.startarg);


        public string? StopArguments => GetArguments(configurations.StopArguments, ArgType.stoparg);

        
        private string GetArguments(string args, ArgType type)
        {

            if(args is null)
            {
                switch (type)
                {
                    case ArgType.arg:
                        return Defaults.Arguments;
                    case ArgType.startarg:
                        return Defaults.StartArguments;
                    case ArgType.stoparg:
                        return Defaults.StopArguments;
                }
            }

            string newArgs = Regex.Replace(args, @"\\n", " ");
            return newArgs;
        }

        private enum ArgType
        {
            arg = 0,
            startarg = 1,
            stoparg = 2
        }


        //TODO
        private Dictionary<string, string> LoadEnvironmentVariables()
        {
            throw new NotImplementedException();
        }


        public ProcessPriorityClass Priority => configurations.Priority;

        public bool StopParentProcessFirst => configurations.StopParentProcessFirst;

        public StartMode StartMode => configurations.StartMode;

        public string[] ServiceDependencies => configurations.ServiceDependencies;

        public string? LogDirectory => throw new NotImplementedException();

        public string LogMode => throw new NotImplementedException();

        public Log? Log => throw new NotImplementedException();

        public XmlNode? ExtensionsConfiguration => throw new NotImplementedException();

    }
}

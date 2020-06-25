using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml;
using winsw.Native;
using WMI;
using YamlDotNet.Serialization;

namespace winsw.Configuration
{
    public class YamlConfiguration : IWinSWConfiguration
    {

        public static DefaultWinSWSettings Defaults { get; } = new DefaultWinSWSettings();


        [YamlMember(Alias = "id")]
        public string Id { get; set; }

        [YamlMember(Alias = "name")]
        public string Name { get; set; }

        [YamlMember(Alias = "description")]
        public string Description { get; set; }

        [YamlMember(Alias = "executable")]
        public string Executable { get; set; }

        [YamlMember(Alias = "executablePath")]
        public string ExecutablePath { get; set; }

        [YamlMember(Alias = "hideWindow")]
        public bool HideWindow { get; set; }

        [YamlMember(Alias = "workingdirectory")]
        public string? WorkingDirectory { get; set; }

        [YamlMember(Alias = "serviceaccount")]
        public ServiceAccount? ServiceAccount { get; set; }
        string? IWinSWConfiguration.ServiceAccountPassword => ServiceAccount != null ? ServiceAccount.Password : null;
        string? IWinSWConfiguration.ServiceAccountUser => ServiceAccount != null ? ServiceAccount.User : null;
        bool IWinSWConfiguration.AllowServiceAcountLogonRight { get; }


        [YamlMember(Alias = "log")]
        public YAMLLog? _YAMLLog { get; set; }
        public Log? Log => _YAMLLog;

        string IWinSWConfiguration.LogDirectory => Log != null ? Log.Directory : null;
        string IWinSWConfiguration.LogMode => Log != null ? Log.Directory : null;

        
        [YamlMember(Alias = "download")]
        public List<Download> Downloads { get; set; }

        [YamlMember(Alias = "caption")]
        public string Caption { get; set; }

        

        [YamlMember(Alias = "arguments")]
        public string? Arguments
        {
            get => GetArguments(Arguments, ArgType.arg);
            set { }
        }

        [YamlMember(Alias = "startArguments")]
        public string? StartArguments {
            get => GetArguments(StartArguments, ArgType.startarg);
            set { } 
        }

        [YamlMember(Alias = "stopArguments")]
        public string? StopArguments {
            get => GetArguments(StopArguments, ArgType.stoparg);
            set { }
        }

        [YamlMember(Alias = "stopExecutable")]
        public string? StopExecutable { get; set; }

        

        [YamlMember(Alias = "stopParentProcessFirst")]
        public bool StopParentProcessFirst { get; set; }
        
        [YamlMember(Alias = "resetFailureAfter")]
        public TimeSpan ResetFailureAfter { get; set; }

        [YamlMember(Alias = "stopTimeout")]
        public TimeSpan StopTimeout { get; set; }
        
        [YamlMember(Alias = "startMode")]
        public StartMode StartMode { get; set; }

        [YamlMember(Alias = "serviceDependencies")]
        public string[] ServiceDependencies { get; set; }

        [YamlMember(Alias = "waitHint")]
        public TimeSpan WaitHint { get; set; }

        [YamlMember(Alias = "sleepTime")]
        public TimeSpan SleepTime { get; set; }

        [YamlMember(Alias = "interactive")]
        public bool Interactive { get; set; }

        [YamlMember(Alias = "priority")]
        public ProcessPriorityClass Priority { get; set; }

        [YamlMember(Alias = "beepOnShutdown")]
        public bool BeepOnShutdown { get; set; }

        [YamlMember(Alias = "environmentVariables")]
        public Dictionary<string, string> EnvironmentVariables { get; set; }

        [YamlMember(Alias = "failureActions")]
        public List<YAML_SC_ACTION> YamlFailureActions { get; set; }

        [YamlMember(Alias = "delayedAutoStart")]
        public bool DelayedAutoStart { get; set; }

        public class YAML_SC_ACTION
        {
            [YamlMember(Alias = "type")]
            private SC_ACTION_TYPE type;

            [YamlMember(Alias = "delay")]
            private TimeSpan delay;

            public SC_ACTION_TYPE Type { get => type; set => type = value; }
            public TimeSpan Delay { get => delay; set => delay = value; }
        }


        public SC_ACTION[] FailureActions
        {
            get
            {
                var arr = new List<SC_ACTION>();

                foreach (var item in YamlFailureActions)
                {
                    arr.Add(new SC_ACTION(item.Type, item.Delay));
                }

                return arr.ToArray();
            }
        }

        public bool HasServiceAccount()
        {
            return !(ServiceAccount is null);
        }

        private string GetArguments(string args, ArgType type)
        {

            if (args is null)
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


        public class YAMLLog : Log
        {
            [YamlMember(Alias = "mode")]
            public string? _Mode { get; set; }

            [YamlMember(Alias = "name")]
            public string? _Name { get; set; }

            [YamlMember(Alias = "directory")]
            public string? _Directory { get; set; }

            [YamlMember(Alias = "sizeThreshold")]
            public int? _SizeThreshold { get; set; }

            [YamlMember(Alias = "keepFiles")]
            public int? _KeepFiles { get; set; }

            [YamlMember(Alias = "pattern")]
            public string? _Pattern { get; set; }


            [YamlMember(Alias = "period")]
            public int? _Period { get; set; }


            // Filters
            [YamlMember(Alias = "outFileDisabled")]
            public bool _OutFileDisabled { get; set; }

            [YamlMember(Alias = "errFileDisabled")]
            public bool _ErrFileDisabled { get; set; }

            [YamlMember(Alias = "outFilePattern")]
            public string _OutFilePattern;

            [YamlMember(Alias = "errFilePattern")]
            public string _ErrFilePattern;


            // Zip options
            [YamlMember(Alias = "autoRollAtTime")]
            public string? _AutoRollAtTime { get; set; }

            [YamlMember(Alias = "zipOlderThanNumDays")]
            public int? _ZipOlderThanNumDays { get; set; }

            [YamlMember(Alias = "zipDateFormat")]
            public string? _ZipDateFormat { get; set; }

            public override string? Mode => _Mode;

            public override string? Name => _Name;

            public override string? Directory => _Directory;

            public override int? SizeThreshold => _SizeThreshold;

            public override int? KeepFiles => _KeepFiles;

            public override string? Pattern => _Pattern;

            public override int? Period => _Period;

            public override bool OutFileDisabled => _OutFileDisabled;

            public override bool ErrFileDisabled => _ErrFileDisabled;

            public override string OutFilePattern => _OutFilePattern;

            public override string ErrFilePattern => _ErrFilePattern;

            public override string? AutoRollAtTime => _AutoRollAtTime;

            public override int? ZipOlderThanNumDays => _ZipOlderThanNumDays;

            public override string? ZipDateFormat => _ZipDateFormat;
        }



        // TODO
        XmlNode? IWinSWConfiguration.ExtensionsConfiguration => throw new NotImplementedException();

    }
}

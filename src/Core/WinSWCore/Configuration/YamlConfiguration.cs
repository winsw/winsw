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
        public DefaultWinSWSettings Defaults { get; } = new DefaultWinSWSettings();

        [YamlMember(Alias = "id")]
        string _Id { get; set; }

        [YamlMember(Alias = "name")]
        public string Name { get; set; }

        [YamlMember(Alias = "description")]
        public string _Description { get; set; }

        [YamlMember(Alias = "executable")]
        public string _Executable { get; set; }

        [YamlMember(Alias = "executablePath")]
        public string _ExecutablePath { get; set; }

        [YamlMember(Alias = "caption")]
        public string _Caption { get; set; }

        [YamlMember(Alias = "hideWindow")]
        public bool HideWindow { get; set; }

        [YamlMember(Alias = "workingdirectory")]
        public string? _WorkingDirectory { get; set; }

        [YamlMember(Alias = "serviceaccount")]
        public ServiceAccount? ServiceAccount { get; set; }

        [YamlMember(Alias = "log")]
        public YAMLLog? _YAMLLog { get; set; }
        public Log? Log => _YAMLLog;

        [YamlMember(Alias = "download")]
        public List<Download> _Downloads { get; set; }

        [YamlMember(Alias = "arguments")]
        public string? _Arguments { get; set; }

        [YamlMember(Alias = "startArguments")]
        public string? _StartArguments { get; set; }

        [YamlMember(Alias = "stopArguments")]
        public string? _StopArguments { get; set; }

        [YamlMember(Alias = "stopExecutable")]
        public string? StopExecutable { get; set; }

        [YamlMember(Alias = "stopParentProcessFirst")]
        public bool StopParentProcessFirst { get; set; }
        
        [YamlMember(Alias = "resetFailureAfter")]
        public TimeSpan _ResetFailureAfter { get; set; }

        [YamlMember(Alias = "stopTimeout")]
        public TimeSpan _StopTimeout { get; set; }
        

        //[TODO] - set default value
        [YamlMember(Alias = "startMode")]
        public StartMode StartMode { get; set; }

        [YamlMember(Alias = "serviceDependencies")]
        public string[] _ServiceDependencies { get; set; }

        [YamlMember(Alias = "waitHint")]
        public TimeSpan _WaitHint { get; set; }

        [YamlMember(Alias = "sleepTime")]
        public TimeSpan _SleepTime { get; set; }

        [YamlMember(Alias = "interactive")]
        public bool Interactive { get; set; }

        [YamlMember(Alias = "priority")]
        public ProcessPriorityClass _Priority { get; set; }

        [YamlMember(Alias = "beepOnShutdown")]
        public bool BeepOnShutdown { get; set; }

        [YamlMember(Alias = "environmentVariables")]
        public Dictionary<string, string> _EnvironmentVariables { get; set; }

        [YamlMember(Alias = "failureActions")]
        public List<YAML_SC_ACTION> YamlFailureActions { get; set; }

        [YamlMember(Alias = "delayedAutoStart")]
        public bool DelayedAutoStart { get; set; }


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



        public class YAML_SC_ACTION
        {
            [YamlMember(Alias = "type")]
            private SC_ACTION_TYPE type;

            [YamlMember(Alias = "delay")]
            private TimeSpan delay;

            public SC_ACTION_TYPE Type { get => type; set => type = value; }
            public TimeSpan Delay { get => delay; set => delay = value; }
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


        public string Id => string.IsNullOrEmpty(_Id) ? Defaults.Id : _Id;

        public string Description => string.IsNullOrEmpty(_Description) ? Defaults.Description : _Description;

        public string Executable => string.IsNullOrEmpty(_Executable) ? Defaults.Executable : _Executable;
        
        public string ExecutablePath => string.IsNullOrEmpty(_ExecutablePath) ? Defaults.ExecutablePath : _ExecutablePath;

        public string Caption => string.IsNullOrEmpty(_Caption) ? Defaults.Caption : _Caption;

        public string Arguments => GetArguments(Arguments, ArgType.arg);

        public string? StartArguments => GetArguments(StartArguments, ArgType.startarg);

        public string? StopArguments => GetArguments(StopArguments, ArgType.stoparg);

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

        public TimeSpan ResetFailureAfter => _ResetFailureAfter.Equals(TimeSpan.Zero) ? 
            Defaults.ResetFailureAfter : 
            _ResetFailureAfter;

        public string WorkingDirectory => string.IsNullOrEmpty(_WorkingDirectory) ? 
            Defaults.WorkingDirectory : 
            _WorkingDirectory;

        public ProcessPriorityClass Priority => _Priority is 0 ? Defaults.Priority : _Priority;

        public TimeSpan StopTimeout => _StopTimeout.Equals(TimeSpan.Zero) ? Defaults.StopTimeout : _StopTimeout;

        public string[] ServiceDependencies => _ServiceDependencies is null ? 
            Defaults.ServiceDependencies : 
            _ServiceDependencies;

        public TimeSpan WaitHint => _WaitHint.Equals(TimeSpan.Zero) ? Defaults.WaitHint : _WaitHint;

        public TimeSpan SleepTime => _SleepTime.Equals(TimeSpan.Zero) ? Defaults.SleepTime : _SleepTime;

        public List<Download> Downloads => _Downloads is null ? Defaults.Downloads : _Downloads;

        public Dictionary<string, string> EnvironmentVariables => _EnvironmentVariables is null ? 
            Defaults.EnvironmentVariables :
            _EnvironmentVariables;

        //Service Account
        public string? ServiceAccountPassword => ServiceAccount != null ? ServiceAccount.Password : null;

        public string? ServiceAccountUser => ServiceAccount is null ? 
            null : 
            (ServiceAccount.Domain ?? ".") + "\\" + ServiceAccount.Name;

        
        public bool AllowServiceAcountLogonRight => ServiceAccount.AllowServiceAcountLogonRight is null ?
            Defaults.AllowServiceAcountLogonRight :
            (bool)ServiceAccount.AllowServiceAcountLogonRight;

        public bool HasServiceAccount()
        {
            return !(ServiceAccount is null);
        }


        //Log
        public string LogDirectory => Log != null ? Log.Directory : Defaults.LogDirectory;

        public string LogMode => Log != null ? Log.Mode : Defaults.LogMode;

        // TODO
        XmlNode? IWinSWConfiguration.ExtensionsConfiguration => throw new NotImplementedException();

    }
}

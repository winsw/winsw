using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        public string? _Id { get; set; }

        [YamlMember(Alias = "name")]
        public string? Name { get; set; }

        [YamlMember(Alias = "description")]
        public string? _Description { get; set; }

        [YamlMember(Alias = "executable")]
        public string? _Executable { get; set; }

        [YamlMember(Alias = "executablePath")]
        public string? _ExecutablePath { get; set; }

        [YamlMember(Alias = "caption")]
        public string? _Caption { get; set; }

        [YamlMember(Alias = "hideWindow")]
        public bool? _HideWindow { get; set; }

        [YamlMember(Alias = "workingdirectory")]
        public string? _WorkingDirectory { get; set; }

        [YamlMember(Alias = "serviceaccount")]
        public ServiceAccount? ServiceAccount { get; set; }

        [YamlMember(Alias = "log")]
        public YamlLog? _YAMLLog { get; set; }

        [YamlMember(Alias = "download")]
        public List<YamlDownload>? _Downloads { get; set; }

        [YamlMember(Alias = "arguments")]
        public string? _Arguments { get; set; }

        [YamlMember(Alias = "startArguments")]
        public string? _StartArguments { get; set; }

        [YamlMember(Alias = "stopArguments")]
        public string? _StopArguments { get; set; }

        [YamlMember(Alias = "stopExecutable")]
        public string? _StopExecutable { get; set; }

        [YamlMember(Alias = "stopParentProcessFirst")]
        public bool? _StopParentProcessFirst { get; set; }

        [YamlMember(Alias = "resetFailureAfter")]
        public TimeSpan? _ResetFailureAfter { get; set; }

        [YamlMember(Alias = "stopTimeout")]
        public TimeSpan? _StopTimeout { get; set; }

        [YamlMember(Alias = "startMode")]
        public StartMode? _StartMode { get; set; }

        [YamlMember(Alias = "serviceDependencies")]
        public string[]? _ServiceDependencies { get; set; }

        [YamlMember(Alias = "waitHint")]
        public TimeSpan? _WaitHint { get; set; }

        [YamlMember(Alias = "sleepTime")]
        public TimeSpan? _SleepTime { get; set; }

        [YamlMember(Alias = "interactive")]
        public bool? _Interactive { get; set; }

        [YamlMember(Alias = "priority")]
        public ProcessPriorityClass? _Priority { get; set; }

        [YamlMember(Alias = "beepOnShutdown")]
        public bool BeepOnShutdown { get; set; }

        [YamlMember(Alias = "env")]
        public Dictionary<string, string>? _EnvironmentVariables { get; set; }

        [YamlMember(Alias = "failureActions")]
        public List<YamlFailureAction>? YamlFailureActions { get; set; }

        [YamlMember(Alias = "delayedAutoStart")]
        public bool DelayedAutoStart { get; set; }

        [YamlMember(Alias = "securityDescriptor")]
        public string? _SecurityDescriptor { get; set; }

        public class YamlLog : Log
        {

            private readonly YamlConfiguration configs;

            public YamlLog()
            {
                configs = new YamlConfiguration();
            }

            [YamlMember(Alias = "mode")]
            public string? _Mode { get; set; }

            [YamlMember(Alias = "name")]
            public string? _Name { get; set; }

            [YamlMember(Alias = "sizeThreshold")]
            public int? _SizeThreshold { get; set; }

            [YamlMember(Alias = "keepFiles")]
            public int? _KeepFiles { get; set; }

            [YamlMember(Alias = "pattern")]
            public string? _Pattern { get; set; }

            [YamlMember(Alias = "period")]
            public int? _Period { get; set; }

            [YamlMember(Alias = "logpath")]
            public string? _LogPath { get; set; }


            // Filters
            [YamlMember(Alias = "outFileDisabled")]
            public bool? _OutFileDisabled { get; set; }

            [YamlMember(Alias = "errFileDisabled")]
            public bool? _ErrFileDisabled { get; set; }

            [YamlMember(Alias = "outFilePattern")]
            public string? _OutFilePattern;

            [YamlMember(Alias = "errFilePattern")]
            public string? _ErrFilePattern;


            // Zip options
            [YamlMember(Alias = "autoRollAtTime")]
            public string? _AutoRollAtTime { get; set; }

            [YamlMember(Alias = "zipOlderThanNumDays")]
            public int? _ZipOlderThanNumDays { get; set; }

            [YamlMember(Alias = "zipDateFormat")]
            public string? _ZipDateFormat { get; set; }

            public override string Mode => _Mode is null ?
                DefaultWinSWSettings.DefaultLogSettings.Mode :
                _Mode;

            public override string? Name => _Name is null ?
                DefaultWinSWSettings.DefaultLogSettings.Name :
                Environment.ExpandEnvironmentVariables(_Name);

            public override string Directory => _LogPath is null ?
                DefaultWinSWSettings.DefaultLogSettings.Directory :
                Environment.ExpandEnvironmentVariables(_LogPath);

            public override int? SizeThreshold => _SizeThreshold is null ?
                DefaultWinSWSettings.DefaultLogSettings.SizeThreshold :
                _SizeThreshold * RollingSizeTimeLogAppender.BYTES_PER_KB;

            public override int? KeepFiles => _KeepFiles is null ?
                DefaultWinSWSettings.DefaultLogSettings.KeepFiles :
                _KeepFiles;


            public override string? Pattern
            {
                get
                {
                    if (_Pattern != null)
                    {
                        return _Pattern;
                    }

                    return DefaultWinSWSettings.DefaultLogSettings.Pattern;
                }
            }

            public override int? Period => _Period is null ? 1 : _Period;

            public override bool OutFileDisabled => _OutFileDisabled is null ?
                DefaultWinSWSettings.DefaultLogSettings.OutFileDisabled :
                (bool)_OutFileDisabled;

            public override bool ErrFileDisabled => _ErrFileDisabled is null ?
                configs.Defaults.ErrFileDisabled :
                (bool)_ErrFileDisabled;

            public override string OutFilePattern => _OutFilePattern is null ?
                DefaultWinSWSettings.DefaultLogSettings.OutFilePattern :
                Environment.ExpandEnvironmentVariables(_OutFilePattern);

            public override string ErrFilePattern => _ErrFilePattern is null ?
                DefaultWinSWSettings.DefaultLogSettings.ErrFilePattern :
                Environment.ExpandEnvironmentVariables(_ErrFilePattern);

            public override string? AutoRollAtTime => _AutoRollAtTime is null ?
                DefaultWinSWSettings.DefaultLogSettings.AutoRollAtTime :
                _AutoRollAtTime;

            public override int? ZipOlderThanNumDays
            {
                get
                {
                    if (_ZipOlderThanNumDays != null)
                    {
                        return _ZipOlderThanNumDays;
                    }

                    return DefaultWinSWSettings.DefaultLogSettings.ZipOlderThanNumDays;
                }
            }

            public override string? ZipDateFormat => _ZipDateFormat is null ?
                DefaultWinSWSettings.DefaultLogSettings.ZipDateFormat :
                _ZipDateFormat;
        }

        public class YamlDownload : Download
        {
            [YamlMember(Alias = "from")]
            public string _From;

            [YamlMember(Alias = "to")]
            public string _To;

            [YamlMember(Alias = "auth")]
            public AuthType _Auth;

            [YamlMember(Alias = "username")]
            public string? _Username;

            [YamlMember(Alias = "password")]
            public string? _Password;

            [YamlMember(Alias = "unsecureAuth")]
            public bool _UnsecureAuth;

            [YamlMember(Alias = "failOnError")]
            public bool _FailOnError;

            [YamlMember(Alias = "proxy")]
            public string? _Proxy;

        }


        public class YamlFailureAction
        {
            [YamlMember(Alias = "type")]
            private SC_ACTION_TYPE type;

            [YamlMember(Alias = "delay")]
            private TimeSpan delay;

            public SC_ACTION_TYPE Type { get => type; set => type = value; }
            public TimeSpan Delay { get => delay; set => delay = value; }
        }


        private string? GetArguments(string? args, ArgType type)
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

            return Environment.ExpandEnvironmentVariables(args);
        }

        private enum ArgType
        {
            arg = 0,
            startarg = 1,
            stoparg = 2
        }

        private List<Download> GetDownloads(List<YamlDownload>? downloads)
        {
            if (downloads is null)
            {
                return Defaults.Downloads;
            }

            var result = new List<Download>(downloads.Count);

            foreach (var item in downloads)
            {
                result.Add(new Download(
                    item._From,
                    item._To,
                    item._FailOnError,
                    item._Auth,
                    item._Username,
                    item._Password,
                    item._UnsecureAuth,
                    item._Proxy));
            }

            return result;
        }


        public string Id => _Id is null ? Defaults.Id : _Id;

        public string Description => _Description is null ? Defaults.Description : _Description;

        public string Executable => _Executable is null ? Defaults.Executable : _Executable;

        public string ExecutablePath => _ExecutablePath is null ? Defaults.ExecutablePath : _ExecutablePath;

        public string Caption => _Caption is null ? Defaults.Caption : _Caption;

        public bool HideWindow => _HideWindow is null ? Defaults.HideWindow : (bool)_HideWindow;

        public bool StopParentProcessFirst => _StopParentProcessFirst is null ?
            Defaults.StopParentProcessFirst :
            (bool)_StopParentProcessFirst;

        public StartMode StartMode => _StartMode is null ? Defaults.StartMode : (StartMode)_StartMode;

        public string Arguments => GetArguments(_Arguments, ArgType.arg);

        public string? StartArguments => GetArguments(_StartArguments, ArgType.startarg);

        public string? StopArguments => GetArguments(_StopArguments, ArgType.stoparg);

        public string? StopExecutable => _StopExecutable is null ?
            Defaults.StopExecutable :
            null;

        public SC_ACTION[] FailureActions
        {
            get
            {
                if (YamlFailureActions is null)
                {
                    return new SC_ACTION[0];
                }

                var arr = new List<SC_ACTION>();

                foreach (var item in YamlFailureActions)
                {
                    arr.Add(new SC_ACTION(item.Type, item.Delay));
                }

                return arr.ToArray();
            }
        }

        public TimeSpan ResetFailureAfter => _ResetFailureAfter is null ?
            Defaults.ResetFailureAfter :
            (TimeSpan)_ResetFailureAfter;

        public string WorkingDirectory => _WorkingDirectory is null ?
            Defaults.WorkingDirectory :
            _WorkingDirectory;

        public ProcessPriorityClass Priority => _Priority is null ? Defaults.Priority : (ProcessPriorityClass)_Priority;

        public TimeSpan StopTimeout => _StopTimeout is null ? Defaults.StopTimeout : (TimeSpan)_StopTimeout;

        public string[] ServiceDependencies => _ServiceDependencies is null ?
            Defaults.ServiceDependencies :
            _ServiceDependencies;

        public TimeSpan WaitHint => _WaitHint is null ? Defaults.WaitHint : (TimeSpan)_WaitHint;

        public TimeSpan SleepTime => _SleepTime is null ? Defaults.SleepTime : (TimeSpan)_SleepTime;

        public bool Interactive => _Interactive is null ? Defaults.Interactive : (bool)_Interactive;

        public List<Download> Downloads => GetDownloads(_Downloads);

        public Dictionary<string, string> EnvironmentVariables
        {
            get
            {
                if (_EnvironmentVariables is null)
                {
                    return Defaults.EnvironmentVariables;
                }

                var dictionary = new Dictionary<string, string>();
                foreach (var item in _EnvironmentVariables)
                {
                    dictionary[item.Key] = Environment.ExpandEnvironmentVariables(item.Value);
                }

                return dictionary;
            }
        }


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
        public Log Log => _YAMLLog is null ? (Log)DefaultWinSWSettings.DefaultLogSettings : _YAMLLog;

        public string LogDirectory => Log.Directory;

        public string LogMode => Log.Mode;

        // TODO
        XmlNode? IWinSWConfiguration.ExtensionsConfiguration => throw new NotImplementedException();

    }
}

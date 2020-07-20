using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml;
using WinSW.Native;
using WMI;
using YamlDotNet.Serialization;
using static WinSW.Download;

namespace WinSW.Configuration
{
    public class YamlConfiguration : IWinSWConfiguration
    {
        public DefaultWinSWSettings Defaults { get; } = new DefaultWinSWSettings();

        [YamlMember(Alias = "id")]
        public string? IdYaml { get; set; }

        [YamlMember(Alias = "name")]
        public string? NameYaml { get; set; }

        [YamlMember(Alias = "description")]
        public string? DescriptionYaml { get; set; }

        [YamlMember(Alias = "executable")]
        public string? ExecutableYaml { get; set; }

        [YamlMember(Alias = "executablePath")]
        public string? ExecutablePathYaml { get; set; }

        [YamlMember(Alias = "caption")]
        public string? CaptionYaml { get; set; }

        [YamlMember(Alias = "hideWindow")]
        public bool? HideWindowYaml { get; set; }

        [YamlMember(Alias = "workingdirectory")]
        public string? WorkingDirectoryYaml { get; set; }

        [YamlMember(Alias = "serviceaccount")]
        public ServiceAccount? ServiceAccountYaml { get; set; }

        [YamlMember(Alias = "log")]
        public YamlLog? YAMLLog { get; set; }

        [YamlMember(Alias = "download")]
        public List<YamlDownload>? DownloadsYaml { get; set; }

        [YamlMember(Alias = "arguments")]
        public string? ArgumentsYaml { get; set; }

        [YamlMember(Alias = "startArguments")]
        public string? StartArgumentsYaml { get; set; }

        [YamlMember(Alias = "stopArguments")]
        public string? StopArgumentsYaml { get; set; }

        [YamlMember(Alias = "stopExecutable")]
        public string? StopExecutableYaml { get; set; }

        [YamlMember(Alias = "stopParentProcessFirst")]
        public bool? StopParentProcessFirstYaml { get; set; }

        [YamlMember(Alias = "resetFailureAfter")]
        public TimeSpan? ResetFailureAfterYaml { get; set; }

        [YamlMember(Alias = "stopTimeout")]
        public TimeSpan? StopTimeoutYaml { get; set; }

        [YamlMember(Alias = "startMode")]
        public StartMode? StartModeYaml { get; set; }

        [YamlMember(Alias = "serviceDependencies")]
        public string[]? ServiceDependenciesYaml { get; set; }

        [YamlMember(Alias = "waitHint")]
        public TimeSpan? WaitHintYaml { get; set; }

        [YamlMember(Alias = "sleepTime")]
        public TimeSpan? SleepTimeYaml { get; set; }

        [YamlMember(Alias = "interactive")]
        public bool? InteractiveYaml { get; set; }

        [YamlMember(Alias = "priority")]
        public ProcessPriorityClass? PriorityYaml { get; set; }

        [YamlMember(Alias = "beepOnShutdown")]
        public bool BeepOnShutdown { get; set; }

        [YamlMember(Alias = "env")]
        public Dictionary<string, string>? EnvironmentVariablesYaml { get; set; }

        [YamlMember(Alias = "failureActions")]
        public List<YamlFailureAction>? YamlFailureActions { get; set; }

        [YamlMember(Alias = "delayedAutoStart")]
        public bool DelayedAutoStart { get; set; }

        [YamlMember(Alias = "securityDescriptor")]
        public string? SecurityDescriptorYaml { get; set; }

        public class YamlLog : Log
        {
            private readonly YamlConfiguration configs;

            public YamlLog()
            {
                this.configs = new YamlConfiguration();
            }

            [YamlMember(Alias = "mode")]
            public string? ModeYamlLog { get; set; }

            [YamlMember(Alias = "name")]
            public string? NameYamlLog { get; set; }

            [YamlMember(Alias = "sizeThreshold")]
            public int? SizeThresholdYamlLog { get; set; }

            [YamlMember(Alias = "keepFiles")]
            public int? KeepFilesYamlLog { get; set; }

            [YamlMember(Alias = "pattern")]
            public string? PatternYamlLog { get; set; }

            [YamlMember(Alias = "period")]
            public int? PeriodYamlLog { get; set; }

            [YamlMember(Alias = "logpath")]
            public string? LogPathYamlLog { get; set; }

            // Filters
            [YamlMember(Alias = "outFileDisabled")]
            public bool? OutFileDisabledYamlLog { get; set; }

            [YamlMember(Alias = "errFileDisabled")]
            public bool? ErrFileDisabledYamlLog { get; set; }

            [YamlMember(Alias = "outFilePattern")]
            public string? OutFilePatternYamlLog { get; set; }

            [YamlMember(Alias = "errFilePattern")]
            public string? ErrFilePatternYamlLog { get; set; }

            // Zip options
            [YamlMember(Alias = "autoRollAtTime")]
            public string? AutoRollAtTimeYamlLog { get; set; }

            [YamlMember(Alias = "zipOlderThanNumDays")]
            public int? ZipOlderThanNumDaysYamlLog { get; set; }

            [YamlMember(Alias = "zipDateFormat")]
            public string? ZipDateFormatYamlLog { get; set; }

            public override string Mode => this.ModeYamlLog is null ?
                DefaultWinSWSettings.DefaultLogSettings.Mode :
                this.ModeYamlLog;

            public override string Name
            {
                get
                {
                    return this.NameYamlLog is null ?
                        DefaultWinSWSettings.DefaultLogSettings.Name :
                        Environment.ExpandEnvironmentVariables(this.NameYamlLog);
                }
            }

            public override string Directory
            {
                get
                {
                    return this.LogPathYamlLog is null ?
                        DefaultWinSWSettings.DefaultLogSettings.Directory :
                        Environment.ExpandEnvironmentVariables(this.LogPathYamlLog);
                }
            }

            public override int? SizeThreshold
            {
                get
                {
                    return this.SizeThresholdYamlLog is null ?
                        DefaultWinSWSettings.DefaultLogSettings.SizeThreshold :
                        this.SizeThresholdYamlLog * RollingSizeTimeLogAppender.BytesPerKB;
                }
            }

            public override int? KeepFiles
            {
                get
                {
                    return this.KeepFilesYamlLog is null ?
                        DefaultWinSWSettings.DefaultLogSettings.KeepFiles :
                        this.KeepFilesYamlLog;
                }
            }

            public override string Pattern
            {
                get
                {
                    if (this.PatternYamlLog != null)
                    {
                        return this.PatternYamlLog;
                    }

                    return DefaultWinSWSettings.DefaultLogSettings.Pattern;
                }
            }

            public override int? Period => this.PeriodYamlLog is null ? 1 : this.PeriodYamlLog;

            public override bool OutFileDisabled
            {
                get
                {
                    return this.OutFileDisabledYamlLog is null ?
                        DefaultWinSWSettings.DefaultLogSettings.OutFileDisabled :
                        (bool)this.OutFileDisabledYamlLog;
                }
            }

            public override bool ErrFileDisabled
            {
                get
                {
                    return this.ErrFileDisabledYamlLog is null ?
                        this.configs.Defaults.ErrFileDisabled :
                        (bool)this.ErrFileDisabledYamlLog;
                }
            }

            public override string OutFilePattern
            {
                get
                {
                    return this.OutFilePatternYamlLog is null ?
                        DefaultWinSWSettings.DefaultLogSettings.OutFilePattern :
                        Environment.ExpandEnvironmentVariables(this.OutFilePatternYamlLog);
                }
            }

            public override string ErrFilePattern
            {
                get
                {
                    return this.ErrFilePatternYamlLog is null ?
                        DefaultWinSWSettings.DefaultLogSettings.ErrFilePattern :
                        Environment.ExpandEnvironmentVariables(this.ErrFilePatternYamlLog);
                }
            }

            public override string? AutoRollAtTime
            {
                get
                {
                    return this.AutoRollAtTimeYamlLog is null ?
                        DefaultWinSWSettings.DefaultLogSettings.AutoRollAtTime :
                        this.AutoRollAtTimeYamlLog;
                }
            }

            public override int? ZipOlderThanNumDays
            {
                get
                {
                    if (this.ZipOlderThanNumDaysYamlLog != null)
                    {
                        return this.ZipOlderThanNumDaysYamlLog;
                    }

                    return DefaultWinSWSettings.DefaultLogSettings.ZipOlderThanNumDays;
                }
            }

            public override string? ZipDateFormat
            {
                get
                {
                    return this.ZipDateFormatYamlLog is null ?
                        DefaultWinSWSettings.DefaultLogSettings.ZipDateFormat :
                        this.ZipDateFormatYamlLog;
                }
            }
        }

        public class YamlDownload
        {
            [YamlMember(Alias = "from")]
            public string FromYamlDownload { get; set; } = string.Empty;

            [YamlMember(Alias = "to")]
            public string ToYamlDownload { get; set; } = string.Empty;

            [YamlMember(Alias = "auth")]
            public AuthType AuthYamlDownload { get; set; }

            [YamlMember(Alias = "username")]
            public string? UsernameYamlDownload { get; set; }

            [YamlMember(Alias = "password")]
            public string? PasswordYamlDownload { get; set; }

            [YamlMember(Alias = "unsecureAuth")]
            public bool UnsecureAuthYamlDownload { get; set; }

            [YamlMember(Alias = "failOnError")]
            public bool FailOnErrorYamlDownload { get; set; }

            [YamlMember(Alias = "proxy")]
            public string? ProxyYamlDownload { get; set; }
        }

        public class YamlFailureAction
        {
            [YamlMember(Alias = "type")]
            private SC_ACTION_TYPE type;

            [YamlMember(Alias = "delay")]
            private TimeSpan delay;

            public SC_ACTION_TYPE Type { get => this.type; set => this.type = value; }

            public TimeSpan Delay { get => this.delay; set => this.delay = value; }
        }

        private string? GetArguments(string? args, ArgType type)
        {
            if (args is null)
            {
                switch (type)
                {
                    case ArgType.Arg:
                        return this.Defaults.Arguments;
                    case ArgType.Startarg:
                        return this.Defaults.StartArguments;
                    case ArgType.Stoparg:
                        return this.Defaults.StopArguments;
                    default:
                        return string.Empty;
                }
            }

            return Environment.ExpandEnvironmentVariables(args);
        }

        private enum ArgType
        {
            Arg = 0,
            Startarg = 1,
            Stoparg = 2
        }

        private List<Download> GetDownloads(List<YamlDownload>? downloads)
        {
            if (downloads is null)
            {
                return this.Defaults.Downloads;
            }

            var result = new List<Download>(downloads.Count);

            foreach (var item in downloads)
            {
                result.Add(new Download(
                    item.FromYamlDownload,
                    item.ToYamlDownload,
                    item.FailOnErrorYamlDownload,
                    item.AuthYamlDownload,
                    item.UsernameYamlDownload,
                    item.PasswordYamlDownload,
                    item.UnsecureAuthYamlDownload,
                    item.ProxyYamlDownload));
            }

            return result;
        }

        public string Id => this.IdYaml is null ? this.Defaults.Id : this.IdYaml;

        public string Description => this.DescriptionYaml is null ? this.Defaults.Description : this.DescriptionYaml;

        public string Executable => this.ExecutableYaml is null ? this.Defaults.Executable : this.ExecutableYaml;

        public string ExecutablePath => this.ExecutablePathYaml is null ? this.Defaults.ExecutablePath : this.ExecutablePathYaml;

        public string Caption => this.CaptionYaml is null ? this.Defaults.Caption : this.CaptionYaml;

        public bool HideWindow => this.HideWindowYaml is null ? this.Defaults.HideWindow : (bool)this.HideWindowYaml;

        public bool StopParentProcessFirst
        {
            get
            {
                return this.StopParentProcessFirstYaml is null ?
                    this.Defaults.StopParentProcessFirst :
                    (bool)this.StopParentProcessFirstYaml;
            }
        }

        public StartMode StartMode => this.StartModeYaml is null ? this.Defaults.StartMode : (StartMode)this.StartModeYaml;

        public string Arguments
        {
            get
            {
                var args = this.GetArguments(this.ArgumentsYaml, ArgType.Arg);
                return args is null ? this.Defaults.Arguments : args;
            }
        }

        public string? StartArguments => this.GetArguments(this.StartArgumentsYaml, ArgType.Startarg);

        public string? StopArguments => this.GetArguments(this.StopArgumentsYaml, ArgType.Stoparg);

        public string? StopExecutable
        {
            get
            {
                return this.StopExecutableYaml is null ?
                    this.Defaults.StopExecutable :
                    null;
            }
        }

        public SC_ACTION[] FailureActions
        {
            get
            {
                if (this.YamlFailureActions is null)
                {
                    return new SC_ACTION[0];
                }

                var arr = new List<SC_ACTION>();

                foreach (var item in this.YamlFailureActions)
                {
                    arr.Add(new SC_ACTION(item.Type, item.Delay));
                }

                return arr.ToArray();
            }
        }

        public TimeSpan ResetFailureAfter => this.ResetFailureAfterYaml is null ?
            this.Defaults.ResetFailureAfter :
            (TimeSpan)this.ResetFailureAfterYaml;

        public string WorkingDirectory => this.WorkingDirectoryYaml is null ?
            this.Defaults.WorkingDirectory :
            this.WorkingDirectoryYaml;

        public ProcessPriorityClass Priority => this.PriorityYaml is null ? this.Defaults.Priority : (ProcessPriorityClass)this.PriorityYaml;

        public TimeSpan StopTimeout => this.StopTimeoutYaml is null ? this.Defaults.StopTimeout : (TimeSpan)this.StopTimeoutYaml;

        public string[] ServiceDependencies => this.ServiceDependenciesYaml is null ?
            this.Defaults.ServiceDependencies :
            this.ServiceDependenciesYaml;

        public TimeSpan WaitHint => this.WaitHintYaml is null ? this.Defaults.WaitHint : (TimeSpan)this.WaitHintYaml;

        public TimeSpan SleepTime => this.SleepTimeYaml is null ? this.Defaults.SleepTime : (TimeSpan)this.SleepTimeYaml;

        public bool Interactive => this.InteractiveYaml is null ? this.Defaults.Interactive : (bool)this.InteractiveYaml;

        public List<Download> Downloads => this.GetDownloads(this.DownloadsYaml);

        public Dictionary<string, string> EnvironmentVariables
        {
            get
            {
                if (this.EnvironmentVariablesYaml is null)
                {
                    return this.Defaults.EnvironmentVariables;
                }

                var dictionary = new Dictionary<string, string>();
                foreach (var item in this.EnvironmentVariablesYaml)
                {
                    dictionary[item.Key] = Environment.ExpandEnvironmentVariables(item.Value);
                }

                return dictionary;
            }
        }

        public ServiceAccount ServiceAccount => this.ServiceAccountYaml is null ? this.Defaults.ServiceAccount : this.ServiceAccountYaml;

        public Log Log => this.YAMLLog is null ? this.Defaults.Log : this.YAMLLog;

        public string LogDirectory => this.Log.Directory;

        public string LogMode => this.Log.Mode is null ? this.Defaults.LogMode : this.Log.Mode;

        // TODO
        XmlNode? IWinSWConfiguration.ExtensionsConfiguration => throw new NotImplementedException();

        public List<string> ExtensionIds => throw new NotImplementedException();

        public string BaseName => throw new NotImplementedException();

        public string BasePath => throw new NotImplementedException();

        public string? ServiceAccountDomain => throw new NotImplementedException();

        public string? ServiceAccountName => throw new NotImplementedException();

        public string? SecurityDescriptor => throw new NotImplementedException();
    }
}

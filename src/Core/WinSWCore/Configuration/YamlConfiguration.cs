using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;
using WinSW.Native;
using WinSW.Util;
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
        public string? ResetFailureAfterYaml { get; set; }

        [YamlMember(Alias = "stopTimeout")]
        public string? StopTimeoutYaml { get; set; }

        [YamlMember(Alias = "startMode")]
        public string? StartModeYaml { get; set; }

        [YamlMember(Alias = "serviceDependencies")]
        public string[]? ServiceDependenciesYaml { get; set; }

        [YamlMember(Alias = "waitHint")]
        public string? WaitHintYaml { get; set; }

        [YamlMember(Alias = "sleepTime")]
        public string? SleepTimeYaml { get; set; }

        [YamlMember(Alias = "interactive")]
        public bool? InteractiveYaml { get; set; }

        [YamlMember(Alias = "priority")]
        public string? PriorityYaml { get; set; }

        [YamlMember(Alias = "beepOnShutdown")]
        public bool BeepOnShutdown { get; set; }

        [YamlMember(Alias = "env")]
        public List<YamlEnv>? EnvironmentVariablesYaml { get; set; }

        [YamlMember(Alias = "onFailure")]
        public List<YamlFailureAction>? YamlFailureActions { get; set; }

        [YamlMember(Alias = "delayedAutoStart")]
        public bool DelayedAutoStart { get; set; }

        [YamlMember(Alias = "securityDescriptor")]
        public string? SecurityDescriptorYaml { get; set; }

        [YamlMember(Alias = "extensions")]
        public List<string>? YamlExtensionIds { get; set; }

        public class YamlEnv
        {
            [YamlMember(Alias = "name")]
            public string? Name { get; set; }

            [YamlMember(Alias = "value")]
            public string? Value { get; set; }
        }

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
                        ExpandEnv(this.NameYamlLog);
                }
            }

            public override string Directory
            {
                get
                {
                    return this.LogPathYamlLog is null ?
                        DefaultWinSWSettings.DefaultLogSettings.Directory :
                        ExpandEnv(this.LogPathYamlLog);
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
                        ExpandEnv(this.OutFilePatternYamlLog);
                }
            }

            public override string ErrFilePattern
            {
                get
                {
                    return this.ErrFilePatternYamlLog is null ?
                        DefaultWinSWSettings.DefaultLogSettings.ErrFilePattern :
                        ExpandEnv(this.ErrFilePatternYamlLog);
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
            public string? AuthYamlDownload { get; set; }

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

            public string FromDownload => ExpandEnv(this.FromYamlDownload);

            public string ToDownload => ExpandEnv(this.ToYamlDownload);

            public string? UsernameDownload => this.UsernameYamlDownload is null ? null : ExpandEnv(this.UsernameYamlDownload);

            public string? PasswordDownload => this.PasswordYamlDownload is null ? null : ExpandEnv(this.PasswordYamlDownload);

            public string? ProxyDownload => this.ProxyYamlDownload is null ? null : ExpandEnv(this.ProxyYamlDownload);

            public AuthType AuthDownload
            {
                get
                {
                    if (this.AuthYamlDownload is null)
                    {
                        return AuthType.None;
                    }

                    var auth = ExpandEnv(this.AuthYamlDownload);

                    try
                    {
                        return (AuthType)Enum.Parse(typeof(AuthType), auth, true);
                    }
                    catch
                    {
                        Console.WriteLine("Auth type in YAML must be one of the following:");
                        foreach (string at in Enum.GetNames(typeof(AuthType)))
                        {
                            Console.WriteLine(at);
                        }

                        throw;
                    }
                }
            }
        }

        public class YamlFailureAction
        {
            [YamlMember(Alias = "action")]
            public string? FailureAction { get; set; }

            [YamlMember(Alias = "delay")]
            public string? FailureActionDelay { get; set; }

            public SC_ACTION_TYPE Type
            {
                get
                {
                    SC_ACTION_TYPE actionType = this.FailureAction switch
                    {
                        "restart" => SC_ACTION_TYPE.SC_ACTION_RESTART,
                        "none" => SC_ACTION_TYPE.SC_ACTION_NONE,
                        "reboot" => SC_ACTION_TYPE.SC_ACTION_REBOOT,
                        _ => throw new InvalidDataException("Invalid failure action: " + this.FailureAction)
                    };

                    return actionType;
                }
            }

            public TimeSpan Delay => this.FailureActionDelay is null ? TimeSpan.Zero : ConfigHelper.ParseTimeSpan(this.FailureActionDelay);
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

            return ExpandEnv(args);
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
                    item.FromDownload,
                    item.ToDownload,
                    item.FailOnErrorYamlDownload,
                    item.AuthDownload,
                    item.UsernameDownload,
                    item.PasswordDownload,
                    item.UnsecureAuthYamlDownload,
                    item.ProxyDownload));
            }

            return result;
        }

        internal static string ExpandEnv(string str)
        {
            return Environment.ExpandEnvironmentVariables(str);
        }

        public string Id => this.IdYaml is null ? this.Defaults.Id : ExpandEnv(this.IdYaml);

        public string Description => this.DescriptionYaml is null ? this.Defaults.Description : ExpandEnv(this.DescriptionYaml);

        public string Executable => this.ExecutableYaml is null ? this.Defaults.Executable : ExpandEnv(this.ExecutableYaml);

        public string ExecutablePath => this.ExecutablePathYaml is null ?
            this.Defaults.ExecutablePath :
            ExpandEnv(this.ExecutablePathYaml);

        public string Caption => this.NameYaml is null ? this.Defaults.Caption : ExpandEnv(this.NameYaml);

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

        public StartMode StartMode
        {
            get
            {
                if (this.StartModeYaml is null)
                {
                    return this.Defaults.StartMode;
                }

                var p = ExpandEnv(this.StartModeYaml);

                try
                {
                    return (StartMode)Enum.Parse(typeof(StartMode), p, true);
                }
                catch
                {
                    Console.WriteLine("Start mode in YAML must be one of the following:");
                    foreach (string sm in Enum.GetNames(typeof(StartMode)))
                    {
                        Console.WriteLine(sm);
                    }

                    throw;
                }
            }
        }

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
                    ExpandEnv(this.StopExecutableYaml);
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
            ConfigHelper.ParseTimeSpan(this.ResetFailureAfterYaml);

        public string WorkingDirectory => this.WorkingDirectoryYaml is null ?
            this.Defaults.WorkingDirectory :
            ExpandEnv(this.WorkingDirectoryYaml);

        public ProcessPriorityClass Priority
        {
            get
            {
                if (this.PriorityYaml is null)
                {
                    return this.Defaults.Priority;
                }

                var p = ExpandEnv(this.PriorityYaml);

                try
                {
                    return (ProcessPriorityClass)Enum.Parse(typeof(ProcessPriorityClass), p, true);
                }
                catch
                {
                    Console.WriteLine("Priority in YAML must be one of the following:");
                    foreach (string pr in Enum.GetNames(typeof(ProcessPriorityClass)))
                    {
                        Console.WriteLine(pr);
                    }

                    throw;
                }
            }
        }

        public TimeSpan StopTimeout => this.StopTimeoutYaml is null ? this.Defaults.StopTimeout : ConfigHelper.ParseTimeSpan(this.StopTimeoutYaml);

        public string[] ServiceDependencies
        {
            get
            {
                if (this.ServiceDependenciesYaml is null)
                {
                    return this.Defaults.ServiceDependencies;
                }

                var result = new List<string>(0);

                foreach (var item in this.ServiceDependenciesYaml)
                {
                    result.Add(ExpandEnv(item));
                }

                return result.ToArray();
            }
        }

        public TimeSpan WaitHint => this.WaitHintYaml is null ? this.Defaults.WaitHint : ConfigHelper.ParseTimeSpan(this.WaitHintYaml);

        public TimeSpan SleepTime => this.SleepTimeYaml is null ? this.Defaults.SleepTime : ConfigHelper.ParseTimeSpan(this.SleepTimeYaml);

        public bool Interactive => this.InteractiveYaml is null ? this.Defaults.Interactive : (bool)this.InteractiveYaml;

        public List<Download> Downloads => this.GetDownloads(this.DownloadsYaml);

        public Dictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();

        public void LoadEnvironmentVariables()
        {
            if (this.EnvironmentVariablesYaml is null)
            {
                this.EnvironmentVariables = this.Defaults.EnvironmentVariables;
            }
            else
            {
                foreach (var item in this.EnvironmentVariablesYaml)
                {
                    if (item.Name is null || item.Value is null)
                    {
                        continue;
                    }

                    var key = item.Name;
                    var value = ExpandEnv(item.Value);

                    this.EnvironmentVariables[key] = value;
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }

        public ServiceAccount ServiceAccount => this.ServiceAccountYaml is null ? this.Defaults.ServiceAccount : this.ServiceAccountYaml;

        public Log Log => this.YAMLLog is null ? this.Defaults.Log : this.YAMLLog;

        public string LogDirectory => this.Log.Directory;

        public string LogMode => this.Log.Mode is null ? this.Defaults.LogMode : this.Log.Mode;

        // TODO - Extensions
        XmlNode? IWinSWConfiguration.ExtensionsConfiguration => throw new NotImplementedException();

        public List<string> ExtensionIds => this.YamlExtensionIds ?? this.Defaults.ExtensionIds;

        public string BaseName => this.Defaults.BaseName;

        public string BasePath => this.Defaults.BasePath;

        public string? SecurityDescriptor
        {
            get
            {
                if (this.SecurityDescriptorYaml is null)
                {
                    return this.Defaults.SecurityDescriptor;
                }

                return ExpandEnv(this.SecurityDescriptorYaml);
            }
        }
    }
}

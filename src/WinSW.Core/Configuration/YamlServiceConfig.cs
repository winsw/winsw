using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Xml;
using WinSW.Native;
using WinSW.Util;
using YamlDotNet.Serialization;
using static System.Environment;
using static WinSW.Download;

namespace WinSW.Configuration
{
    public class YamlServiceConfig : IServiceConfig
    {
        private readonly DefaultSettings defaults;

        public YamlServiceConfig()
        {
            this.defaults = new DefaultSettings();
            this.BaseName = this.defaults.BaseName;
            this.BasePath = this.defaults.BasePath;
        }

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

        public class YamlEnv
        {
            [YamlMember(Alias = "name")]
            public string? Name { get; set; }

            [YamlMember(Alias = "value")]
            public string? Value { get; set; }
        }

        public class YamlLog : Log
        {
            private readonly YamlServiceConfig configs;

            public YamlLog()
            {
                this.configs = new YamlServiceConfig();
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
            public string? ZipOlderThanNumDaysYamlLog { get; set; }

            [YamlMember(Alias = "zipDateFormat")]
            public string? ZipDateFormatYamlLog { get; set; }

            public override string Mode => this.ModeYamlLog is null ?
                DefaultSettings.DefaultLogSettings.Mode :
                this.ModeYamlLog;

            public override string Name
            {
                get
                {
                    return this.NameYamlLog is null ?
                        DefaultSettings.DefaultLogSettings.Name :
                        ExpandEnvironmentVariables(this.NameYamlLog);
                }
            }

            public override string Directory
            {
                get
                {
                    return this.LogPathYamlLog is null ?
                        DefaultSettings.DefaultLogSettings.Directory :
                        ExpandEnvironmentVariables(this.LogPathYamlLog);
                }
            }

            public override int? SizeThreshold
            {
                get
                {
                    return this.SizeThresholdYamlLog is null ?
                        DefaultSettings.DefaultLogSettings.SizeThreshold :
                        this.SizeThresholdYamlLog;
                }
            }

            public override int? KeepFiles
            {
                get
                {
                    return this.KeepFilesYamlLog is null ?
                        DefaultSettings.DefaultLogSettings.KeepFiles :
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

                    return DefaultSettings.DefaultLogSettings.Pattern;
                }
            }

            public override int? Period => this.PeriodYamlLog is null ? 1 : this.PeriodYamlLog;

            public override bool OutFileDisabled
            {
                get
                {
                    return this.OutFileDisabledYamlLog is null ?
                        DefaultSettings.DefaultLogSettings.OutFileDisabled :
                        (bool)this.OutFileDisabledYamlLog;
                }
            }

            public override bool ErrFileDisabled
            {
                get
                {
                    return this.ErrFileDisabledYamlLog is null ?
                        this.configs.defaults.ErrFileDisabled :
                        (bool)this.ErrFileDisabledYamlLog;
                }
            }

            public override string OutFilePattern
            {
                get
                {
                    return this.OutFilePatternYamlLog is null ?
                        DefaultSettings.DefaultLogSettings.OutFilePattern :
                        ExpandEnvironmentVariables(this.OutFilePatternYamlLog);
                }
            }

            public override string ErrFilePattern
            {
                get
                {
                    return this.ErrFilePatternYamlLog is null ?
                        DefaultSettings.DefaultLogSettings.ErrFilePattern :
                        ExpandEnvironmentVariables(this.ErrFilePatternYamlLog);
                }
            }

            public override string? AutoRollAtTime
            {
                get
                {
                    return this.AutoRollAtTimeYamlLog is null ?
                        DefaultSettings.DefaultLogSettings.AutoRollAtTime :
                        this.AutoRollAtTimeYamlLog;
                }
            }

            public override int? ZipOlderThanNumDays
            {
                get
                {
                    int? zipolderthannumdays = null;

                    if (!string.IsNullOrEmpty(this.ZipOlderThanNumDaysYamlLog))
                    {
                        if (!int.TryParse(this.ZipOlderThanNumDaysYamlLog, out int zipolderthannumdaysValue))
                        {
                            throw new InvalidDataException("Roll-Size-Time Based rolling policy is specified but zipOlderThanNumDays does not match the int format found in configuration XML.");
                        }

                        zipolderthannumdays = zipolderthannumdaysValue;
                    }

                    return zipolderthannumdays;
                }
            }

            public override string? ZipDateFormat
            {
                get
                {
                    return this.ZipDateFormatYamlLog is null ?
                        DefaultSettings.DefaultLogSettings.ZipDateFormat :
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

            public string FromDownload => ExpandEnvironmentVariables(this.FromYamlDownload);

            public string ToDownload => ExpandEnvironmentVariables(this.ToYamlDownload);

            public string? UsernameDownload => this.UsernameYamlDownload is null ? null : ExpandEnvironmentVariables(this.UsernameYamlDownload);

            public string? PasswordDownload => this.PasswordYamlDownload is null ? null : ExpandEnvironmentVariables(this.PasswordYamlDownload);

            public string? ProxyDownload => this.ProxyYamlDownload is null ? null : ExpandEnvironmentVariables(this.ProxyYamlDownload);

            public AuthType AuthDownload
            {
                get
                {
                    if (this.AuthYamlDownload is null)
                    {
                        return AuthType.None;
                    }

                    string auth = ExpandEnvironmentVariables(this.AuthYamlDownload);

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
                    var actionType = this.FailureAction switch
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
                        return this.defaults.Arguments;
                    case ArgType.Startarg:
                        return this.defaults.StartArguments;
                    case ArgType.Stoparg:
                        return this.defaults.StopArguments;
                    default:
                        return string.Empty;
                }
            }

            return ExpandEnvironmentVariables(args);
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
                return this.defaults.Downloads;
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

        public string Name => this.IdYaml is null ? this.defaults.Name : ExpandEnvironmentVariables(this.IdYaml);

        public string Description => this.DescriptionYaml is null ? this.defaults.Description : ExpandEnvironmentVariables(this.DescriptionYaml);

        public string Executable => this.ExecutableYaml is null ? this.defaults.Executable : ExpandEnvironmentVariables(this.ExecutableYaml);

        public string ExecutablePath => this.ExecutablePathYaml is null ?
            this.defaults.ExecutablePath :
            ExpandEnvironmentVariables(this.ExecutablePathYaml);

        public string DisplayName => this.NameYaml is null ? this.defaults.DisplayName : ExpandEnvironmentVariables(this.NameYaml);

        public bool HideWindow => this.HideWindowYaml is null ? this.defaults.HideWindow : (bool)this.HideWindowYaml;

        public bool StopParentProcessFirst
        {
            get
            {
                return this.StopParentProcessFirstYaml is null ?
                    this.defaults.StopParentProcessFirst :
                    (bool)this.StopParentProcessFirstYaml;
            }
        }

        public ServiceStartMode StartMode
        {
            get
            {
                if (this.StartModeYaml is null)
                {
                    return this.defaults.StartMode;
                }

                string p = ExpandEnvironmentVariables(this.StartModeYaml);

                try
                {
                    return (ServiceStartMode)Enum.Parse(typeof(ServiceStartMode), p, true);
                }
                catch
                {
                    Console.WriteLine("Start mode in YAML must be one of the following:");
                    foreach (string sm in Enum.GetNames(typeof(ServiceStartMode)))
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
                string? args = this.GetArguments(this.ArgumentsYaml, ArgType.Arg);
                return args is null ? this.defaults.Arguments : args;
            }
        }

        public string? StartArguments => this.GetArguments(this.StartArgumentsYaml, ArgType.Startarg);

        public string? StopArguments => this.GetArguments(this.StopArgumentsYaml, ArgType.Stoparg);

        public string? StopExecutable
        {
            get
            {
                return this.StopExecutableYaml is null ?
                    this.defaults.StopExecutable :
                    ExpandEnvironmentVariables(this.StopExecutableYaml);
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
            this.defaults.ResetFailureAfter :
            ConfigHelper.ParseTimeSpan(this.ResetFailureAfterYaml);

        public string WorkingDirectory => this.WorkingDirectoryYaml is null ?
            this.defaults.WorkingDirectory :
            ExpandEnvironmentVariables(this.WorkingDirectoryYaml);

        public ProcessPriorityClass Priority
        {
            get
            {
                if (this.PriorityYaml is null)
                {
                    return this.defaults.Priority;
                }

                string p = ExpandEnvironmentVariables(this.PriorityYaml);

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

        public TimeSpan StopTimeout => this.StopTimeoutYaml is null ? this.defaults.StopTimeout : ConfigHelper.ParseTimeSpan(this.StopTimeoutYaml);

        public string[] ServiceDependencies
        {
            get
            {
                if (this.ServiceDependenciesYaml is null)
                {
                    return this.defaults.ServiceDependencies;
                }

                var result = new List<string>(0);

                foreach (string item in this.ServiceDependenciesYaml)
                {
                    result.Add(ExpandEnvironmentVariables(item));
                }

                return result.ToArray();
            }
        }

        public TimeSpan WaitHint => this.WaitHintYaml is null ? this.defaults.WaitHint : ConfigHelper.ParseTimeSpan(this.WaitHintYaml);

        public TimeSpan SleepTime => this.SleepTimeYaml is null ? this.defaults.SleepTime : ConfigHelper.ParseTimeSpan(this.SleepTimeYaml);

        public bool Interactive => this.InteractiveYaml is null ? this.defaults.Interactive : (bool)this.InteractiveYaml;

        public List<Download> Downloads => this.GetDownloads(this.DownloadsYaml);

        public Dictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();

        public void LoadEnvironmentVariables()
        {
            if (this.EnvironmentVariablesYaml is null)
            {
                this.EnvironmentVariables = this.defaults.EnvironmentVariables;
            }
            else
            {
                foreach (var item in this.EnvironmentVariablesYaml)
                {
                    if (item.Name is null || item.Value is null)
                    {
                        continue;
                    }

                    string key = item.Name;
                    string value = ExpandEnvironmentVariables(item.Value);

                    this.EnvironmentVariables[key] = value;
                    SetEnvironmentVariable(key, value);
                }
            }
        }

        public ServiceAccount ServiceAccount => this.ServiceAccountYaml is null ? this.defaults.ServiceAccount : this.ServiceAccountYaml;

        public Log Log => this.YAMLLog is null ? this.defaults.Log : this.YAMLLog;

        public string LogDirectory => this.Log.Directory;

        public string LogMode => this.Log.Mode is null ? this.defaults.LogMode : this.Log.Mode;

        public XmlNode? XmlExtensions => null;

        // YAML Extension
        [YamlMember(Alias = "extensions")]
        public List<YamlExtensionConfig>? YamlExtensions { get; set; }

        public List<string> ExtensionIds
        {
            get
            {
                int extensionNumber = 1;

                if (this.YamlExtensions is null)
                {
                    return new List<string>(0);
                }

                var result = new List<string>(this.YamlExtensions.Count);

                foreach (var item in this.YamlExtensions)
                {
                    try
                    {
                        result.Add(item.GetId());
                    }
                    catch (InvalidDataException)
                    {
                        throw new InvalidDataException("Id is null in Extension " + extensionNumber);
                    }

                    extensionNumber++;
                }

                return result;
            }
        }

        public string BaseName { get; set; }

        public string BasePath { get; set; }

        public string? SecurityDescriptor
        {
            get
            {
                if (this.SecurityDescriptorYaml is null)
                {
                    return this.defaults.SecurityDescriptor;
                }

                return ExpandEnvironmentVariables(this.SecurityDescriptorYaml);
            }
        }
    }
}

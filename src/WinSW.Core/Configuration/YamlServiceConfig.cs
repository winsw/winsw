using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
#if VNEXT
using System.Runtime.CompilerServices;
#endif
using System.ServiceProcess;
using System.Xml;
using WinSW.Native;
using WinSW.Util;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace WinSW.Configuration
{
    public class YamlServiceConfig : IServiceConfig
    {
        private readonly DefaultSettings defaults;
        private readonly RawYamlServiceConfig raw;

        public YamlServiceConfig(string baseName, string directory)
        {
            this.defaults = new DefaultSettings();

            this.BaseName = baseName;
            this.BasePath = Path.Combine(directory, baseName);

            using (var reader = new StreamReader(this.BasePath + ".yml"))
            {
                var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
                this.raw = deserializer.Deserialize<RawYamlServiceConfig>(reader);
            }

            Environment.SetEnvironmentVariable("BASE", directory);

            // ditto for ID
            Environment.SetEnvironmentVariable("SERVICE_ID", this.Name);

            // New name
            Environment.SetEnvironmentVariable(WinSWSystem.EnvVarNameExecutablePath, this.ExecutablePath);

            // Also inject system environment variables
            Environment.SetEnvironmentVariable(WinSWSystem.EnvVarNameServiceId, this.Name);

            this.LoadEnvironmentVariables();
        }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private YamlServiceConfig(RawYamlServiceConfig raw)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            this.defaults = new DefaultSettings();
            this.raw = raw;
        }

        public static YamlServiceConfig FromYaml(string yaml)
        {
            var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
            var raw = deserializer.Deserialize<RawYamlServiceConfig>(yaml);
            return new(raw);
        }

        private static string? Expand(string? value) => value is null ? null : Environment.ExpandEnvironmentVariables(value);

        private static bool? ExpandBoolean(string? value) => value is null ? null : ConfigHelper.YamlBoolParse(Environment.ExpandEnvironmentVariables(value));

        private static TimeSpan? ExpandTimeSpan(string? value) => value is null ? null : ConfigHelper.ParseTimeSpan(Environment.ExpandEnvironmentVariables(value));

        private static T? ExpandEnum<T>(
            string? value,
#if VNEXT
            [CallerMemberName] string? name = null)
#else
            string? name = null)
#endif
            where T : unmanaged, Enum
        {
            if (value is null)
            {
                return null;
            }

            try
            {
                return (T)Enum.Parse(typeof(T), value, true);
            }
            catch
            {
                Console.WriteLine($"'{name ?? typeof(T).FullName}' in YAML must be one of the followings: {string.Join(", ", Enum.GetNames(typeof(T)))}.");
                throw;
            }
        }

        public string Name => Expand(this.raw.Id) ?? this.defaults.Name;

        public string Description => Expand(this.raw.Description) ?? this.defaults.Description;

        public string Executable => Expand(this.raw.Executable) ?? this.defaults.Executable;

        public string ExecutablePath => this.defaults.ExecutablePath;

        public string DisplayName => Expand(this.raw.Name) ?? this.defaults.DisplayName;

        public bool HideWindow => ExpandBoolean(this.raw.HideWindow) ?? this.defaults.HideWindow;

        public bool StopParentProcessFirst => ExpandBoolean(this.raw.StopParentProcessFirst) ?? this.defaults.StopParentProcessFirst;

        public ServiceStartMode StartMode => ExpandEnum<ServiceStartMode>(this.raw.StartMode) ?? this.defaults.StartMode;

        public bool DelayedAutoStart => ExpandBoolean(this.raw.DelayedAutoStart) ?? this.defaults.DelayedAutoStart;

        public bool BeepOnShutdown => ExpandBoolean(this.raw.BeepOnShutdown) ?? this.defaults.BeepOnShutdown;

        public string Arguments => Expand(this.raw.Arguments) ?? this.defaults.Arguments;

        public string? StartArguments => Expand(this.raw.StartArguments) ?? this.defaults.StartArguments;

        public string? StopArguments => Expand(this.raw.StopArguments) ?? this.defaults.StopArguments;

        public string? StopExecutable => Expand(this.raw.StopExecutable) ?? this.defaults.StopExecutable;

        public SC_ACTION[] FailureActions
        {
            get
            {
                if (this.raw.OnFailure is null)
                {
#if VNEXT
                    return Array.Empty<SC_ACTION>();
#else
                    return new SC_ACTION[0];
#endif
                }

                var result = new SC_ACTION[this.raw.OnFailure.Count];

                for (int i = 0; i < result.Length; i++)
                {
                    var item = new YamlFailureAction(this.raw.OnFailure[i]);
                    result[i] = new(item.Type, item.Delay);
                }

                return result;
            }
        }

        public TimeSpan ResetFailureAfter => ExpandTimeSpan(this.raw.ResetFailure) ?? this.defaults.ResetFailureAfter;

        public string WorkingDirectory => Expand(this.raw.WorkingDirectory) ?? this.defaults.WorkingDirectory;

        public ProcessPriorityClass Priority => ExpandEnum<ProcessPriorityClass>(this.raw.Priority) ?? this.defaults.Priority;

        public TimeSpan StopTimeout => ExpandTimeSpan(this.raw.StopTimeout) ?? this.defaults.StopTimeout;

        public string[] ServiceDependencies
        {
            get
            {
                if (this.raw.Depend is null)
                {
                    return this.defaults.ServiceDependencies;
                }

                string[] result = new string[this.raw.Depend.Length];

                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = Environment.ExpandEnvironmentVariables(this.raw.Depend[i]);
                }

                return result;
            }
        }

        public TimeSpan WaitHint => this.defaults.WaitHint;

        public TimeSpan SleepTime => this.defaults.SleepTime;

        public bool Interactive => ExpandBoolean(this.raw.Interactive) ?? this.defaults.Interactive;

        public List<Download> Downloads
        {
            get
            {
                if (this.raw.Download is null)
                {
                    return this.defaults.Downloads;
                }

                var result = new List<Download>(this.raw.Download.Count);

                foreach (var item in this.raw.Download)
                {
                    result.Add(new YamlDownload(item).Download);
                }

                return result;
            }
        }

        public Dictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();

        public void LoadEnvironmentVariables()
        {
            if (this.raw.Env is null)
            {
                this.EnvironmentVariables = this.defaults.EnvironmentVariables;
                return;
            }

            foreach (var item in this.raw.Env)
            {
                if (item.Name is null || item.Value is null)
                {
                    continue;
                }

                string key = item.Name;
                string value = Environment.ExpandEnvironmentVariables(item.Value);

                this.EnvironmentVariables[key] = value;
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        public ServiceAccount ServiceAccount
        {
            get
            {
                var rawServiceAccount = this.raw.ServiceAccount;
                if (rawServiceAccount is null)
                {
                    return this.defaults.ServiceAccount;
                }

                return new()
                {
                    User = Expand(rawServiceAccount.User),
                    Domain = Expand(rawServiceAccount.Domain),
                    Password = Expand(rawServiceAccount.Password),
                    AllowServiceLogonRight = ExpandBoolean(rawServiceAccount.AllowServiceLogon) ?? false,
                };
            }
        }

        public Log Log => this.raw.Log is null ? this.defaults.Log : new YamlLog(this.raw.Log);

        public string LogDirectory => this.Log.Directory;

        public string LogMode => this.Log.Mode is null ? this.defaults.LogMode : this.Log.Mode;

        public XmlNode? XmlExtensions => null;

        public List<YamlExtensionConfig>? YamlExtensions => this.raw.Extensions;

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

        public string? SecurityDescriptor => Expand(this.raw.SecurityDescriptor) ?? this.defaults.SecurityDescriptor;

        internal sealed class RawYamlServiceConfig
        {
            public string? Id;
            public string? Name;
            public string? Description;
            public string? Executable;
            public string? HideWindow;
            public string? WorkingDirectory;
            public RawServiceAccount? ServiceAccount;
            public RawYamlLog? Log;
            public List<RawYamlDownload>? Download;
            public string? Arguments;
            public string? StartArguments;
            public string? StopArguments;
            public string? StopExecutable;
            public string? StopParentProcessFirst;
            public string? ResetFailure;
            public string? StopTimeout;
            public string? StartMode;
            public string[]? Depend;
            public string? Interactive;
            public string? Priority;
            public string? BeepOnShutdown;
            public List<RawYamlEnv>? Env;
            public List<RawYamlFailureAction>? OnFailure;
            public string? DelayedAutoStart;
            public string? SecurityDescriptor;
            public List<YamlExtensionConfig>? Extensions;
        }

        internal sealed class RawServiceAccount
        {
            public string? User;
            public string? Domain;
            public string? Password;
            public string? AllowServiceLogon;
        }

        internal sealed class YamlLog : Log
        {
            private readonly DefaultSettings.LogDefaults defaults = new();
            private readonly RawYamlLog raw;

            internal YamlLog(RawYamlLog raw) => this.raw = raw;

            public override string Mode => Expand(this.raw.Mode) ?? this.defaults.Mode;

            public override string Name => Expand(this.raw.Name) ?? this.defaults.Name;

            public override string Directory => Expand(this.raw.LogPath) ?? this.defaults.Directory;

            public override int? SizeThreshold => this.raw.SizeThreshold ?? this.defaults.SizeThreshold;

            public override int? KeepFiles => this.raw.KeepFiles ?? this.defaults.KeepFiles;

            public override string Pattern => Expand(this.raw.Pattern) ?? this.defaults.Pattern;

            public override int? Period => this.raw.Period ?? this.defaults.Period;

            public override bool OutFileDisabled => ExpandBoolean(this.raw.OutFileDisabled) ?? this.defaults.OutFileDisabled;

            public override bool ErrFileDisabled => ExpandBoolean(this.raw.ErrFileDisabled) ?? this.defaults.ErrFileDisabled;

            public override string OutFilePattern => Expand(this.raw.OutFilePattern) ?? this.defaults.OutFilePattern;

            public override string ErrFilePattern => Expand(this.raw.ErrFilePattern) ?? this.defaults.ErrFilePattern;

            public override string? AutoRollAtTime => Expand(this.raw.AutoRollAtTime) ?? this.defaults.AutoRollAtTime;

            public override int? ZipOlderThanNumDays => this.raw.ZipOlderThanNumDays;

            public override string? ZipDateFormat => Expand(this.raw.ZipDateFormat) ?? this.defaults.ZipDateFormat;
        }

        internal sealed class RawYamlLog
        {
            public string? Mode;
            public string? Name;
            public int? SizeThreshold;
            public int? KeepFiles;
            public string? Pattern;
            public int? Period;
            public string? LogPath;
            public string? OutFileDisabled;
            public string? ErrFileDisabled;
            public string? OutFilePattern;
            public string? ErrFilePattern;
            public string? AutoRollAtTime;
            public int? ZipOlderThanNumDays;
            public string? ZipDateFormat;
        }

        internal sealed class YamlDownload
        {
            private readonly RawYamlDownload raw;

            internal YamlDownload(RawYamlDownload raw) => this.raw = raw;

            public Download Download => new(this.From, this.To, this.FailOnError, this.Auth, this.Username, this.Password, this.UnsecureAuth, this.Proxy);

            public string From => Expand(this.raw.From)!;

            public string To => Expand(this.raw.To)!;

            public string? Username => Expand(this.raw.Username);

            public string? Password => Expand(this.raw.Password);

            public bool UnsecureAuth => ExpandBoolean(this.raw.UnsecureAuth) ?? false;

            public bool FailOnError => ExpandBoolean(this.raw.FailOnError) ?? false;

            public string? Proxy => Expand(this.raw.Proxy);

            public Download.AuthType Auth => ExpandEnum<Download.AuthType>(this.raw.Auth) ?? Download.AuthType.None;
        }

        internal sealed class RawYamlDownload
        {
            public string From = string.Empty;
            public string To = string.Empty;
            public string? Auth;
            public string? Username;
            public string? Password;
            public string? UnsecureAuth;
            public string? FailOnError;
            public string? Proxy;
        }

        internal sealed class RawYamlEnv
        {
            public string? Name;
            public string? Value;
        }

        internal sealed class YamlFailureAction
        {
            private readonly RawYamlFailureAction raw;

            internal YamlFailureAction(RawYamlFailureAction raw) => this.raw = raw;

            public SC_ACTION_TYPE Type => ExpandEnum<SC_ACTION_TYPE>(this.raw.Action) ?? SC_ACTION_TYPE.NONE;

            public TimeSpan Delay => ExpandTimeSpan(this.raw.Delay) ?? TimeSpan.Zero;
        }

        internal sealed class RawYamlFailureAction
        {
            public string? Action;
            public string? Delay;
        }
    }
}

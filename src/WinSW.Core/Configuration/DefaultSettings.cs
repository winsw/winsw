using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Xml;

namespace WinSW.Configuration
{
    /// <summary>
    /// Default WinSW settings.
    /// </summary>
    public sealed class DefaultSettings : IServiceConfig
    {
        public static LogDefaults DefaultLogSettings { get; } = new LogDefaults();

        public string Name => throw new InvalidOperationException(nameof(this.Name) + " must be specified.");

        public string DisplayName => throw new InvalidOperationException(nameof(this.DisplayName) + " must be specified.");

        public string Description => throw new InvalidOperationException(nameof(this.Description) + " must be specified.");

        public string Executable => throw new InvalidOperationException(nameof(this.Executable) + " must be specified.");

        public bool HideWindow => false;

        public string ExecutablePath => Process.GetCurrentProcess().MainModule!.FileName!;

        // Installation
        public Native.SC_ACTION[] FailureActions => new Native.SC_ACTION[0];

        public TimeSpan ResetFailureAfter => TimeSpan.FromDays(1);

        // Executable management
        public string Arguments => string.Empty;

        public string? StartArguments => null;

        public string? StopExecutable => null;

        public string? StopArguments => null;

        public string WorkingDirectory => Path.GetDirectoryName(this.ExecutablePath)!;

        public ProcessPriorityClass Priority => ProcessPriorityClass.Normal;

        public TimeSpan StopTimeout => TimeSpan.FromSeconds(15);

        public bool StopParentProcessFirst => true;

        // Service management
        public ServiceStartMode StartMode => ServiceStartMode.Automatic;

        public bool DelayedAutoStart => false;

        public string[] ServiceDependencies => new string[0];

        public TimeSpan WaitHint => TimeSpan.FromSeconds(15);

        public TimeSpan SleepTime => TimeSpan.FromSeconds(1);

        public bool Interactive => false;

        // Logging
        public Log Log => new LogDefaults();

        public string LogDirectory => DefaultLogSettings.Directory;

        public string LogMode => DefaultLogSettings.Mode;

        public bool OutFileDisabled => this.Log.OutFileDisabled;

        public bool ErrFileDisabled => this.Log.ErrFileDisabled;

        public string OutFilePattern => this.Log.OutFilePattern;

        public string ErrFilePattern => this.Log.ErrFilePattern;

        public ServiceAccount ServiceAccount => new()
        {
            User = null,
            Domain = null,
            Password = null,
            AllowServiceLogonRight = false
        };

        public class LogDefaults : Log
        {
            private readonly DefaultSettings defaults;

            public LogDefaults()
            {
                this.defaults = new DefaultSettings();
            }

            public override string Mode => "append";

            public override string Name => this.defaults.BaseName;

            public override string Directory => Path.GetDirectoryName(this.defaults.ExecutablePath)!;

            public override int? SizeThreshold => 1024 * 10 * RollingSizeTimeLogAppender.BytesPerKB;

            public override int? KeepFiles => SizeBasedRollingLogAppender.DefaultFilesToKeep;

            public override string Pattern =>
                throw new InvalidDataException("Time Based rolling policy is specified but no pattern can be found in configuration XML.");

            public override int? Period => 1;

            public override bool OutFileDisabled => false;

            public override bool ErrFileDisabled => false;

            public override string OutFilePattern => ".out.log";

            public override string ErrFilePattern => ".err.log";

            public override string? AutoRollAtTime => null;

            public override int? ZipOlderThanNumDays =>
                throw new InvalidDataException("Roll-Size-Time Based rolling policy is specified but zipOlderThanNumDays does not match the int format found in configuration XML.");

            public override string? ZipDateFormat => null;
        }

        // Environment
        public List<Download> Downloads => new(0);

        public Dictionary<string, string> EnvironmentVariables => new(0);

        // Misc
        public bool BeepOnShutdown => false;

        // Extensions
        public XmlNode? XmlExtensions => null;

        public List<YamlExtensionConfig>? YamlExtensions => new(0);

        public string BaseName => Path.GetFileNameWithoutExtension(this.ExecutablePath);

        public string BasePath => Path.Combine(Path.GetDirectoryName(this.ExecutablePath)!, this.BaseName);

        public List<string> ExtensionIds => new(0);

        public string? SecurityDescriptor => null;
    }
}

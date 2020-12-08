using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;
using WMI;

namespace WinSW.Configuration
{
    /// <summary>
    /// Default WinSW settings.
    /// </summary>
    public sealed class DefaultWinSWSettings : IWinSWConfiguration
    {
        public static LogDefaults DefaultLogSettings { get; } = new LogDefaults();

        public string Id => throw new InvalidOperationException(nameof(this.Id) + " must be specified.");

        public string Caption => throw new InvalidOperationException(nameof(this.Caption) + " must be specified.");

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
        public StartMode StartMode => StartMode.Automatic;

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
            ServiceAccountName = null,
            ServiceAccountDomain = null,
            ServiceAccountPassword = null,
            AllowServiceAcountLogonRight = false
        };

        public class LogDefaults : Log
        {
            private readonly DefaultWinSWSettings defaults;

            public LogDefaults()
            {
                this.defaults = new DefaultWinSWSettings();
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
        public XmlNode? ExtensionsConfiguration => null;

        public List<YamlExtensionConfiguration>? YamlExtensionsConfiguration => new(0);

        public string BaseName
        {
            get
            {
                string baseName = Path.GetFileNameWithoutExtension(this.ExecutablePath);
                if (baseName.EndsWith(".vshost"))
                {
                    baseName = baseName.Substring(0, baseName.Length - 7);
                }

                return baseName;
            }
        }

        public string BasePath
        {
            get
            {
                var d = new DirectoryInfo(Path.GetDirectoryName(this.ExecutablePath)!);
                return Path.Combine(d.FullName, this.BaseName);
            }
        }

        public List<string> ExtensionIds => new(0);

        public string? SecurityDescriptor => null;
    }
}

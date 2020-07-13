using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;
using WMI;

namespace winsw.Configuration
{
    /// <summary>
    /// Default WinSW settings
    /// </summary>
    public sealed class DefaultWinSWSettings : IWinSWConfiguration
    {
        public static LogDefaults DefaultLogSettings { get; } = new LogDefaults();

        public string Id => throw new InvalidOperationException(nameof(Id) + " must be specified.");
        public string Caption => throw new InvalidOperationException(nameof(Caption) + " must be specified.");
        public string Description => throw new InvalidOperationException(nameof(Description) + " must be specified.");
        public string Executable => throw new InvalidOperationException(nameof(Executable) + " must be specified.");
        public bool HideWindow => false;

        public string ExecutablePath => Process.GetCurrentProcess().MainModule.FileName;

        // Installation
        public bool AllowServiceAcountLogonRight => false;
        public string? ServiceAccountPassword => null;
        public string? ServiceAccountUser => null;
        public Native.SC_ACTION[] FailureActions => new Native.SC_ACTION[0];
        public TimeSpan ResetFailureAfter => TimeSpan.FromDays(1);

        // Executable management
        public string Arguments => string.Empty;
        public string? StartArguments => null;
        public string? StopExecutable => null;
        public string? StopArguments => null;
        public string WorkingDirectory => Path.GetDirectoryName(ExecutablePath)!;
        public ProcessPriorityClass Priority => ProcessPriorityClass.Normal;
        public TimeSpan StopTimeout => TimeSpan.FromSeconds(15);
        public bool StopParentProcessFirst => false;

        // Service management
        public StartMode StartMode => StartMode.Automatic;
        public bool DelayedAutoStart => false;
        public string[] ServiceDependencies => new string[0];
        public TimeSpan WaitHint => TimeSpan.FromSeconds(15);
        public TimeSpan SleepTime => TimeSpan.FromSeconds(1);
        public bool Interactive => false;

        // Logging
        public Log Log { get => new LogDefaults(); }
        public string LogDirectory => DefaultLogSettings.Directory;
        public string LogMode => DefaultLogSettings.Mode;

        public bool OutFileDisabled => Log.OutFileDisabled;
        public bool ErrFileDisabled => Log.ErrFileDisabled;
        public string OutFilePattern => Log.OutFilePattern;
        public string ErrFilePattern => Log.ErrFilePattern;


        public ServiceAccount ServiceAccount => new ServiceAccount()
        {
            Name = null,
            Domain = null,
            User = null,
            Password = null,
            AllowServiceAcountLogonRight = false
        };

        public class LogDefaults : Log
        {
            readonly DefaultWinSWSettings defaults;

            public LogDefaults()
            {
                defaults = new DefaultWinSWSettings();
            }

            public override string Mode => "append";

            public override string? Name => defaults.BaseName;

            public override string Directory => Path.GetDirectoryName(defaults.ExecutablePath)!;

            public override int? SizeThreshold => 1024 * 10 * RollingSizeTimeLogAppender.BYTES_PER_KB;

            public override int? KeepFiles => SizeBasedRollingLogAppender.DEFAULT_FILES_TO_KEEP;

            public override string? Pattern =>
                throw new InvalidDataException("Time Based rolling policy is specified but no pattern can be found in configuration XML.");

            public override int? Period => 1;

            public override bool OutFileDisabled { get => false; }
            public override bool ErrFileDisabled { get => false; }
            public override string OutFilePattern { get => ".out.log"; }
            public override string ErrFilePattern { get => ".err.log"; }

            public override string? AutoRollAtTime => null;

            public override int? ZipOlderThanNumDays =>
                throw new InvalidDataException("Roll-Size-Time Based rolling policy is specified but zipOlderThanNumDays does not match the int format found in configuration XML.");

            public override string? ZipDateFormat => null;
        }

        // Environment
        public List<Download> Downloads => new List<Download>(0);
        public Dictionary<string, string> EnvironmentVariables => new Dictionary<string, string>(0);

        // Misc
        public bool BeepOnShutdown => false;

        // Extensions
        public XmlNode? ExtensionsConfiguration => null;

        public string BaseName
        {
            get
            {
                string baseName = Path.GetFileNameWithoutExtension(ExecutablePath);
                if (baseName.EndsWith(".vshost"))
                    baseName = baseName.Substring(0, baseName.Length - 7);

                return baseName;
            }
        }

        public string BasePath => null;

        public List<string> ExtensionIds => new List<string>(0);

        public string? ServiceAccountDomain => null;

        public string? ServiceAccountName => null;

        public string? SecurityDescriptor => null;

        public bool HasServiceAccount()
        {
            return false;
        }
    }
}

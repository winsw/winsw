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
        public Log Log { get => new LogDefaults(this); }
        public string LogDirectory => Path.GetDirectoryName(ExecutablePath)!;
        public string LogMode => Log.Mode;

        public bool OutFileDisabled => Log.OutFileDisabled;
        public bool ErrFileDisabled => Log.ErrFileDisabled;
        public string OutFilePattern => Log.OutFilePattern;
        public string ErrFilePattern => Log.ErrFilePattern;

        public class LogDefaults : Log
        {
            private DefaultWinSWSettings defaults;

            public LogDefaults(DefaultWinSWSettings settings)
            {
                defaults = settings;
            }


            public override string Mode  => "append";

            public override string? Name => throw new NotImplementedException();

            public override string? Directory => throw new NotImplementedException();

            public override int? SizeThreshold => throw new NotImplementedException();

            public override int? KeepFiles => throw new NotImplementedException();

            public override string? Pattern => throw new NotImplementedException();

            public override int? Period => throw new NotImplementedException();

            public override bool OutFileDisabled { get => false; }
            public override bool ErrFileDisabled { get => false; }
            public override string OutFilePattern { get => ".out.log"; }
            public override string ErrFilePattern { get => ".err.log"; }

            public override string? AutoRollAtTime => throw new NotImplementedException();

            public override int? ZipOlderThanNumDays => throw new NotImplementedException();

            public override string? ZipDateFormat => throw new NotImplementedException();
        }

        // Environment
        public List<Download> Downloads => new List<Download>(0);
        public Dictionary<string, string> EnvironmentVariables => new Dictionary<string, string>(0);

        // Misc
        public bool BeepOnShutdown => false;

        // Extensions
        public XmlNode? ExtensionsConfiguration => null;
    }
}

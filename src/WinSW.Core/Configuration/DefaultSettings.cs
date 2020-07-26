using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Xml;

namespace WinSW.Configuration
{
    /// <summary>
    /// Default WinSW settings
    /// </summary>
    public sealed class DefaultWinSWSettings : IWinSWConfiguration
    {
        public string Id => throw new InvalidOperationException(nameof(this.Id) + " must be specified.");

        public string Caption => string.Empty;

        public string Description => string.Empty;

        public string Executable => throw new InvalidOperationException(nameof(this.Executable) + " must be specified.");

        public bool HideWindow => false;

        public string ExecutablePath => Process.GetCurrentProcess().MainModule.FileName;

        // Installation
        public bool AllowServiceAcountLogonRight => false;

        public string? ServiceAccountPassword => null;

        public string? ServiceAccountUserName => null;

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

        // Service management
        public ServiceStartMode StartMode => ServiceStartMode.Automatic;

        public bool DelayedAutoStart => false;

        public string[] ServiceDependencies => new string[0];

        public bool Interactive => false;

        // Logging
        public string LogDirectory => Path.GetDirectoryName(this.ExecutablePath)!;

        public string LogMode => "append";

        public bool OutFileDisabled => false;

        public bool ErrFileDisabled => false;

        public string OutFilePattern => ".out.log";

        public string ErrFilePattern => ".err.log";

        // Environment
        public List<Download> Downloads => new List<Download>(0);

        public Dictionary<string, string> EnvironmentVariables => new Dictionary<string, string>(0);

        // Misc
        public bool BeepOnShutdown => false;

        // Extensions
        public XmlNode? ExtensionsConfiguration => null;
    }
}

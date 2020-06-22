using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml;
using winsw.Native;
using WMI;
using YamlDotNet.Serialization;

namespace winsw.Configuration
{
    public class YamlConfiguration : IWinSWConfiguration
    {
        [YamlMember(Alias = "id")]
        public string Id { get; set; }

        [YamlMember(Alias = "name")]
        public string? Name { get; set; }

        [YamlMember(Alias = "description")]
        public string? Description { get; set; }

        [YamlMember(Alias = "executable")]
        public string? Executable { get; set; }

        [YamlMember(Alias = "workingdirectory")]
        public string? WorkingDirectory { get; set; }

        [YamlMember(Alias = "serviceaccount")]
        public ServiceAccount? ServiceAccount { get; set; }
        string? IWinSWConfiguration.ServiceAccountPassword => ServiceAccount != null ? ServiceAccount.password : null;
        string? IWinSWConfiguration.ServiceAccountUser => ServiceAccount != null ? ServiceAccount.user : null;

        [YamlMember(Alias = "log")]
        public YAMLLog? _YAMLLog { get; set; }
        public Log? Log => _YAMLLog;

        string IWinSWConfiguration.LogDirectory => Log != null ? Log.Directory : null;
        string IWinSWConfiguration.LogMode => Log != null ? Log.Directory : null;

        public class YAMLLog : Log
        {
            [YamlMember(Alias = "mode")]
            public string? _Mode { get; set; }

            [YamlMember(Alias = "name")]
            public string? _Name { get; set; }

            [YamlMember(Alias = "directory")]
            public string? _Directory { get; set; }

            [YamlMember(Alias = "sizeThreshold")]
            public int? _SizeThreshold { get; set; }

            [YamlMember(Alias = "keepFiles")]
            public int? _KeepFiles { get; set; }

            [YamlMember(Alias = "pattern")]
            public string? _Pattern { get; set; }


            [YamlMember(Alias = "period")]
            public int? _Period { get; set; }


            // Filters
            [YamlMember(Alias = "outFileDisabled")]
            public bool _OutFileDisabled { get; set; }

            [YamlMember(Alias = "errFileDisabled")]
            public bool _ErrFileDisabled { get; set; }

            [YamlMember(Alias = "outFilePattern")]
            public string _OutFilePattern;

            [YamlMember(Alias = "errFilePattern")]
            public string _ErrFilePattern;


            // Zip options
            [YamlMember(Alias = "autoRollAtTime")]
            public string? _AutoRollAtTime { get; set; }

            [YamlMember(Alias = "zipOlderThanNumDays")]
            public int? _ZipOlderThanNumDays { get; set; }

            [YamlMember(Alias = "zipDateFormat")]
            public string? _ZipDateFormat { get; set; }

            public override string? Mode => _Mode;

            public override string? Name => _Name;

            public override string? Directory => _Directory;

            public override int? SizeThreshold => _SizeThreshold;

            public override int? KeepFiles => _KeepFiles;

            public override string? Pattern => _Pattern;

            public override int? Period => _Period;

            public override bool OutFileDisabled => _OutFileDisabled;

            public override bool ErrFileDisabled => _ErrFileDisabled;

            public override string OutFilePattern => _OutFilePattern;

            public override string ErrFilePattern => _ErrFilePattern;

            public override string? AutoRollAtTime => _AutoRollAtTime;

            public override int? ZipOlderThanNumDays => _ZipOlderThanNumDays;

            public override string? ZipDateFormat => _ZipDateFormat;
        }

        [YamlMember(Alias = "download")]
        public List<Download>? Download { get; set; }


        string IWinSWConfiguration.Caption => throw new NotImplementedException();

        string IWinSWConfiguration.ExecutablePath => throw new NotImplementedException();

        bool IWinSWConfiguration.HideWindow => throw new NotImplementedException();

        bool IWinSWConfiguration.AllowServiceAcountLogonRight => throw new NotImplementedException();

        // TODO: Implement configuration
        SC_ACTION[] IWinSWConfiguration.FailureActions => new SC_ACTION[0];

        TimeSpan IWinSWConfiguration.ResetFailureAfter => throw new NotImplementedException();


        string IWinSWConfiguration.Arguments => throw new NotImplementedException();

        string? IWinSWConfiguration.StartArguments => throw new NotImplementedException();

        string? IWinSWConfiguration.StopExecutable => throw new NotImplementedException();

        string? IWinSWConfiguration.StopArguments => throw new NotImplementedException();

        string IWinSWConfiguration.WorkingDirectory => throw new NotImplementedException();

        ProcessPriorityClass IWinSWConfiguration.Priority => throw new NotImplementedException();

        TimeSpan IWinSWConfiguration.StopTimeout => throw new NotImplementedException();

        bool IWinSWConfiguration.StopParentProcessFirst => throw new NotImplementedException();

        StartMode IWinSWConfiguration.StartMode => throw new NotImplementedException();

        string[] IWinSWConfiguration.ServiceDependencies => throw new NotImplementedException();

        TimeSpan IWinSWConfiguration.WaitHint => throw new NotImplementedException();

        TimeSpan IWinSWConfiguration.SleepTime => throw new NotImplementedException();

        bool IWinSWConfiguration.Interactive => throw new NotImplementedException();

 

        List<Download> IWinSWConfiguration.Downloads => throw new NotImplementedException();

        Dictionary<string, string> IWinSWConfiguration.EnvironmentVariables => throw new NotImplementedException();

        bool IWinSWConfiguration.BeepOnShutdown => throw new NotImplementedException();

        XmlNode? IWinSWConfiguration.ExtensionsConfiguration => throw new NotImplementedException();
    }
}

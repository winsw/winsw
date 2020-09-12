using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.ServiceProcess;
using System.Text;
using System.Xml;
using WinSW.Configuration;
using WinSW.Native;
using WinSW.Util;
using Names = WinSW.Configuration.SettingNames;

namespace WinSW
{
    /// <summary>
    /// In-memory representation of the configuration file.
    /// </summary>
    public class XmlServiceConfig : ServiceConfig
    {
        protected readonly XmlDocument dom = new();

        private readonly XmlNode root;
        private readonly Dictionary<string, string> environmentVariables;

        internal static XmlServiceConfig? TestConfig;

        public override string FullPath { get; }

        /// <summary>
        /// Where did we find the configuration file?
        ///
        /// This string is "c:\abc\def\ghi" when the configuration XML is "c:\abc\def\ghi.xml"
        /// </summary>
        public virtual string BasePath { get; }

        /// <summary>
        /// The file name portion of the configuration file.
        ///
        /// In the above example, this would be "ghi".
        /// </summary>
        public virtual string BaseName { get; set; }

        /// <exception cref="FileNotFoundException" />
        public XmlServiceConfig(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(null, path);
            }

            string fullPath = this.FullPath = Path.GetFullPath(path);
            string baseName = this.BaseName = Path.GetFileNameWithoutExtension(path);
            string baseDir = Path.GetDirectoryName(fullPath)!;

            this.BasePath = Path.Combine(baseDir, baseName);

            try
            {
                this.dom.Load(path);
            }
            catch (XmlException e)
            {
                throw new InvalidDataException(e.Message, e);
            }

            this.root = this.dom.SelectSingleNode(Names.Service) ?? throw new InvalidDataException("<" + Names.Service + "> is missing in configuration XML");

            // register the base directory as environment variable so that future expansions can refer to this.
            Environment.SetEnvironmentVariable("BASE", baseDir);

            // ditto for ID
            Environment.SetEnvironmentVariable("SERVICE_ID", this.Name);

            // New name
            Environment.SetEnvironmentVariable(WinSWSystem.EnvVarNameExecutablePath, this.ExecutablePath);

            // Also inject system environment variables
            Environment.SetEnvironmentVariable(WinSWSystem.EnvVarNameServiceId, this.Name);

            this.environmentVariables = this.LoadEnvironmentVariables();
        }

        /// <summary>
        /// Loads config from existing DOM
        /// </summary>
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public XmlServiceConfig(XmlDocument dom)
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        {
            this.dom = dom;
            this.root = this.dom.SelectSingleNode(Names.Service) ?? throw new InvalidDataException("<" + Names.Service + "> is missing in configuration XML");
            this.environmentVariables = this.LoadEnvironmentVariables();
        }

        public static XmlServiceConfig FromXml(string xml)
        {
            var dom = new XmlDocument();
            dom.LoadXml(xml);
            return new XmlServiceConfig(dom);
        }

        private static int SingleIntElement(XmlNode parent, string tagName, int defaultValue)
        {
            var e = parent.SelectSingleNode(tagName);

            return e is null ? defaultValue : int.Parse(e.InnerText, NumberFormatInfo.InvariantInfo);
        }

        private static TimeSpan ParseTimeSpan(string v)
        {
            v = v.Trim();
            foreach (var s in Suffix)
            {
                if (v.EndsWith(s.Key))
                {
                    return TimeSpan.FromMilliseconds(int.Parse(v.Substring(0, v.Length - s.Key.Length).Trim(), NumberFormatInfo.InvariantInfo) * s.Value);
                }
            }

            return TimeSpan.FromMilliseconds(int.Parse(v, NumberFormatInfo.InvariantInfo));
        }

        private string SingleElement(string tagName)
        {
            return this.SingleElementOrNull(tagName) ?? throw new InvalidDataException("<" + tagName + "> is missing in configuration XML");
        }

        private string? SingleElementOrNull(string tagName)
        {
            var n = this.root.SelectSingleNode(tagName);
            return n is null ? null : Environment.ExpandEnvironmentVariables(n.InnerText);
        }

        private bool SingleBoolElementOrDefault(string tagName, bool defaultValue)
        {
            var e = this.root.SelectSingleNode(tagName);

            return e is null ? defaultValue : bool.Parse(e.InnerText);
        }

        private TimeSpan SingleTimeSpanElement(XmlNode parent, string tagName, TimeSpan defaultValue)
        {
            string? value = this.SingleElementOrNull(tagName);
            return value is null ? defaultValue : ParseTimeSpan(value);
        }

        private static readonly Dictionary<string, long> Suffix = new()
        {
            { "ms",     1 },
            { "sec",    1000L },
            { "secs",   1000L },
            { "min",    1000L * 60L },
            { "mins",   1000L * 60L },
            { "hr",     1000L * 60L * 60L },
            { "hrs",    1000L * 60L * 60L },
            { "hour",   1000L * 60L * 60L },
            { "hours",  1000L * 60L * 60L },
            { "day",    1000L * 60L * 60L * 24L },
            { "days",   1000L * 60L * 60L * 24L }
        };

        /// <summary>
        /// Path to the executable.
        /// </summary>
        public override string Executable => this.SingleElement("executable");

        public override bool HideWindow => this.SingleBoolElementOrDefault("hidewindow", base.HideWindow);

        /// <summary>
        /// Optionally specify a different Path to an executable to shutdown the service.
        /// </summary>
        public override string? StopExecutable => this.SingleElementOrNull("stopexecutable");

        /// <summary>
        /// The <c>arguments</c> element.
        /// </summary>
        public override string Arguments => this.SingleElementOrNull("arguments") ?? base.Arguments;

        /// <summary>
        /// The <c>startarguments</c> element.
        /// </summary>
        public override string? StartArguments => this.SingleElementOrNull("startarguments");

        /// <summary>
        /// The <c>stoparguments</c> element.
        /// </summary>
        public override string? StopArguments => this.SingleElementOrNull("stoparguments");

        public ProcessCommand Prestart => this.GetProcessCommand(Names.Prestart);

        public ProcessCommand Poststart => this.GetProcessCommand(Names.Poststart);

        public ProcessCommand Prestop => this.GetProcessCommand(Names.Prestop);

        public ProcessCommand Poststop => this.GetProcessCommand(Names.Poststop);

        public override string WorkingDirectory
        {
            get
            {
                string? wd = this.SingleElementOrNull("workingdirectory");
                return string.IsNullOrEmpty(wd) ? base.WorkingDirectory : wd!;
            }
        }

        public List<string> ExtensionIds
        {
            get
            {
                var argumentNode = this.ExtensionsConfiguration;
                var extensions = argumentNode?.SelectNodes("extension");
                if (extensions is null)
                {
                    return new List<string>(0);
                }

                var result = new List<string>(extensions.Count);
                for (int i = 0; i < extensions.Count; i++)
                {
                    result.Add(XmlHelper.SingleAttribute<string>((XmlElement)extensions[i]!, "id"));
                }

                return result;
            }
        }

        public override XmlNode? ExtensionsConfiguration => this.root.SelectSingleNode("extensions");

        /// <summary>
        /// LogDirectory is the service wrapper executable directory or the optionally specified logpath element.
        /// </summary>
        public override string LogDirectory => this.SingleElementOrNull("logpath") ?? base.LogDirectory;

        public override string LogMode
        {
            get
            {
                string? mode = null;

                // first, backward compatibility with older configuration
                var e = (XmlElement?)this.root.SelectSingleNode("logmode");
                if (e != null)
                {
                    mode = e.InnerText;
                }
                else
                {
                    // this is more modern way, to support nested elements as configuration
                    e = (XmlElement?)this.root.SelectSingleNode("log");
                    if (e != null)
                    {
                        mode = e.GetAttribute("mode");
                    }
                }

                return mode ?? base.LogMode;
            }
        }

        public string LogName => this.SingleElementOrNull("logname") ?? this.BaseName;

        public override bool OutFileDisabled => this.SingleBoolElementOrDefault("outfiledisabled", base.OutFileDisabled);

        public override bool ErrFileDisabled => this.SingleBoolElementOrDefault("errfiledisabled", base.ErrFileDisabled);

        public override string OutFilePattern => this.SingleElementOrNull("outfilepattern") ?? base.OutFilePattern;

        public override string ErrFilePattern => this.SingleElementOrNull("errfilepattern") ?? base.ErrFilePattern;

        public LogHandler LogHandler
        {
            get
            {
                var e = (XmlElement?)this.root.SelectSingleNode("logmode");

                // this is more modern way, to support nested elements as configuration
                e ??= (XmlElement?)this.root.SelectSingleNode("log")!; // WARNING: NRE

                int sizeThreshold;
                switch (this.LogMode)
                {
                    case "rotate":
                        return new SizeBasedRollingLogAppender(this.LogDirectory, this.LogName, this.OutFileDisabled, this.ErrFileDisabled, this.OutFilePattern, this.ErrFilePattern);

                    case "none":
                        return new IgnoreLogAppender();

                    case "reset":
                        return new ResetLogAppender(this.LogDirectory, this.LogName, this.OutFileDisabled, this.ErrFileDisabled, this.OutFilePattern, this.ErrFilePattern);

                    case "roll":
                        return new RollingLogAppender(this.LogDirectory, this.LogName, this.OutFileDisabled, this.ErrFileDisabled, this.OutFilePattern, this.ErrFilePattern);

                    case "roll-by-time":
                        var patternNode = e.SelectSingleNode("pattern");
                        if (patternNode is null)
                        {
                            throw new InvalidDataException("Time Based rolling policy is specified but no pattern can be found in configuration XML.");
                        }

                        string? pattern = patternNode.InnerText;
                        int period = SingleIntElement(e, "period", 1);
                        return new TimeBasedRollingLogAppender(this.LogDirectory, this.LogName, this.OutFileDisabled, this.ErrFileDisabled, this.OutFilePattern, this.ErrFilePattern, pattern, period);

                    case "roll-by-size":
                        sizeThreshold = SingleIntElement(e, "sizeThreshold", 10 * 1024) * SizeBasedRollingLogAppender.BytesPerKB;
                        int keepFiles = SingleIntElement(e, "keepFiles", SizeBasedRollingLogAppender.DefaultFilesToKeep);
                        return new SizeBasedRollingLogAppender(this.LogDirectory, this.LogName, this.OutFileDisabled, this.ErrFileDisabled, this.OutFilePattern, this.ErrFilePattern, sizeThreshold, keepFiles);

                    case "append":
                        return new DefaultLogAppender(this.LogDirectory, this.LogName, this.OutFileDisabled, this.ErrFileDisabled, this.OutFilePattern, this.ErrFilePattern);

                    case "roll-by-size-time":
                        sizeThreshold = SingleIntElement(e, "sizeThreshold", 10 * 1024) * RollingSizeTimeLogAppender.BytesPerKB;
                        var filePatternNode = e.SelectSingleNode("pattern");
                        if (filePatternNode is null)
                        {
                            throw new InvalidDataException("Roll-Size-Time Based rolling policy is specified but no pattern can be found in configuration XML.");
                        }

                        var autoRollAtTimeNode = e.SelectSingleNode("autoRollAtTime");
                        TimeSpan? autoRollAtTime = null;
                        if (autoRollAtTimeNode != null)
                        {
                            // validate it
                            if (!TimeSpan.TryParse(autoRollAtTimeNode.InnerText, out var autoRollAtTimeValue))
                            {
                                throw new InvalidDataException("Roll-Size-Time Based rolling policy is specified but autoRollAtTime does not match the TimeSpan format HH:mm:ss found in configuration XML.");
                            }

                            autoRollAtTime = autoRollAtTimeValue;
                        }

                        var zipolderthannumdaysNode = e.SelectSingleNode("zipOlderThanNumDays");
                        int? zipolderthannumdays = null;
                        if (zipolderthannumdaysNode != null)
                        {
                            // validate it
                            if (!int.TryParse(zipolderthannumdaysNode.InnerText, out int zipolderthannumdaysValue))
                            {
                                throw new InvalidDataException("Roll-Size-Time Based rolling policy is specified but zipOlderThanNumDays does not match the int format found in configuration XML.");
                            }

                            zipolderthannumdays = zipolderthannumdaysValue;
                        }

                        var zipdateformatNode = e.SelectSingleNode("zipDateFormat");
                        string zipdateformat = zipdateformatNode is null ? "yyyyMM" : zipdateformatNode.InnerText;

                        return new RollingSizeTimeLogAppender(this.LogDirectory, this.LogName, this.OutFileDisabled, this.ErrFileDisabled, this.OutFilePattern, this.ErrFilePattern, sizeThreshold, filePatternNode.InnerText, autoRollAtTime, zipolderthannumdays, zipdateformat);

                    default:
                        throw new InvalidDataException("Undefined logging mode: " + this.LogMode);
                }
            }
        }

        /// <summary>
        /// Optionally specified depend services that must start before this service starts.
        /// </summary>
        public override string[] ServiceDependencies
        {
            get
            {
                var nodeList = this.root.SelectNodes("depend");
                if (nodeList is null)
                {
                    return base.ServiceDependencies;
                }

                string[] serviceDependencies = new string[nodeList.Count];
                for (int i = 0; i < nodeList.Count; i++)
                {
                    serviceDependencies[i] = nodeList[i]!.InnerText;
                }

                return serviceDependencies;
            }
        }

        public override string Name => this.SingleElement("id");

        public override string DisplayName => this.SingleElementOrNull("name") ?? base.DisplayName;

        public override string Description => this.SingleElementOrNull("description") ?? base.Description;

        /// <summary>
        /// Start mode of the Service
        /// </summary>
        public override ServiceStartMode StartMode
        {
            get
            {
                string? p = this.SingleElementOrNull("startmode");
                if (p is null)
                {
                    return base.StartMode;
                }

                try
                {
                    return (ServiceStartMode)Enum.Parse(typeof(ServiceStartMode), p, true);
                }
                catch (ArgumentException e)
                {
                    var builder = new StringBuilder();
                    builder.AppendLine("Start mode in XML must be one of the following:");
                    foreach (string sm in Enum.GetNames(typeof(ServiceStartMode)))
                    {
                        builder.AppendLine(sm);
                    }

                    throw new InvalidDataException(builder.ToString(), e);
                }
            }
        }

        /// <summary>
        /// True if the service should be installed with the DelayedAutoStart flag.
        /// This setting will be applyed only during the install command and only when the Automatic start mode is configured.
        /// </summary>
        public override bool DelayedAutoStart => this.SingleBoolElementOrDefault("delayedAutoStart", base.DelayedAutoStart);

        public override bool Preshutdown => this.SingleBoolElementOrDefault("preshutdown", base.Preshutdown);

        public TimeSpan? PreshutdownTimeout
        {
            get
            {
                string? value = this.SingleElementOrNull("preshutdownTimeout");
                return value is null ? default : ParseTimeSpan(value);
            }
        }

        /// <summary>
        /// True if the service should beep when finished on shutdown.
        /// This doesn't work on some OSes. See http://msdn.microsoft.com/en-us/library/ms679277%28VS.85%29.aspx
        /// </summary>
        public override bool BeepOnShutdown => this.SingleBoolElementOrDefault("beeponshutdown", base.DelayedAutoStart);

        /// <summary>
        /// True if the service can interact with the desktop.
        /// </summary>
        public override bool Interactive => this.SingleBoolElementOrDefault("interactive", base.DelayedAutoStart);

        /// <summary>
        /// Environment variable overrides
        /// </summary>
        public override Dictionary<string, string> EnvironmentVariables => this.environmentVariables;

        /// <summary>
        /// List of downloads to be performed by the wrapper before starting
        /// a service.
        /// </summary>
        public override List<Download> Downloads
        {
            get
            {
                var nodeList = this.root.SelectNodes("download");
                if (nodeList is null)
                {
                    return base.Downloads;
                }

                var result = new List<Download>(nodeList.Count);
                for (int i = 0; i < nodeList.Count; i++)
                {
                    if (nodeList[i] is XmlElement element)
                    {
                        result.Add(new Download(element));
                    }
                }

                return result;
            }
        }

        public override SC_ACTION[] FailureActions
        {
            get
            {
                var childNodes = this.root.SelectNodes("onfailure");
                if (childNodes is null)
                {
                    return Array.Empty<SC_ACTION>();
                }

                var result = new SC_ACTION[childNodes.Count];
                for (int i = 0; i < childNodes.Count; i++)
                {
                    var node = childNodes[i]!;
                    string action = node.Attributes!["action"]?.Value ?? throw new InvalidDataException("'action' is missing");
                    var type = action switch
                    {
                        "restart" => SC_ACTION_TYPE.SC_ACTION_RESTART,
                        "none" => SC_ACTION_TYPE.SC_ACTION_NONE,
                        "reboot" => SC_ACTION_TYPE.SC_ACTION_REBOOT,
                        _ => throw new Exception("Invalid failure action: " + action)
                    };
                    var delay = node.Attributes["delay"];
                    result[i] = new SC_ACTION(type, delay != null ? ParseTimeSpan(delay.Value) : TimeSpan.Zero);
                }

                return result;
            }
        }

        public override TimeSpan ResetFailureAfter => this.SingleTimeSpanElement(this.root, "resetfailure", base.ResetFailureAfter);

        protected string? GetServiceAccountPart(string subNodeName)
        {
            var node = this.root.SelectSingleNode("serviceaccount");

            if (node != null)
            {
                var subNode = node.SelectSingleNode(subNodeName);
                if (subNode != null)
                {
                    return subNode.InnerText;
                }
            }

            return null;
        }

        public string? ServiceAccountPrompt => this.GetServiceAccountPart("prompt")?.ToLowerInvariant();

        protected string? AllowServiceLogon => this.GetServiceAccountPart("allowservicelogon");

        public override string? ServiceAccountPassword => this.GetServiceAccountPart("password");

        public override string? ServiceAccountUserName => this.GetServiceAccountPart("username");

        public bool HasServiceAccount()
        {
            return this.root.SelectSingleNode("serviceaccount") != null;
        }

        public override bool AllowServiceAcountLogonRight
        {
            get
            {
                if (this.AllowServiceLogon != null)
                {
                    if (bool.TryParse(this.AllowServiceLogon, out bool parsedvalue))
                    {
                        return parsedvalue;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Time to wait for the service to gracefully shutdown the executable before we forcibly kill it
        /// </summary>
        public override TimeSpan StopTimeout => this.SingleTimeSpanElement(this.root, "stoptimeout", base.StopTimeout);

        public int StopTimeoutInMs => (int)this.StopTimeout.TotalMilliseconds;

        /// <summary>
        /// Desired process priority or null if not specified.
        /// </summary>
        public override ProcessPriorityClass Priority
        {
            get
            {
                string? p = this.SingleElementOrNull("priority");
                if (p is null)
                {
                    return base.Priority;
                }

                return (ProcessPriorityClass)Enum.Parse(typeof(ProcessPriorityClass), p, true);
            }
        }

        public string? SecurityDescriptor => this.SingleElementOrNull("securityDescriptor");

        public bool AutoRefresh => this.SingleBoolElementOrDefault("autoRefresh", true);

        public override List<SharedDirectoryMapperConfig> SharedDirectories
        {
            get
            {
                var mapNodes = this.root.SelectSingleNode("sharedDirectoryMapping")?.SelectNodes("map");
                if (mapNodes is null)
                {
                    return new();
                }

                var result = new List<SharedDirectoryMapperConfig>(mapNodes.Count);
                for (int i = 0; i < mapNodes.Count; i++)
                {
                    if (mapNodes[i] is XmlElement mapElement)
                    {
                        string label = XmlHelper.SingleAttribute<string>(mapElement, "label");
                        string uncPath = XmlHelper.SingleAttribute<string>(mapElement, "uncpath");
                        result.Add(new(label, uncPath));
                    }
                }

                return result;
            }
        }

        private Dictionary<string, string> LoadEnvironmentVariables()
        {
            var nodeList = this.root.SelectNodes("env")!;
            var environment = new Dictionary<string, string>(nodeList.Count);
            for (int i = 0; i < nodeList.Count; i++)
            {
                var node = nodeList[i]!;
                string key = node.Attributes!["name"]?.Value ?? throw new InvalidDataException("'name' is missing");
                string value = Environment.ExpandEnvironmentVariables(node.Attributes["value"]?.Value ?? throw new InvalidDataException("'value' is missing"));
                environment[key] = value;

                Environment.SetEnvironmentVariable(key, value);
            }

            return environment;
        }

        private ProcessCommand GetProcessCommand(string name)
        {
            var node = this.root.SelectSingleNode(name);
            return node is null ? default : new ProcessCommand
            {
                Executable = GetInnerText(Names.Executable),
                Arguments = GetInnerText(Names.Arguments),
                StdoutPath = GetInnerText(Names.StdoutPath),
                StderrPath = GetInnerText(Names.StderrPath),
            };

            string? GetInnerText(string name)
            {
                string? text = node.SelectSingleNode(name)?.InnerText;
                return text is null ? null : Environment.ExpandEnvironmentVariables(text);
            }
        }
    }
}

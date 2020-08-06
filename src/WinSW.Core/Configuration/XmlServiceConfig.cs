using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        protected readonly XmlDocument dom = new XmlDocument();

        private readonly Dictionary<string, string> environmentVariables;

        internal static XmlServiceConfig? TestConfig;

        public override string FullPath { get; }

        /// <summary>
        /// Where did we find the configuration file?
        ///
        /// This string is "c:\abc\def\ghi" when the configuration XML is "c:\abc\def\ghi.xml"
        /// </summary>
        public string BasePath { get; set; }

        /// <summary>
        /// The file name portion of the configuration file.
        ///
        /// In the above example, this would be "ghi".
        /// </summary>
        public string BaseName { get; set; }

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

            this.environmentVariables = this.LoadEnvironmentVariables();
        }

        public static XmlServiceConfig FromXml(string xml)
        {
            var dom = new XmlDocument();
            dom.LoadXml(xml);
            return new XmlServiceConfig(dom);
        }

        private string SingleElement(string tagName)
        {
            return this.SingleElement(tagName, false)!;
        }

        private string? SingleElement(string tagName, bool optional)
        {
            XmlNode? n = this.dom.SelectSingleNode("//" + tagName);
            if (n is null && !optional)
            {
                throw new InvalidDataException("<" + tagName + "> is missing in configuration XML");
            }

            return n is null ? null : Environment.ExpandEnvironmentVariables(n.InnerText);
        }

        private bool SingleBoolElement(string tagName, bool defaultValue)
        {
            XmlNode? e = this.dom.SelectSingleNode("//" + tagName);

            return e is null ? defaultValue : bool.Parse(e.InnerText);
        }

        private int SingleIntElement(XmlNode parent, string tagName, int defaultValue)
        {
            XmlNode? e = parent.SelectSingleNode(tagName);

            return e is null ? defaultValue : int.Parse(e.InnerText);
        }

        private TimeSpan SingleTimeSpanElement(XmlNode parent, string tagName, TimeSpan defaultValue)
        {
            string? value = this.SingleElement(tagName, true);
            return value is null ? defaultValue : this.ParseTimeSpan(value);
        }

        private TimeSpan ParseTimeSpan(string v)
        {
            v = v.Trim();
            foreach (var s in Suffix)
            {
                if (v.EndsWith(s.Key))
                {
                    return TimeSpan.FromMilliseconds(int.Parse(v.Substring(0, v.Length - s.Key.Length).Trim()) * s.Value);
                }
            }

            return TimeSpan.FromMilliseconds(int.Parse(v));
        }

        private static readonly Dictionary<string, long> Suffix = new Dictionary<string, long>
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

        public override bool HideWindow => this.SingleBoolElement("hidewindow", base.HideWindow);

        /// <summary>
        /// Optionally specify a different Path to an executable to shutdown the service.
        /// </summary>
        public override string? StopExecutable => this.SingleElement("stopexecutable", true);

        /// <summary>
        /// The <c>arguments</c> element.
        /// </summary>
        public override string Arguments
        {
            get
            {
                XmlNode? argumentsNode = this.dom.SelectSingleNode("//arguments");
                return argumentsNode is null ? base.Arguments : Environment.ExpandEnvironmentVariables(argumentsNode.InnerText);
            }
        }

        /// <summary>
        /// The <c>startarguments</c> element.
        /// </summary>
        public override string? StartArguments
        {
            get
            {
                XmlNode? startArgumentsNode = this.dom.SelectSingleNode("//startarguments");
                return startArgumentsNode is null ? null : Environment.ExpandEnvironmentVariables(startArgumentsNode.InnerText);
            }
        }

        /// <summary>
        /// The <c>stoparguments</c> element.
        /// </summary>
        public override string? StopArguments
        {
            get
            {
                XmlNode? stopArgumentsNode = this.dom.SelectSingleNode("//stoparguments");
                return stopArgumentsNode is null ? null : Environment.ExpandEnvironmentVariables(stopArgumentsNode.InnerText);
            }
        }

        public ProcessCommand Prestart => this.GetProcessCommand(Names.Prestart);

        public ProcessCommand Poststart => this.GetProcessCommand(Names.Poststart);

        public ProcessCommand Prestop => this.GetProcessCommand(Names.Prestop);

        public ProcessCommand Poststop => this.GetProcessCommand(Names.Poststop);

        public override string WorkingDirectory
        {
            get
            {
                var wd = this.SingleElement("workingdirectory", true);
                return string.IsNullOrEmpty(wd) ? base.WorkingDirectory : wd!;
            }
        }

        public List<string> ExtensionIds
        {
            get
            {
                XmlNode? argumentNode = this.ExtensionsConfiguration;
                XmlNodeList? extensions = argumentNode?.SelectNodes("extension");
                if (extensions is null)
                {
                    return new List<string>(0);
                }

                List<string> result = new List<string>(extensions.Count);
                for (int i = 0; i < extensions.Count; i++)
                {
                    result.Add(XmlHelper.SingleAttribute<string>((XmlElement)extensions[i], "id"));
                }

                return result;
            }
        }

        public override XmlNode? ExtensionsConfiguration => this.dom.SelectSingleNode("//extensions");

        /// <summary>
        /// Combines the contents of all the elements of the given name,
        /// or return null if no element exists. Handles whitespace quotation.
        /// </summary>
        private string? AppendTags(string tagName, string? defaultValue = null)
        {
            XmlNode? argumentNode = this.dom.SelectSingleNode("//" + tagName);
            if (argumentNode is null)
            {
                return defaultValue;
            }

            StringBuilder arguments = new StringBuilder();

            XmlNodeList argumentNodeList = this.dom.SelectNodes("//" + tagName);
            for (int i = 0; i < argumentNodeList.Count; i++)
            {
                arguments.Append(' ');

                string token = Environment.ExpandEnvironmentVariables(argumentNodeList[i].InnerText);

                if (token.StartsWith("\"") && token.EndsWith("\""))
                {
                    // for backward compatibility, if the argument is already quoted, leave it as is.
                    // in earlier versions we didn't handle quotation, so the user might have worked
                    // around it by themselves
                }
                else
                {
                    if (token.Contains(" "))
                    {
                        arguments.Append('"').Append(token).Append('"');
                        continue;
                    }
                }

                arguments.Append(token);
            }

            return arguments.ToString();
        }

        /// <summary>
        /// LogDirectory is the service wrapper executable directory or the optionally specified logpath element.
        /// </summary>
        public override string LogDirectory
        {
            get
            {
                XmlNode? loggingNode = this.dom.SelectSingleNode("//logpath");

                return loggingNode is null
                    ? base.LogDirectory
                    : Environment.ExpandEnvironmentVariables(loggingNode.InnerText);
            }
        }

        public override string LogMode
        {
            get
            {
                string? mode = null;

                // first, backward compatibility with older configuration
                XmlElement? e = (XmlElement?)this.dom.SelectSingleNode("//logmode");
                if (e != null)
                {
                    mode = e.InnerText;
                }
                else
                {
                    // this is more modern way, to support nested elements as configuration
                    e = (XmlElement?)this.dom.SelectSingleNode("//log");
                    if (e != null)
                    {
                        mode = e.GetAttribute("mode");
                    }
                }

                return mode ?? base.LogMode;
            }
        }

        public string LogName
        {
            get
            {
                XmlNode? loggingName = this.dom.SelectSingleNode("//logname");

                return loggingName is null ? this.BaseName : Environment.ExpandEnvironmentVariables(loggingName.InnerText);
            }
        }

        public override bool OutFileDisabled => this.SingleBoolElement("outfiledisabled", base.OutFileDisabled);

        public override bool ErrFileDisabled => this.SingleBoolElement("errfiledisabled", base.ErrFileDisabled);

        public override string OutFilePattern
        {
            get
            {
                XmlNode? loggingName = this.dom.SelectSingleNode("//outfilepattern");

                return loggingName is null ? base.OutFilePattern : Environment.ExpandEnvironmentVariables(loggingName.InnerText);
            }
        }

        public override string ErrFilePattern
        {
            get
            {
                XmlNode? loggingName = this.dom.SelectSingleNode("//errfilepattern");

                return loggingName is null ? base.ErrFilePattern : Environment.ExpandEnvironmentVariables(loggingName.InnerText);
            }
        }

        public LogHandler LogHandler
        {
            get
            {
                XmlElement? e = (XmlElement?)this.dom.SelectSingleNode("//logmode");

                // this is more modern way, to support nested elements as configuration
                e ??= (XmlElement?)this.dom.SelectSingleNode("//log")!; // WARNING: NRE

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
                        XmlNode? patternNode = e.SelectSingleNode("pattern");
                        if (patternNode is null)
                        {
                            throw new InvalidDataException("Time Based rolling policy is specified but no pattern can be found in configuration XML.");
                        }

                        var pattern = patternNode.InnerText;
                        int period = this.SingleIntElement(e, "period", 1);
                        return new TimeBasedRollingLogAppender(this.LogDirectory, this.LogName, this.OutFileDisabled, this.ErrFileDisabled, this.OutFilePattern, this.ErrFilePattern, pattern, period);

                    case "roll-by-size":
                        sizeThreshold = this.SingleIntElement(e, "sizeThreshold", 10 * 1024) * SizeBasedRollingLogAppender.BytesPerKB;
                        int keepFiles = this.SingleIntElement(e, "keepFiles", SizeBasedRollingLogAppender.DefaultFilesToKeep);
                        return new SizeBasedRollingLogAppender(this.LogDirectory, this.LogName, this.OutFileDisabled, this.ErrFileDisabled, this.OutFilePattern, this.ErrFilePattern, sizeThreshold, keepFiles);

                    case "append":
                        return new DefaultLogAppender(this.LogDirectory, this.LogName, this.OutFileDisabled, this.ErrFileDisabled, this.OutFilePattern, this.ErrFilePattern);

                    case "roll-by-size-time":
                        sizeThreshold = this.SingleIntElement(e, "sizeThreshold", 10 * 1024) * RollingSizeTimeLogAppender.BytesPerKB;
                        XmlNode? filePatternNode = e.SelectSingleNode("pattern");
                        if (filePatternNode is null)
                        {
                            throw new InvalidDataException("Roll-Size-Time Based rolling policy is specified but no pattern can be found in configuration XML.");
                        }

                        XmlNode? autoRollAtTimeNode = e.SelectSingleNode("autoRollAtTime");
                        TimeSpan? autoRollAtTime = null;
                        if (autoRollAtTimeNode != null)
                        {
                            // validate it
                            if (!TimeSpan.TryParse(autoRollAtTimeNode.InnerText, out TimeSpan autoRollAtTimeValue))
                            {
                                throw new InvalidDataException("Roll-Size-Time Based rolling policy is specified but autoRollAtTime does not match the TimeSpan format HH:mm:ss found in configuration XML.");
                            }

                            autoRollAtTime = autoRollAtTimeValue;
                        }

                        XmlNode? zipolderthannumdaysNode = e.SelectSingleNode("zipOlderThanNumDays");
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

                        XmlNode? zipdateformatNode = e.SelectSingleNode("zipDateFormat");
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
                XmlNodeList? nodeList = this.dom.SelectNodes("//depend");
                if (nodeList is null)
                {
                    return base.ServiceDependencies;
                }

                string[] serviceDependencies = new string[nodeList.Count];
                for (int i = 0; i < nodeList.Count; i++)
                {
                    serviceDependencies[i] = nodeList[i].InnerText;
                }

                return serviceDependencies;
            }
        }

        public override string Name => this.SingleElement("id");

        public override string DisplayName => this.SingleElement("name", true) ?? base.DisplayName;

        public override string Description => this.SingleElement("description", true) ?? base.Description;

        /// <summary>
        /// Start mode of the Service
        /// </summary>
        public override ServiceStartMode StartMode
        {
            get
            {
                string? p = this.SingleElement("startmode", true);
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
                    StringBuilder builder = new StringBuilder();
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
        public override bool DelayedAutoStart => this.SingleBoolElement("delayedAutoStart", base.DelayedAutoStart);

        public override bool Preshutdown => this.SingleBoolElement("preshutdown", base.Preshutdown);

        public TimeSpan? PreshutdownTimeout
        {
            get
            {
                string? value = this.SingleElement("preshutdownTimeout", true);
                return value is null ? default : this.ParseTimeSpan(value);
            }
        }

        /// <summary>
        /// True if the service should beep when finished on shutdown.
        /// This doesn't work on some OSes. See http://msdn.microsoft.com/en-us/library/ms679277%28VS.85%29.aspx
        /// </summary>
        public override bool BeepOnShutdown => this.SingleBoolElement("beeponshutdown", base.DelayedAutoStart);

        /// <summary>
        /// True if the service can interact with the desktop.
        /// </summary>
        public override bool Interactive => this.SingleBoolElement("interactive", base.DelayedAutoStart);

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
                XmlNodeList? nodeList = this.dom.SelectNodes("//download");
                if (nodeList is null)
                {
                    return base.Downloads;
                }

                List<Download> result = new List<Download>(nodeList.Count);
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
                XmlNodeList? childNodes = this.dom.SelectNodes("//onfailure");
                if (childNodes is null)
                {
                    return new SC_ACTION[0];
                }

                SC_ACTION[] result = new SC_ACTION[childNodes.Count];
                for (int i = 0; i < childNodes.Count; i++)
                {
                    XmlNode node = childNodes[i];
                    string action = node.Attributes["action"].Value;
                    SC_ACTION_TYPE type = action switch
                    {
                        "restart" => SC_ACTION_TYPE.SC_ACTION_RESTART,
                        "none" => SC_ACTION_TYPE.SC_ACTION_NONE,
                        "reboot" => SC_ACTION_TYPE.SC_ACTION_REBOOT,
                        _ => throw new Exception("Invalid failure action: " + action)
                    };
                    XmlAttribute? delay = node.Attributes["delay"];
                    result[i] = new SC_ACTION(type, delay != null ? this.ParseTimeSpan(delay.Value) : TimeSpan.Zero);
                }

                return result;
            }
        }

        public override TimeSpan ResetFailureAfter => this.SingleTimeSpanElement(this.dom, "resetfailure", base.ResetFailureAfter);

        protected string? GetServiceAccountPart(string subNodeName)
        {
            XmlNode? node = this.dom.SelectSingleNode("//serviceaccount");

            if (node != null)
            {
                XmlNode? subNode = node.SelectSingleNode(subNodeName);
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
            return this.dom.SelectSingleNode("//serviceaccount") != null;
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
        public override TimeSpan StopTimeout => this.SingleTimeSpanElement(this.dom, "stoptimeout", base.StopTimeout);

        public int StopTimeoutInMs => (int)this.StopTimeout.TotalMilliseconds;

        /// <summary>
        /// Desired process priority or null if not specified.
        /// </summary>
        public override ProcessPriorityClass Priority
        {
            get
            {
                string? p = this.SingleElement("priority", true);
                if (p is null)
                {
                    return base.Priority;
                }

                return (ProcessPriorityClass)Enum.Parse(typeof(ProcessPriorityClass), p, true);
            }
        }

        public string? SecurityDescriptor => this.SingleElement("securityDescriptor", true);

        private Dictionary<string, string> LoadEnvironmentVariables()
        {
            XmlNodeList nodeList = this.dom.SelectNodes("//env");
            Dictionary<string, string> environment = new Dictionary<string, string>(nodeList.Count);
            for (int i = 0; i < nodeList.Count; i++)
            {
                XmlNode node = nodeList[i];
                string key = node.Attributes["name"].Value;
                string value = Environment.ExpandEnvironmentVariables(node.Attributes["value"].Value);
                environment[key] = value;

                Environment.SetEnvironmentVariable(key, value);
            }

            return environment;
        }

        private ProcessCommand GetProcessCommand(string name)
        {
            XmlNode? node = this.dom.SelectSingleNode(Names.Service)?.SelectSingleNode(name);
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

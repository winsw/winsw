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

namespace WinSW
{
    /// <summary>
    /// In-memory representation of the configuration file.
    /// </summary>
    public class XmlServiceConfig : IServiceConfig
    {
        protected readonly XmlDocument dom = new();

        private readonly Dictionary<string, string> environmentVariables;

        public static DefaultSettings Defaults { get; } = new DefaultSettings();

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

        // Currently there is no opportunity to alter the executable path
        public virtual string ExecutablePath => Defaults.ExecutablePath;

        public XmlServiceConfig(string baseName, string directory)
        {
            this.BaseName = baseName;
            this.BasePath = Path.Combine(directory, this.BaseName);

            try
            {
                this.dom.Load(this.BasePath + ".xml");
            }
            catch (XmlException e)
            {
                throw new InvalidDataException(e.Message, e);
            }

            // register the base directory as environment variable so that future expansions can refer to this.
            Environment.SetEnvironmentVariable("BASE", directory);

            // ditto for ID
            Environment.SetEnvironmentVariable("SERVICE_ID", this.Name);

            // New name
            Environment.SetEnvironmentVariable(WinSWSystem.EnvVarNameExecutablePath, this.ExecutablePath);

            // Also inject system environment variables
            Environment.SetEnvironmentVariable(WinSWSystem.EnvVarNameServiceId, this.Name);

            this.environmentVariables = this.LoadEnvironmentVariables();
        }

        /// <summary>
        /// Loads descriptor from existing DOM
        /// </summary>
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public XmlServiceConfig(XmlDocument dom)
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        {
            this.dom = dom;

            this.environmentVariables = this.LoadEnvironmentVariables();
        }

        public static XmlServiceConfig FromXML(string xml)
        {
            var xmlDom = new XmlDocument();
            xmlDom.LoadXml(xml);
            return new XmlServiceConfig(xmlDom);
        }

        private string SingleElement(string tagName)
        {
            return this.SingleElement(tagName, false)!;
        }

        private string? SingleElement(string tagName, bool optional)
        {
            var n = this.dom.SelectSingleNode("//" + tagName);
            if (n is null && !optional)
            {
                throw new InvalidDataException("<" + tagName + "> is missing in configuration XML");
            }

            return n is null ? null : Environment.ExpandEnvironmentVariables(n.InnerText);
        }

        private bool SingleBoolElement(string tagName, bool defaultValue)
        {
            var e = this.dom.SelectSingleNode("//" + tagName);

            return e is null ? defaultValue : bool.Parse(e.InnerText);
        }

        private int SingleIntElement(XmlNode parent, string tagName, int defaultValue)
        {
            var e = parent.SelectSingleNode(tagName);

            return e is null ? defaultValue : int.Parse(e.InnerText);
        }

        private TimeSpan SingleTimeSpanElement(string tagName, TimeSpan defaultValue)
        {
            string? value = this.SingleElement(tagName, true);
            return value is null ? defaultValue : ConfigHelper.ParseTimeSpan(value);
        }

        /// <summary>
        /// Path to the executable.
        /// </summary>
        public string Executable => this.SingleElement("executable");

        public bool HideWindow => this.SingleBoolElement("hidewindow", Defaults.HideWindow);

        /// <summary>
        /// Optionally specify a different Path to an executable to shutdown the service.
        /// </summary>
        public string? StopExecutable => this.SingleElement("stopexecutable", true);

        /// <summary>
        /// <c>arguments</c> or multiple optional <c>argument</c> elements which overrule the arguments element.
        /// </summary>
        public string Arguments
        {
            get
            {
                string? arguments = this.AppendTags("argument", null);

                if (!(arguments is null))
                {
                    return arguments;
                }

                var argumentsNode = this.dom.SelectSingleNode("//arguments");

                return argumentsNode is null ? Defaults.Arguments : Environment.ExpandEnvironmentVariables(argumentsNode.InnerText);
            }
        }

        /// <summary>
        /// <c>startarguments</c> or multiple optional <c>startargument</c> elements.
        /// </summary>
        public string? StartArguments
        {
            get
            {
                string? startArguments = this.AppendTags("startargument", null);

                if (!(startArguments is null))
                {
                    return startArguments;
                }

                var startArgumentsNode = this.dom.SelectSingleNode("//startarguments");

                return startArgumentsNode is null ? null : Environment.ExpandEnvironmentVariables(startArgumentsNode.InnerText);
            }
        }

        /// <summary>
        /// <c>stoparguments</c> or multiple optional <c>stopargument</c> elements.
        /// </summary>
        public string? StopArguments
        {
            get
            {
                string? stopArguments = this.AppendTags("stopargument", null);

                if (!(stopArguments is null))
                {
                    return stopArguments;
                }

                var stopArgumentsNode = this.dom.SelectSingleNode("//stoparguments");

                return stopArgumentsNode is null ? null : Environment.ExpandEnvironmentVariables(stopArgumentsNode.InnerText);
            }
        }

        public string WorkingDirectory
        {
            get
            {
                string? wd = this.SingleElement("workingdirectory", true);
                return string.IsNullOrEmpty(wd) ? Defaults.WorkingDirectory : wd!;
            }
        }

        public List<string> ExtensionIds
        {
            get
            {
                var argumentNode = this.XmlExtensions;
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

        public XmlNode? XmlExtensions => this.dom.SelectSingleNode("//extensions");

        /// <summary>
        /// Combines the contents of all the elements of the given name,
        /// or return null if no element exists. Handles whitespace quotation.
        /// </summary>
        private string? AppendTags(string tagName, string? defaultValue = null)
        {
            var argumentNode = this.dom.SelectSingleNode("//" + tagName);
            if (argumentNode is null)
            {
                return defaultValue;
            }

            var arguments = new StringBuilder();

            var argumentNodeList = this.dom.SelectNodes("//" + tagName)!;
            for (int i = 0; i < argumentNodeList.Count; i++)
            {
                arguments.Append(' ');

                string token = Environment.ExpandEnvironmentVariables(argumentNodeList[i]!.InnerText);

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
        public string LogDirectory => this.Log.Directory;

        public string LogMode
        {
            get
            {
                string? mode = null;

                // first, backward compatibility with older configuration
                var e = (XmlElement?)this.dom.SelectSingleNode("//logmode");
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

                return mode ?? Defaults.LogMode;
            }
        }

        public string LogName
        {
            get
            {
                var loggingName = this.dom.SelectSingleNode("//logname");

                return loggingName is null ? this.BaseName : Environment.ExpandEnvironmentVariables(loggingName.InnerText);
            }
        }

        public Log Log => new XmlLogSettings(this);

        private class XmlLogSettings : Log
        {
            private readonly XmlServiceConfig config;

            public XmlLogSettings(XmlServiceConfig config)
            {
                this.config = config;
            }

            private XmlElement Element
            {
                get
                {
                    var element = (XmlElement?)this.config.dom.SelectSingleNode("//logmode");

                    // this is more modern way, to support nested elements as configuration
                    element ??= (XmlElement?)this.config.dom.SelectSingleNode("//log")!; // WARNING: NRE
                    return element;
                }
            }

            public override string? Mode => this.config.LogMode;

            public override string Name => this.config.LogName;

            public override string Directory
            {
                get
                {
                    var loggingNode = this.config.dom.SelectSingleNode("//logpath");

                    return loggingNode is null
                        ? Defaults.LogDirectory
                        : Environment.ExpandEnvironmentVariables(loggingNode.InnerText);
                }
            }

            public override int? SizeThreshold => this.config.SingleIntElement(this.Element, "sizeThreshold", 10 * 1024);

            public override int? KeepFiles => this.config.SingleIntElement(this.Element, "keepFiles", SizeBasedRollingLogAppender.DefaultFilesToKeep);

            public override int? Period => this.config.SingleIntElement(this.Element, "period", 1);

            public override string Pattern
            {
                get
                {
                    var patternNode = this.Element.SelectSingleNode("pattern");
                    if (patternNode is null)
                    {
                        throw new InvalidDataException("Time Based rolling policy is specified but no pattern can be found in configuration XML.");
                    }

                    return patternNode.InnerText;
                }
            }

            public override bool OutFileDisabled => this.config.SingleBoolElement("outfiledisabled", Defaults.OutFileDisabled);

            public override bool ErrFileDisabled => this.config.SingleBoolElement("errfiledisabled", Defaults.ErrFileDisabled);

            public override string OutFilePattern
            {
                get
                {
                    var loggingName = this.config.dom.SelectSingleNode("//outfilepattern");

                    return loggingName is null ? Defaults.OutFilePattern : Environment.ExpandEnvironmentVariables(loggingName.InnerText);
                }
            }

            public override string ErrFilePattern
            {
                get
                {
                    var loggingName = this.config.dom.SelectSingleNode("//errfilepattern");

                    return loggingName is null ? Defaults.ErrFilePattern : Environment.ExpandEnvironmentVariables(loggingName.InnerText);
                }
            }

            public override string? AutoRollAtTime
            {
                get
                {
                    var autoRollAtTimeNode = this.Element.SelectSingleNode("autoRollAtTime");
                    return autoRollAtTimeNode?.InnerText;
                }
            }

            public override int? ZipOlderThanNumDays
            {
                get
                {
                    var zipolderthannumdaysNode = this.Element.SelectSingleNode("zipOlderThanNumDays");
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

                    return zipolderthannumdays;
                }
            }

            public override string? ZipDateFormat
            {
                get
                {
                    var zipdateformatNode = this.Element.SelectSingleNode("zipDateFormat");
                    return zipdateformatNode is null ? null : zipdateformatNode.InnerText;
                }
            }
        }

        /// <summary>
        /// Optionally specified depend services that must start before this service starts.
        /// </summary>
        public string[] ServiceDependencies
        {
            get
            {
                var nodeList = this.dom.SelectNodes("//depend");
                if (nodeList is null)
                {
                    return Defaults.ServiceDependencies;
                }

                string[] serviceDependencies = new string[nodeList.Count];
                for (int i = 0; i < nodeList.Count; i++)
                {
                    serviceDependencies[i] = nodeList[i]!.InnerText;
                }

                return serviceDependencies;
            }
        }

        public string Name => this.SingleElement("id");

        public string DisplayName => this.SingleElement("name");

        public string Description => this.SingleElement("description");

        /// <summary>
        /// Start mode of the Service
        /// </summary>
        public ServiceStartMode StartMode
        {
            get
            {
                string? p = this.SingleElement("startmode", true);
                if (p is null)
                {
                    return Defaults.StartMode;
                }

                try
                {
                    return (ServiceStartMode)Enum.Parse(typeof(ServiceStartMode), p, true);
                }
                catch
                {
                    Console.WriteLine("Start mode in XML must be one of the following:");
                    foreach (string sm in Enum.GetNames(typeof(ServiceStartMode)))
                    {
                        Console.WriteLine(sm);
                    }

                    throw;
                }
            }
        }

        /// <summary>
        /// True if the service should be installed with the DelayedAutoStart flag.
        /// This setting will be applyed only during the install command and only when the Automatic start mode is configured.
        /// </summary>
        public bool DelayedAutoStart => this.dom.SelectSingleNode("//delayedAutoStart") != null;

        /// <summary>
        /// True if the service should beep when finished on shutdown.
        /// This doesn't work on some OSes. See http://msdn.microsoft.com/en-us/library/ms679277%28VS.85%29.aspx
        /// </summary>
        public bool BeepOnShutdown => this.dom.SelectSingleNode("//beeponshutdown") != null;

        /// <summary>
        /// The estimated time required for a pending stop operation (default 15 secs).
        /// Before the specified amount of time has elapsed, the service should make its next call to the SetServiceStatus function
        /// with either an incremented checkPoint value or a change in currentState. (see http://msdn.microsoft.com/en-us/library/ms685996.aspx)
        /// </summary>
        public TimeSpan WaitHint => this.SingleTimeSpanElement("waithint", Defaults.WaitHint);

        /// <summary>
        /// The time before the service should make its next call to the SetServiceStatus function
        /// with an incremented checkPoint value (default 1 sec).
        /// Do not wait longer than the wait hint. A good interval is one-tenth of the wait hint but not less than 1 second and not more than 10 seconds.
        /// </summary>
        public TimeSpan SleepTime => this.SingleTimeSpanElement("sleeptime", Defaults.SleepTime);

        /// <summary>
        /// True if the service can interact with the desktop.
        /// </summary>
        public bool Interactive => this.dom.SelectSingleNode("//interactive") != null;

        /// <summary>
        /// Environment variable overrides
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables => new(this.environmentVariables);

        /// <summary>
        /// List of downloads to be performed by the wrapper before starting
        /// a service.
        /// </summary>
        public List<Download> Downloads
        {
            get
            {
                var nodeList = this.dom.SelectNodes("//download");
                if (nodeList is null)
                {
                    return Defaults.Downloads;
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

        public SC_ACTION[] FailureActions
        {
            get
            {
                var childNodes = this.dom.SelectNodes("//onfailure");
                if (childNodes is null)
                {
                    return new SC_ACTION[0];
                }

                var result = new SC_ACTION[childNodes.Count];
                for (int i = 0; i < childNodes.Count; i++)
                {
                    var node = childNodes[i]!;
                    string action = node.Attributes!["action"]?.Value ?? throw new InvalidDataException("'action' is missing");
                    var type = action switch
                    {
                        "restart" => SC_ACTION_TYPE.RESTART,
                        "none" => SC_ACTION_TYPE.NONE,
                        "reboot" => SC_ACTION_TYPE.REBOOT,
                        _ => throw new Exception("Invalid failure action: " + action)
                    };
                    var delay = node.Attributes["delay"];
                    result[i] = new SC_ACTION(type, delay != null ? ConfigHelper.ParseTimeSpan(delay.Value) : TimeSpan.Zero);
                }

                return result;
            }
        }

        public TimeSpan ResetFailureAfter => this.SingleTimeSpanElement("resetfailure", Defaults.ResetFailureAfter);

        protected string? GetServiceAccountPart(XmlNode node, string subNodeName)
        {
            var subNode = node.SelectSingleNode(subNodeName);
            if (subNode != null)
            {
                return subNode.InnerText;
            }

            return null;
        }

        private bool ParseAllowServiceAcountLogonRight(string? logonRight)
        {
            if (logonRight != null && bool.TryParse(logonRight, out bool parsedvalue))
            {
                return parsedvalue;
            }

            return false;
        }

        public Configuration.ServiceAccount ServiceAccount
        {
            get
            {
                var node = this.dom.SelectSingleNode("//serviceaccount");

                if (node is null)
                {
                    return Defaults.ServiceAccount;
                }

                var serviceAccount = Defaults.ServiceAccount;

                serviceAccount.Domain = this.GetServiceAccountPart(node, "domain");

                serviceAccount.User = this.GetServiceAccountPart(node, "user");

                serviceAccount.Password = this.GetServiceAccountPart(node, "password");

                string? loginRight = this.GetServiceAccountPart(node, "allowservicelogon");

                serviceAccount.AllowServiceLogonRight = this.ParseAllowServiceAcountLogonRight(loginRight);

                return serviceAccount;
            }
        }

        /// <summary>
        /// Time to wait for the service to gracefully shutdown the executable before we forcibly kill it
        /// </summary>
        public TimeSpan StopTimeout => this.SingleTimeSpanElement("stoptimeout", Defaults.StopTimeout);

        public bool StopParentProcessFirst
        {
            get
            {
                string? value = this.SingleElement("stopparentprocessfirst", true);
                if (bool.TryParse(value, out bool result))
                {
                    return result;
                }

                return Defaults.StopParentProcessFirst;
            }
        }

        /// <summary>
        /// Desired process priority or null if not specified.
        /// </summary>
        public ProcessPriorityClass Priority
        {
            get
            {
                string? p = this.SingleElement("priority", true);
                if (p is null)
                {
                    return Defaults.Priority;
                }

                return (ProcessPriorityClass)Enum.Parse(typeof(ProcessPriorityClass), p, true);
            }
        }

        public string? SecurityDescriptor => this.SingleElement("securityDescriptor", true);

        private Dictionary<string, string> LoadEnvironmentVariables()
        {
            var nodeList = this.dom.SelectNodes("//env")!;
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

        public List<YamlExtensionConfig>? YamlExtensions => Defaults.YamlExtensions;
    }
}

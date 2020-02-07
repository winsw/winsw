using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using winsw.Configuration;
using winsw.Native;
using winsw.Util;
using WMI;

namespace winsw
{
    /// <summary>
    /// In-memory representation of the configuration file.
    /// </summary>
    public class ServiceDescriptor : IWinSWConfiguration
    {
        // ReSharper disable once InconsistentNaming
        protected readonly XmlDocument dom = new XmlDocument();

        public static DefaultWinSWSettings Defaults { get; } = new DefaultWinSWSettings();

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

        public ServiceDescriptor()
        {
            // find co-located configuration xml. We search up to the ancestor directories to simplify debugging,
            // as well as trimming off ".vshost" suffix (which is used during debugging)
            // Get the first parent to go into the recursive loop
            string p = ExecutablePath;
            string baseName = Path.GetFileNameWithoutExtension(p);
            if (baseName.EndsWith(".vshost"))
                baseName = baseName.Substring(0, baseName.Length - 7);

            DirectoryInfo d = new DirectoryInfo(Path.GetDirectoryName(p));
            while (true)
            {
                if (File.Exists(Path.Combine(d.FullName, baseName + ".xml")))
                    break;

                if (d.Parent is null)
                    throw new FileNotFoundException("Unable to locate " + baseName + ".xml file within executable directory or any parents");

                d = d.Parent;
            }

            BaseName = baseName;
            BasePath = Path.Combine(d.FullName, BaseName);

            try
            {
                dom.Load(BasePath + ".xml");
            }
            catch (XmlException e)
            {
                throw new InvalidDataException(e.Message, e);
            }

            // register the base directory as environment variable so that future expansions can refer to this.
            Environment.SetEnvironmentVariable("BASE", d.FullName);

            // ditto for ID
            Environment.SetEnvironmentVariable("SERVICE_ID", Id);

            // New name
            Environment.SetEnvironmentVariable(WinSWSystem.ENVVAR_NAME_EXECUTABLE_PATH, ExecutablePath);

            // Also inject system environment variables
            Environment.SetEnvironmentVariable(WinSWSystem.ENVVAR_NAME_SERVICE_ID, Id);
        }

        /// <summary>
        /// Loads descriptor from existing DOM
        /// </summary>
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public ServiceDescriptor(XmlDocument dom)
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        {
            this.dom = dom;
        }

        // ReSharper disable once InconsistentNaming
        public static ServiceDescriptor FromXML(string xml)
        {
            var dom = new XmlDocument();
            dom.LoadXml(xml);
            return new ServiceDescriptor(dom);
        }

        private string SingleElement(string tagName)
        {
            return SingleElement(tagName, false)!;
        }

        private string? SingleElement(string tagName, bool optional)
        {
            XmlNode? n = dom.SelectSingleNode("//" + tagName);
            if (n is null && !optional)
                throw new InvalidDataException("<" + tagName + "> is missing in configuration XML");

            return n is null ? null : Environment.ExpandEnvironmentVariables(n.InnerText);
        }

        private bool SingleBoolElement(string tagName, bool defaultValue)
        {
            XmlNode? e = dom.SelectSingleNode("//" + tagName);

            return e is null ? defaultValue : bool.Parse(e.InnerText);
        }

        private int SingleIntElement(XmlNode parent, string tagName, int defaultValue)
        {
            XmlNode? e = parent.SelectSingleNode(tagName);

            return e is null ? defaultValue : int.Parse(e.InnerText);
        }

        private TimeSpan SingleTimeSpanElement(XmlNode parent, string tagName, TimeSpan defaultValue)
        {
            string? value = SingleElement(tagName, true);
            return value is null ? defaultValue : ParseTimeSpan(value);
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
        public string Executable => SingleElement("executable");

        public bool HideWindow => SingleBoolElement("hidewindow", Defaults.HideWindow);

        /// <summary>
        /// Optionally specify a different Path to an executable to shutdown the service.
        /// </summary>
        public string? StopExecutable => SingleElement("stopexecutable", true);

        /// <summary>
        /// Arguments or multiple optional argument elements which overrule the arguments element.
        /// </summary>
        public string Arguments
        {
            get
            {
                string? arguments = AppendTags("argument", null);

                if (arguments is null)
                {
                    XmlNode? argumentsNode = dom.SelectSingleNode("//arguments");

                    if (argumentsNode is null)
                    {
                        return Defaults.Arguments;
                    }

                    return Environment.ExpandEnvironmentVariables(argumentsNode.InnerText);
                }
                else
                {
                    return arguments;
                }
            }
        }

        /// <summary>
        /// Multiple optional startargument elements.
        /// </summary>
        public string? Startarguments => AppendTags("startargument", Defaults.Startarguments);

        /// <summary>
        /// Multiple optional stopargument elements.
        /// </summary>
        public string? Stoparguments => AppendTags("stopargument", Defaults.Stoparguments);

        public string WorkingDirectory
        {
            get
            {
                var wd = SingleElement("workingdirectory", true);
                return string.IsNullOrEmpty(wd) ? Defaults.WorkingDirectory : wd!;
            }
        }

        public List<string> ExtensionIds
        {
            get
            {
                XmlNode? argumentNode = ExtensionsConfiguration;
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

        public XmlNode? ExtensionsConfiguration => dom.SelectSingleNode("//extensions");

        /// <summary>
        /// Combines the contents of all the elements of the given name,
        /// or return null if no element exists. Handles whitespace quotation.
        /// </summary>
        private string? AppendTags(string tagName, string? defaultValue = null)
        {
            XmlNode? argumentNode = dom.SelectSingleNode("//" + tagName);
            if (argumentNode is null)
            {
                return defaultValue;
            }

            StringBuilder arguments = new StringBuilder();

            XmlNodeList argumentNodeList = dom.SelectNodes("//" + tagName);
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
        public string LogDirectory
        {
            get
            {
                XmlNode? loggingNode = dom.SelectSingleNode("//logpath");

                return loggingNode is null
                    ? Defaults.LogDirectory
                    : Environment.ExpandEnvironmentVariables(loggingNode.InnerText);
            }
        }

        public string LogMode
        {
            get
            {
                string? mode = null;

                // first, backward compatibility with older configuration
                XmlElement? e = (XmlElement?)dom.SelectSingleNode("//logmode");
                if (e != null)
                {
                    mode = e.InnerText;
                }
                else
                {
                    // this is more modern way, to support nested elements as configuration
                    e = (XmlElement?)dom.SelectSingleNode("//log");
                    if (e != null)
                        mode = e.GetAttribute("mode");
                }

                return mode ?? Defaults.LogMode;
            }
        }

        public string LogName
        {
            get
            {
                XmlNode? loggingName = dom.SelectSingleNode("//logname");

                return loggingName is null ? BaseName : Environment.ExpandEnvironmentVariables(loggingName.InnerText);
            }
        }

        public bool OutFileDisabled => SingleBoolElement("outfiledisabled", Defaults.OutFileDisabled);

        public bool ErrFileDisabled => SingleBoolElement("errfiledisabled", Defaults.ErrFileDisabled);

        public string OutFilePattern
        {
            get
            {
                XmlNode? loggingName = dom.SelectSingleNode("//outfilepattern");

                return loggingName is null ? Defaults.OutFilePattern : Environment.ExpandEnvironmentVariables(loggingName.InnerText);
            }
        }

        public string ErrFilePattern
        {
            get
            {
                XmlNode? loggingName = dom.SelectSingleNode("//errfilepattern");

                return loggingName is null ? Defaults.ErrFilePattern : Environment.ExpandEnvironmentVariables(loggingName.InnerText);
            }
        }

        public LogHandler LogHandler
        {
            get
            {
                XmlElement? e = (XmlElement?)dom.SelectSingleNode("//logmode");

                // this is more modern way, to support nested elements as configuration
                e ??= (XmlElement?)dom.SelectSingleNode("//log")!; // WARNING: NRE

                int sizeThreshold;
                switch (LogMode)
                {
                    case "rotate":
                        return new SizeBasedRollingLogAppender(LogDirectory, LogName, OutFileDisabled, ErrFileDisabled, OutFilePattern, ErrFilePattern);

                    case "none":
                        return new IgnoreLogAppender();

                    case "reset":
                        return new ResetLogAppender(LogDirectory, LogName, OutFileDisabled, ErrFileDisabled, OutFilePattern, ErrFilePattern);

                    case "roll":
                        return new RollingLogAppender(LogDirectory, LogName, OutFileDisabled, ErrFileDisabled, OutFilePattern, ErrFilePattern);

                    case "roll-by-time":
                        XmlNode? patternNode = e.SelectSingleNode("pattern");
                        if (patternNode is null)
                        {
                            throw new InvalidDataException("Time Based rolling policy is specified but no pattern can be found in configuration XML.");
                        }

                        var pattern = patternNode.InnerText;
                        int period = SingleIntElement(e, "period", 1);
                        return new TimeBasedRollingLogAppender(LogDirectory, LogName, OutFileDisabled, ErrFileDisabled, OutFilePattern, ErrFilePattern, pattern, period);

                    case "roll-by-size":
                        sizeThreshold = SingleIntElement(e, "sizeThreshold", 10 * 1024) * SizeBasedRollingLogAppender.BYTES_PER_KB;
                        int keepFiles = SingleIntElement(e, "keepFiles", SizeBasedRollingLogAppender.DEFAULT_FILES_TO_KEEP);
                        return new SizeBasedRollingLogAppender(LogDirectory, LogName, OutFileDisabled, ErrFileDisabled, OutFilePattern, ErrFilePattern, sizeThreshold, keepFiles);

                    case "append":
                        return new DefaultLogAppender(LogDirectory, LogName, OutFileDisabled, ErrFileDisabled, OutFilePattern, ErrFilePattern);

                    case "roll-by-size-time":
                        sizeThreshold = SingleIntElement(e, "sizeThreshold", 10 * 1024) * RollingSizeTimeLogAppender.BYTES_PER_KB;
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
                                throw new InvalidDataException("Roll-Size-Time Based rolling policy is specified but autoRollAtTime does not match the TimeSpan format HH:mm:ss found in configuration XML.");

                            autoRollAtTime = autoRollAtTimeValue;
                        }

                        XmlNode? zipolderthannumdaysNode = e.SelectSingleNode("zipOlderThanNumDays");
                        int? zipolderthannumdays = null;
                        if (zipolderthannumdaysNode != null)
                        {
                            // validate it
                            if (!int.TryParse(zipolderthannumdaysNode.InnerText, out int zipolderthannumdaysValue))
                                throw new InvalidDataException("Roll-Size-Time Based rolling policy is specified but zipOlderThanNumDays does not match the int format found in configuration XML.");

                            zipolderthannumdays = zipolderthannumdaysValue;
                        }

                        XmlNode? zipdateformatNode = e.SelectSingleNode("zipDateFormat");
                        string zipdateformat = zipdateformatNode is null ? "yyyyMM" : zipdateformatNode.InnerText;

                        return new RollingSizeTimeLogAppender(LogDirectory, LogName, OutFileDisabled, ErrFileDisabled, OutFilePattern, ErrFilePattern, sizeThreshold, filePatternNode.InnerText, autoRollAtTime, zipolderthannumdays, zipdateformat);

                    default:
                        throw new InvalidDataException("Undefined logging mode: " + LogMode);
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
                XmlNodeList? nodeList = dom.SelectNodes("//depend");
                if (nodeList is null)
                {
                    return Defaults.ServiceDependencies;
                }

                string[] serviceDependencies = new string[nodeList.Count];
                for (int i = 0; i < nodeList.Count; i++)
                {
                    serviceDependencies[i] = nodeList[i].InnerText;
                }

                return serviceDependencies;
            }
        }

        public string Id => SingleElement("id");

        public string Caption => SingleElement("name");

        public string Description => SingleElement("description");

        /// <summary>
        /// Start mode of the Service
        /// </summary>
        public StartMode StartMode
        {
            get
            {
                string? p = SingleElement("startmode", true);
                if (p is null)
                    return Defaults.StartMode;

                try
                {
                    return (StartMode)Enum.Parse(typeof(StartMode), p, true);
                }
                catch
                {
                    Console.WriteLine("Start mode in XML must be one of the following:");
                    foreach (string sm in Enum.GetNames(typeof(StartMode)))
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
        public bool DelayedAutoStart => dom.SelectSingleNode("//delayedAutoStart") != null;

        /// <summary>
        /// True if the service should beep when finished on shutdown.
        /// This doesn't work on some OSes. See http://msdn.microsoft.com/en-us/library/ms679277%28VS.85%29.aspx
        /// </summary>
        public bool BeepOnShutdown => dom.SelectSingleNode("//beeponshutdown") != null;

        /// <summary>
        /// The estimated time required for a pending stop operation (default 15 secs).
        /// Before the specified amount of time has elapsed, the service should make its next call to the SetServiceStatus function
        /// with either an incremented checkPoint value or a change in currentState. (see http://msdn.microsoft.com/en-us/library/ms685996.aspx)
        /// </summary>
        public TimeSpan WaitHint => SingleTimeSpanElement(dom, "waithint", Defaults.WaitHint);

        /// <summary>
        /// The time before the service should make its next call to the SetServiceStatus function
        /// with an incremented checkPoint value (default 1 sec).
        /// Do not wait longer than the wait hint. A good interval is one-tenth of the wait hint but not less than 1 second and not more than 10 seconds.
        /// </summary>
        public TimeSpan SleepTime => SingleTimeSpanElement(dom, "sleeptime", Defaults.SleepTime);

        /// <summary>
        /// True if the service can interact with the desktop.
        /// </summary>
        public bool Interactive => dom.SelectSingleNode("//interactive") != null;

        /// <summary>
        /// Environment variable overrides
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables
        {
            get
            {
                Dictionary<string, string> map = new Dictionary<string, string>();
                XmlNodeList nodeList = dom.SelectNodes("//env");
                for (int i = 0; i < nodeList.Count; i++)
                {
                    XmlNode node = nodeList[i];
                    string key = node.Attributes["name"].Value;
                    string value = Environment.ExpandEnvironmentVariables(node.Attributes["value"].Value);
                    map[key] = value;

                    Environment.SetEnvironmentVariable(key, value);
                }

                return map;
            }
        }

        /// <summary>
        /// List of downloads to be performed by the wrapper before starting
        /// a service.
        /// </summary>
        public List<Download> Downloads
        {
            get
            {
                XmlNodeList? nodeList = dom.SelectNodes("//download");
                if (nodeList is null)
                {
                    return Defaults.Downloads;
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

        public List<SC_ACTION> FailureActions
        {
            get
            {
                XmlNodeList? childNodes = dom.SelectNodes("//onfailure");
                if (childNodes is null)
                {
                    return new List<SC_ACTION>(0);
                }

                List<SC_ACTION> result = new List<SC_ACTION>(childNodes.Count);
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
                    result.Add(new SC_ACTION(type, delay != null ? ParseTimeSpan(delay.Value) : TimeSpan.Zero));
                }

                return result;
            }
        }

        public TimeSpan ResetFailureAfter => SingleTimeSpanElement(dom, "resetfailure", Defaults.ResetFailureAfter);

        protected string? GetServiceAccountPart(string subNodeName)
        {
            XmlNode? node = dom.SelectSingleNode("//serviceaccount");

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

        protected string? AllowServiceLogon => GetServiceAccountPart("allowservicelogon");

        // ReSharper disable once InconsistentNaming
        protected string? serviceAccountDomain => GetServiceAccountPart("domain");

        // ReSharper disable once InconsistentNaming
        protected string? serviceAccountName => GetServiceAccountPart("user");

        public string? ServiceAccountPassword => GetServiceAccountPart("password");

        public string ServiceAccountUser => (serviceAccountDomain ?? "NULL") + @"\" + (serviceAccountName ?? "NULL");

        public bool HasServiceAccount()
        {
            return !string.IsNullOrEmpty(serviceAccountDomain) && !string.IsNullOrEmpty(serviceAccountName);
        }

        public bool AllowServiceAcountLogonRight
        {
            get
            {
                if (AllowServiceLogon != null)
                {
                    if (bool.TryParse(AllowServiceLogon, out bool parsedvalue))
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
        public TimeSpan StopTimeout => SingleTimeSpanElement(dom, "stoptimeout", Defaults.StopTimeout);

        public bool StopParentProcessFirst
        {
            get
            {
                var value = SingleElement("stopparentprocessfirst", true);
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
                string? p = SingleElement("priority", true);
                if (p is null)
                    return Defaults.Priority;

                return (ProcessPriorityClass)Enum.Parse(typeof(ProcessPriorityClass), p, true);
            }
        }
    }
}

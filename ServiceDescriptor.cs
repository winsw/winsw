using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Xml;

namespace winsw
{
    /// <summary>
    /// In-memory representation of the configuration file.
    /// </summary>
    public class ServiceDescriptor
    {
        protected readonly XmlDocument Dom = new XmlDocument();

        /// <summary>
        /// Where did we find the configuration file?
        /// 
        /// This string is "c:\abc\def\ghi" when the configuration XML is "c:\abc\def\ghi.xml"
        /// </summary>
        public readonly string BasePath;
        /// <summary>
        /// The file name portion of the configuration file.
        /// 
        /// In the above example, this would be "ghi".
        /// </summary>
        public readonly string BaseName;

        public string ExecutablePath
        {
            get
            {
                // this returns the executable name as given by the calling process, so
                // it needs to be absolutized.
                var p = Environment.GetCommandLineArgs()[0];
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, p);

            }
        }

        public ServiceDescriptor()
        {
            // find co-located configuration xml. We search up to the ancestor directories to simplify debugging,
            // as well as trimming off ".vshost" suffix (which is used during debugging)
            var executablePath = ExecutablePath;
            var baseName = Path.GetFileNameWithoutExtension(executablePath);
            if (baseName != null && baseName.EndsWith(".vshost")) baseName = baseName.Substring(0, baseName.Length - 7);
            while (true)
            {
                executablePath = Path.GetDirectoryName(executablePath);
                if (executablePath != null && File.Exists(Path.Combine(executablePath, baseName + ".xml")))
                    break;
            }

            // register the base directory as environment variable so that future expansions can refer to this.
            Environment.SetEnvironmentVariable("BASE", executablePath);

            BaseName = baseName;
            BasePath = Path.Combine(executablePath, BaseName);

            Dom.Load(BasePath + ".xml");
        }

        private string SingleElement(string tagName)
        {
            return SingleElement(tagName, false);
        }

        private string SingleElement(string tagName, Boolean optional)
        {
            var n = Dom.SelectSingleNode("//" + tagName);
            if (n == null && !optional) throw new InvalidDataException("<" + tagName + "> is missing in configuration XML");
            return n == null ? null : Environment.ExpandEnvironmentVariables(n.InnerText);
        }

        private static int SingleIntElement(XmlNode parent, string tagName, int defaultValue)
        {
            var node = parent.SelectSingleNode(tagName);

            return node == null ? defaultValue : int.Parse(node.InnerText);
        }

        private TimeSpan SingleTimeSpanElement(XmlNode parent, string tagName, TimeSpan defaultValue)
        {
            var node = parent.SelectSingleNode(tagName);

            return node == null ? defaultValue : ParseTimeSpan(node.InnerText);
        }

        private static TimeSpan ParseTimeSpan(string v)
        {
            v = v.Trim();
            foreach (var s in SUFFIX)
            {
                if (v.EndsWith(s.Key))
                {
                    return TimeSpan.FromMilliseconds(int.Parse(v.Substring(0, v.Length - s.Key.Length).Trim()) * s.Value);
                }
            }
            return TimeSpan.FromMilliseconds(int.Parse(v));
        }

        private static readonly Dictionary<string,long> SUFFIX = new Dictionary<string,long> {
            { "ms",     1 }, 
            { "sec",    1000L },
            { "secs",   1000L },
            { "min",    1000L*60L },
            { "mins",   1000L*60L },
            { "hr",     1000L*60L*60L },
            { "hrs",    1000L*60L*60L },
            { "hour",   1000L*60L*60L },
            { "hours",  1000L*60L*60L },
            { "day",    1000L*60L*60L*24L },
            { "days",   1000L*60L*60L*24L }
        };

        /// <summary>
        /// Path to the executable.
        /// </summary>
        public string Executable
        {
            get
            {
                return SingleElement("executable");
            }
        }

        /// <summary>
        /// Optionally specify a different Path to an executable to shutdown the service.
        /// </summary>
        public string StopExecutable
        {
            get
            {
                return SingleElement("stopexecutable");
            }
        }

        /// <summary>
        /// Arguments or multiple optional argument elements which overrule the arguments element.
        /// </summary>
        public string Arguments
        {
            get
            {
                var arguments = AppendTags("argument");

                if (arguments != null)
                {
                    return arguments;
                }
                const string TagName = "arguments";
                var argumentsNode = Dom.SelectSingleNode("//" + TagName);

                if (argumentsNode == null)
                {
                    if (AppendTags("startargument") == null)
                    {
                        throw new InvalidDataException("<" + TagName + "> is missing in configuration XML");
                    }
                    return "";
                }

                return Environment.ExpandEnvironmentVariables(argumentsNode.InnerText);
            }
        }

        /// <summary>
        /// Multiple optional startargument elements.
        /// </summary>
        public string Startarguments
        {
            get
            {
                return AppendTags("startargument");
            }
        }

        /// <summary>
        /// Multiple optional stopargument elements.
        /// </summary>
        public string Stoparguments
        {
            get
            {
                return AppendTags("stopargument");
            }
        }

        /// <summary>
        /// Optional working directory.
        /// </summary>
        public string WorkingDirectory {
            get {
                var wd = SingleElement("workingdirectory", true);
                return String.IsNullOrEmpty(wd) ? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) : wd;
            }
        }

        /// <summary>
        /// Combines the contents of all the elements of the given name,
        /// or return null if no element exists. Handles whitespace quotation.
        /// </summary>
        private string AppendTags(string tagName)
        {
            var argumentNode = Dom.SelectSingleNode("//" + tagName);

            if (argumentNode == null)
            {
                return null;
            }
            var arguments = "";

            var xmlNodeList = Dom.SelectNodes("//" + tagName);
            if (xmlNodeList != null)
            {
                foreach (XmlElement argument in xmlNodeList)
                {
                    var token = Environment.ExpandEnvironmentVariables(argument.InnerText);

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
                            token = '"' + token + '"';
                        }
                    }
                    arguments += " " + token;
                }
            }

            return arguments;
        }

        /// <summary>
        /// LogDirectory is the service wrapper executable directory or the optionally specified logpath element.
        /// </summary>
        public string LogDirectory
        {
            get
            {
                var loggingNode = Dom.SelectSingleNode("//logpath");

                return loggingNode != null ? Environment.ExpandEnvironmentVariables(loggingNode.InnerText) : Path.GetDirectoryName(ExecutablePath);
            }
        }

        public LogHandler LogHandler
        {
            get
            {
                string mode=null;
                
                // first, backward compatibility with older configuration
                var e = (XmlElement)Dom.SelectSingleNode("//logmode");
                if (e!=null) {
                    mode = e.InnerText;
                } else {
                    // this is more modern way, to support nested elements as configuration
                    e = (XmlElement)Dom.SelectSingleNode("//log");
                    if (e!=null)
                        mode = e.GetAttribute("mode");
                }

                if (mode == null) mode = "append";

                switch (mode)
                {
                    case "rotate":
                        return new SizeBasedRollingLogAppender(LogDirectory, BaseName);

                    case "reset":
                        return new ResetLogAppender(LogDirectory, BaseName);

                    case "roll":
                        return new RollingLogAppender(LogDirectory, BaseName);

                    case "roll-by-time":
                        XmlNode patternNode = e.SelectSingleNode("pattern");
                        if (patternNode == null)
                        {
                            throw new InvalidDataException("Time Based rolling policy is specified but no pattern can be found in configuration XML.");
                        }
                        string pattern = patternNode.InnerText;
                        int period = SingleIntElement(e,"period",1);
                        return new TimeBasedRollingLogAppender(LogDirectory, BaseName, pattern, period);

                    case "roll-by-size":
                        int sizeThreshold = SingleIntElement(e,"sizeThreshold",10*1024)  * SizeBasedRollingLogAppender.BYTES_PER_KB;
                        int keepFiles = SingleIntElement(e,"keepFiles",SizeBasedRollingLogAppender.DEFAULT_FILES_TO_KEEP);
                        return new SizeBasedRollingLogAppender(LogDirectory, BaseName, sizeThreshold, keepFiles);

                    case "append":
                        return new DefaultLogAppender(LogDirectory, BaseName);

                    default:
                        throw new InvalidDataException("Undefined logging mode: " + mode);
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
                var serviceDependencies = new System.Collections.ArrayList();

                var xmlNodeList = Dom.SelectNodes("//depend");
                if (xmlNodeList != null)
                {
                    foreach (XmlNode depend in xmlNodeList)
                    {
                        serviceDependencies.Add(depend.InnerText);
                    }
                }

                return (string[])serviceDependencies.ToArray(typeof(string));
            }
        }

        public string Id
        {
            get
            {
                return SingleElement("id");
            }
        }

        public string Caption
        {
            get
            {
                return SingleElement("name");
            }
        }

        public string Description
        {
            get
            {
                return SingleElement("description");
            }
        }

        /// <summary>
        /// True if the service should when finished on shutdown.
        /// This doesn't work on some OSes. See http://msdn.microsoft.com/en-us/library/ms679277%28VS.85%29.aspx
        /// </summary>
        public bool BeepOnShutdown
        {
            get
            {
                return Dom.SelectSingleNode("//beeponshutdown") != null;
            }
        }


        /// <summary>
        /// The estimated time required for a pending stop operation (default 15 secs).
        /// Before the specified amount of time has elapsed, the service should make its next call to the SetServiceStatus function 
        /// with either an incremented checkPoint value or a change in currentState. (see http://msdn.microsoft.com/en-us/library/ms685996.aspx)
        /// </summary>
        public TimeSpan WaitHint
        {
            get
            {
                return SingleTimeSpanElement(Dom, "waithint", TimeSpan.FromSeconds(15));
            }
        }


        /// <summary>
        /// The time before the service should make its next call to the SetServiceStatus function 
        /// with an incremented checkPoint value (default 1 sec).
        /// Do not wait longer than the wait hint. A good interval is one-tenth of the wait hint but not less than 1 second and not more than 10 seconds.
        /// </summary>
        public TimeSpan SleepTime
        {
            get
            {
                return SingleTimeSpanElement(Dom, "sleeptime", TimeSpan.FromSeconds(1));
            }
        }

        /// <summary>
        /// True if the service can interact with the desktop.
        /// </summary>
        public bool Interactive
        {
            get
            {
                return Dom.SelectSingleNode("//interactive") != null;
            }
        }

        /// <summary>
        /// Environment variable overrides
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables
        {
            get
            {
                var map = new Dictionary<string, string>();
                var xmlNodeList = Dom.SelectNodes("//env");
                if (xmlNodeList != null)
                {
                    foreach (XmlNode n in xmlNodeList)
                    {
                        if (n.Attributes != null)
                        {
                            var key = n.Attributes["name"].Value;
                            var value = Environment.ExpandEnvironmentVariables(n.Attributes["value"].Value);
                            map[key] = value;

                            Environment.SetEnvironmentVariable(key, value);
                        }
                    }
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
                var r = new List<Download>();
                var xmlNodeList = Dom.SelectNodes("//download");
                if (xmlNodeList != null)
                {
                    foreach (XmlNode n in xmlNodeList)
                    {
                        r.Add(new Download(n));
                    }
                }
                return r;
            }
        }

        public List<SC_ACTION> FailureActions
        {
            get
            {
                var r = new List<SC_ACTION>();
                var xmlNodeList = Dom.SelectNodes("//onfailure");
                if (xmlNodeList != null)
                {
                    foreach (XmlNode n in xmlNodeList)
                    {
                        var type = SC_ACTION_TYPE.SC_ACTION_NONE;
                        if (n.Attributes != null)
                        {
                            string action = n.Attributes["action"].Value;
                            switch (action)
                            {
                                case "restart":
                                    type = SC_ACTION_TYPE.SC_ACTION_RESTART;
                                    break;
                                case "none":
                                    type = SC_ACTION_TYPE.SC_ACTION_NONE;
                                    break;
                                case "reboot":
                                    type = SC_ACTION_TYPE.SC_ACTION_REBOOT;
                                    break;
                                default:
                                    throw new Exception("Invalid failure action: " + action);
                            }
                        }
                        if (n.Attributes != null)
                        {
                            var delay = n.Attributes["delay"];
                            r.Add(new SC_ACTION(type, delay != null ? ParseTimeSpan(delay.Value) : TimeSpan.Zero));
                        }
                    }
                }
                return r;
            }
        }

        public TimeSpan ResetFailureAfter
        {
            get
            {
                return SingleTimeSpanElement(Dom, "resetfailure", TimeSpan.FromDays(1));
            }
        }

        private string GetServiceAccountPart(string subNodeName)
		{
			var node = Dom.SelectSingleNode("//serviceaccount");

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

        private string ServiceAccountDomain
		{
			get{
				return GetServiceAccountPart("domain");
			}
		}

        private string ServiceAccountName
		{
			get
			{
				return GetServiceAccountPart("user");
			}
		}

		public string ServiceAccountPassword
		{
			get
			{
				return GetServiceAccountPart("password");
			}
		}

		public string ServiceAccountUser
		{
			get { return (ServiceAccountDomain ?? "NULL") + @"\" + (ServiceAccountName ?? "NULL"); }
		}

		public bool HasServiceAccount()
		{
			return !string.IsNullOrEmpty(ServiceAccountDomain) && !string.IsNullOrEmpty(ServiceAccountName);
		}

    }
}

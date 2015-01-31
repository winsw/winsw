using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml;
using winsw.Util;
using WMI;

namespace winsw
{
    /// <summary>
    /// In-memory representation of the configuration file.
    /// </summary>
    public class ServiceDescriptor
    {
        // ReSharper disable once InconsistentNaming
        protected readonly XmlDocument dom = new XmlDocument();

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

        public virtual string ExecutablePath
        {
            get
            {
                // this returns the executable name as given by the calling process, so
                // it needs to be absolutized.
                string p = Environment.GetCommandLineArgs()[0];
                return Path.GetFullPath(p);
            }
        }

        public ServiceDescriptor()
        {
            // find co-located configuration xml. We search up to the ancestor directories to simplify debugging,
            // as well as trimming off ".vshost" suffix (which is used during debugging)
            //Get the first parent to go into the recursive loop
            string p = ExecutablePath;
            string baseName = Path.GetFileNameWithoutExtension(p);
            if (baseName.EndsWith(".vshost")) baseName = baseName.Substring(0, baseName.Length - 7);
            DirectoryInfo d = new DirectoryInfo(Path.GetDirectoryName(p));
            while (true)
            {
                if (File.Exists(Path.Combine(d.FullName, baseName + ".xml")))
                    break;

                if (d.Parent == null)
                    throw new FileNotFoundException("Unable to locate "+baseName+".xml file within executable directory or any parents");

                d = d.Parent;
            }

            BaseName = baseName;
            BasePath = Path.Combine(d.FullName, BaseName);

            dom.Load(BasePath + ".xml");

            // register the base directory as environment variable so that future expansions can refer to this.
            Environment.SetEnvironmentVariable("BASE", p);
            // ditto for ID
            Environment.SetEnvironmentVariable("SERVICE_ID", Id);
            Environment.SetEnvironmentVariable("WINSW_EXECUTABLE", ExecutablePath);
        }

        /// <summary>
        /// Loads descriptor from existing DOM
        /// </summary>
        public ServiceDescriptor(XmlDocument dom)
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
            return SingleElement(tagName, false);
        }

        private string SingleElement(string tagName, Boolean optional)
        {
            var n = dom.SelectSingleNode("//" + tagName);
            if (n == null && !optional) throw new InvalidDataException("<" + tagName + "> is missing in configuration XML");
            return n == null ? null : Environment.ExpandEnvironmentVariables(n.InnerText);
        }

        private int SingleIntElement(XmlNode parent, string tagName, int defaultValue)
        {
            var e = parent.SelectSingleNode(tagName);

            if (e == null)
            {
                return defaultValue;
            }
            else
            {
                return int.Parse(e.InnerText);
            }
        }

        private TimeSpan SingleTimeSpanElement(XmlNode parent, string tagName, TimeSpan defaultValue)
        {
            var e = parent.SelectSingleNode(tagName);

            if (e == null)
            {
                return defaultValue;
            }
            else
            {
                return ParseTimeSpan(e.InnerText);
            }
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

        private static readonly Dictionary<string,long> Suffix = new Dictionary<string,long> {
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
                string arguments = AppendTags("argument");

                if (arguments == null)
                {
                    var argumentsNode = dom.SelectSingleNode("//arguments");

                    if (argumentsNode == null)
                    {
                        return "";
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

        
        public string WorkingDirectory {
            get {
                var wd = SingleElement("workingdirectory", true);
                return String.IsNullOrEmpty(wd) ? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) : wd;
            }
        }

        public List<string> ExtensionIds
        {
            get
            {
                List<string> res = new List<string>();

                XmlNode argumentNode = ExtensionsConfiguration;
                XmlNodeList extensions = argumentNode != null ? argumentNode.SelectNodes("extension") : null;
                if ( extensions != null)
                {
                    foreach (XmlNode e in extensions)
                    {
                        XmlElement extension = (XmlElement)e;
                        String extensionId = XmlHelper.SingleAttribute<string>(extension, "id");
                        res.Add(extensionId);
                    }
                }

                return res;
            }
        }

        public XmlNode ExtensionsConfiguration
        {
            get
            {
                XmlNode argumentNode = dom.SelectSingleNode("//extensions");
                return argumentNode;
            }
        }

        /// <summary>
        /// Combines the contents of all the elements of the given name,
        /// or return null if no element exists. Handles whitespace quotation.
        /// </summary>
        private string AppendTags(string tagName)
        {
            XmlNode argumentNode = dom.SelectSingleNode("//" + tagName);

            if (argumentNode == null)
            {
                return null;
            }
            else
            {
                string arguments = "";

                foreach (XmlElement argument in dom.SelectNodes("//" + tagName))
                {
                    string token = Environment.ExpandEnvironmentVariables(argument.InnerText);

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

                return arguments;
            }
        }

        /// <summary>
        /// LogDirectory is the service wrapper executable directory or the optionally specified logpath element.
        /// </summary>
        public string LogDirectory
        {
            get
            {
                XmlNode loggingNode = dom.SelectSingleNode("//logpath");

                if (loggingNode != null)
                {
                    return Environment.ExpandEnvironmentVariables(loggingNode.InnerText);
                }
                else
                {
                    return Path.GetDirectoryName(ExecutablePath);
                }
            }
        }

        public LogHandler LogHandler
        {
            get
            {
                string mode=null;
                
                // first, backward compatibility with older configuration
                XmlElement e = (XmlElement)dom.SelectSingleNode("//logmode");
                if (e!=null) {
                    mode = e.InnerText;
                } else {
                    // this is more modern way, to support nested elements as configuration
                    e = (XmlElement)dom.SelectSingleNode("//log");
                    if (e!=null)
                        mode = e.GetAttribute("mode");
                }

                if (mode == null) mode = "append";

                switch (mode)
                {
                    case "rotate":
                        return new SizeBasedRollingLogAppender(LogDirectory, BaseName);

                    case "none":
                        return new IgnoreLogAppender();

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
                ArrayList serviceDependencies = new ArrayList();

                var xmlNodeList = dom.SelectNodes("//depend");
                if (xmlNodeList != null)
                    foreach (XmlNode depend in xmlNodeList)
                    {
                        serviceDependencies.Add(depend.InnerText);
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
        /// Start mode of the Service
        /// </summary>
        public StartMode StartMode
        {
            get
            {
                var p = SingleElement("startmode", true);
                if (p == null) return StartMode.Automatic;  // default value
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
        /// True if the service should when finished on shutdown.
        /// This doesn't work on some OSes. See http://msdn.microsoft.com/en-us/library/ms679277%28VS.85%29.aspx
        /// </summary>
        public bool BeepOnShutdown
        {
            get
            {
                return dom.SelectSingleNode("//beeponshutdown") != null;
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
                return SingleTimeSpanElement(dom.FirstChild, "waithint", TimeSpan.FromSeconds(15));
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
                return SingleTimeSpanElement(dom.FirstChild, "sleeptime", TimeSpan.FromSeconds(1));
            }
        }

        /// <summary>
        /// True if the service can interact with the desktop.
        /// </summary>
        public bool Interactive
        {
            get
            {
                return dom.SelectSingleNode("//interactive") != null;
            }
        }

        /// <summary>
        /// Environment variable overrides
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables
        {
            get
            {
                Dictionary<string, string> map = new Dictionary<string, string>();
                foreach (XmlNode n in dom.SelectNodes("//env"))
                {
                    string key = n.Attributes["name"].Value;
                    string value = Environment.ExpandEnvironmentVariables(n.Attributes["value"].Value);
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
                List<Download> r = new List<Download>();
                var xmlNodeList = dom.SelectNodes("//download");
                if (xmlNodeList != null)
                    foreach (XmlNode n in xmlNodeList)
                    {
                        r.Add(new Download(n));
                    }
                return r;
            }
        }

        public List<SC_ACTION> FailureActions
        {
            get
            {
                List<SC_ACTION> r = new List<SC_ACTION>();
                var childNodes = dom.SelectNodes("//onfailure");
                if (childNodes != null)
                {
                    foreach (XmlNode n in childNodes)
                    {
                        SC_ACTION_TYPE type;
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
                        XmlAttribute delay = n.Attributes["delay"];
                        r.Add(new SC_ACTION(type, delay != null ? ParseTimeSpan(delay.Value) : TimeSpan.Zero));
                    }
                }
                return r;
            }
        }

        public TimeSpan ResetFailureAfter
        {
            get
            {
                return SingleTimeSpanElement(dom.FirstChild, "resetfailure", TimeSpan.FromDays(1));
            }
        }

        protected string GetServiceAccountPart(string subNodeName)
		{
			var node = dom.SelectSingleNode("//serviceaccount");

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

        protected string AllowServiceLogon
        {
            get
            {
                return GetServiceAccountPart("allowservicelogon");
            }
        }

        // ReSharper disable once InconsistentNaming
		protected string serviceAccountDomain
		{
			get
            {
				return GetServiceAccountPart("domain");
			}
		}

        // ReSharper disable once InconsistentNaming
		protected string serviceAccountName
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
			get { return (serviceAccountDomain ?? "NULL") + @"\" + (serviceAccountName ?? "NULL"); }
		}

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
                    bool parsedvalue;
                    if (Boolean.TryParse(AllowServiceLogon, out parsedvalue))
                    {
                        return parsedvalue;
                    }
                }
                return false;
            }
        }

         /// <summary>
         /// Time to wait for the service to gracefully shutdown before we forcibly kill it
         /// </summary>
        public TimeSpan StopTimeout
        {
            get
            {
                return SingleTimeSpanElement(dom.FirstChild, "stoptimeout", TimeSpan.FromSeconds(15));
            }
        }

        public bool StopParentProcessFirst
        {
            get
            {
                var value = SingleElement("stopparentprocessfirst", true);
                bool result;
                if (bool.TryParse(value, out result))
                {
                    return result;
                }
                return false;
            }
        }

        /// <summary>
        /// Desired process priority or null if not specified.
        /// </summary>
        public ProcessPriorityClass Priority
        {
            get
            {
                var p = SingleElement("priority",true);
                if (p == null) return ProcessPriorityClass.Normal;  // default value

                return (ProcessPriorityClass)Enum.Parse(typeof(ProcessPriorityClass), p, true);
            }
        }
    }
}

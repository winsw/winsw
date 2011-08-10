using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;


namespace winsw
{
    /// <summary>
    /// In-memory representation of the configuration file.
    /// </summary>
    public class ServiceDescriptor
    {
        private readonly XmlDocument dom = new XmlDocument();


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

        public static string ExecutablePath
        {
            get
            {
                // this returns the executable name as given by the calling process, so
                // it needs to be absolutized.
                string p = Environment.GetCommandLineArgs()[0];
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, p);

            }
        }

        public ServiceDescriptor()
        {
            // find co-located configuration xml. We search up to the ancestor directories to simplify debugging,
            // as well as trimming off ".vshost" suffix (which is used during debugging)
            string p = ExecutablePath;
            string baseName = Path.GetFileNameWithoutExtension(p);
            if (baseName.EndsWith(".vshost")) baseName = baseName.Substring(0, baseName.Length - 7);
            while (true)
            {
                p = Path.GetDirectoryName(p);
                if (File.Exists(Path.Combine(p, baseName + ".xml")))
                    break;
            }

            // register the base directory as environment variable so that future expansions can refer to this.
            Environment.SetEnvironmentVariable("BASE", p);

            BaseName = baseName;
            BasePath = Path.Combine(p, BaseName);

            dom.Load(BasePath + ".xml");
        }

        private string SingleElement(string tagName)
        {
            var n = dom.SelectSingleNode("//" + tagName);
            if (n == null) throw new InvalidDataException("<" + tagName + "> is missing in configuration XML");
            return Environment.ExpandEnvironmentVariables(n.InnerText);
        }


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
                string stopExe = null;

                try
                {
                    stopExe = SingleElement("stopexecutable");
                }
                catch (InvalidDataException)
                {
                    // ignore
                    stopExe = null;
                }

                return stopExe;
            }
        }

        /// <summary>
        /// Arguments or multiple optional argument elements which overrule the arguments element.
        /// </summary>
        public string Arguments
        {
            get
            {
                // collect all of the "argument" tags
                string arguments = AppendTags("argument");

                if (arguments != null)
                {
                    return arguments;
                }

                // no "argument" tags, so look for one "arguments" tag.
                var tagName = "arguments";
                var argumentsNode = dom.SelectSingleNode("//" + tagName);

                if (argumentsNode == null)
                {
                    if ((AppendTags("startargument") == null) &&
                        (AppendTags("startarguments") == null))
                    {
                        throw new InvalidDataException("<" + tagName + "> is missing in configuration XML");
                    }
                    else
                    {
                        return "";
                    }
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
                // collect all of the "startargument" tags
                string arguments = AppendTags("startargument");

                if (arguments != null)
                {
                    return arguments;
                }

                // no "startargument" tags, so look for one "startarguments" tag.
                var tagName = "startarguments";
                var argumentsNode = dom.SelectSingleNode("//" + tagName);

                if (argumentsNode == null)
                {
                    return "";
                }

                return Environment.ExpandEnvironmentVariables(argumentsNode.InnerText);
            }
        }

        /// <summary>
        /// Multiple optional stopargument elements.
        /// </summary>
        public string Stoparguments
        {
            get
            {
                // collect all of the "stopargument" tags
                string arguments = AppendTags("stopargument");

                if (arguments != null)
                {
                    return arguments;
                }

                // no "stopargument" tags, so look for one "stoparguments" tag.
                var tagName = "stoparguments";
                var argumentsNode = dom.SelectSingleNode("//" + tagName);

                if (argumentsNode == null)
                {
                    return null;
                }

                return Environment.ExpandEnvironmentVariables(argumentsNode.InnerText);
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

                foreach (XmlNode argument in dom.SelectNodes("//" + tagName))
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


        public string WorkingDirectory
        {
            get
            {
                XmlNode workingDirNode = dom.SelectSingleNode("//workingdir");

                if (workingDirNode != null)
                {
                    return Environment.ExpandEnvironmentVariables(workingDirNode.InnerText);
                }
                else
                {
                    return Path.GetDirectoryName(ExecutablePath);
                }
            }
        }

        /// <summary>
        /// LogDirectory is the service wrapper executable directory or the optionally specified logpath element.
        /// </summary>
        public string LogDirectory
        {
            get
            {
                XmlNode loggingNode = dom.SelectSingleNode("//logdir");

                if (loggingNode != null)
                {
                    return Environment.ExpandEnvironmentVariables(loggingNode.InnerText);
                }
                else
                {
                    loggingNode = dom.SelectSingleNode("//logpath");

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
        }


        /// <summary>
        /// LogFilemode to 'reset' (overwrite), 'rotate' (based on a size limit), 'rool' (age) or 'append' [default] the out.log and err.log files.
        /// </summary>
        public LogMode LogFileMode
        {
            get
            {
                XmlNode logmodeNode = dom.SelectSingleNode("//logmode");

                if (logmodeNode == null)
                {
                    return new LogMode(null, null, null);
                }

                string countString = getXmlNodeAttribute(logmodeNode, "count", false);
                string sizeString = getXmlNodeAttribute(logmodeNode, "size", false);

                try
                {
                    return new LogMode(logmodeNode.InnerText, countString, sizeString);
                }
                catch
                {
                    return new LogMode(null, null, null);
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
                System.Collections.ArrayList serviceDependencies = new System.Collections.ArrayList();

                foreach (XmlNode depend in dom.SelectNodes("//depend"))
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

        public string DomainUser
        {
            get
            {
                string user = null;

                try
                {
                    user = SingleElement("domainuser");
                }
                catch (InvalidDataException)
                {
                    // ignore
                    user = null;
                }

                return user;
            }
        }

        /// <summary>
        /// True if the service should beep when finished on shutdown.
        /// </summary>
        public bool BeepOnShutdown
        {
            get
            {
                return dom.SelectSingleNode("//beeponshutdown") != null;
            }
        }


        /// <summary>
        /// The estimated time required for a pending stop operation, in milliseconds (default 15 secs).
        /// Before the specified amount of time has elapsed, the service should make its next call to the SetServiceStatus function 
        /// with either an incremented checkPoint value or a change in currentState. (see http://msdn.microsoft.com/en-us/library/ms685996.aspx)
        /// </summary>
        public int WaitHint
        {
            get
            {
                XmlNode waithintNode = dom.SelectSingleNode("//waithint");

                if (waithintNode == null)
                {
                    return 15000;
                }
                else
                {
                    return int.Parse(waithintNode.InnerText);
                }
            }
        }


        /// <summary>
        /// The time, in milliseconds (default 1 sec), before the service should make its next call to the SetServiceStatus function 
        /// with an incremented checkPoint value.
        /// Do not wait longer than the wait hint. A good interval is one-tenth of the wait hint but not less than 1 second and not more than 10 seconds.
        /// </summary>
        public int SleepTime
        {
            get
            {
                XmlNode sleeptimeNode = dom.SelectSingleNode("//sleeptime");

                if (sleeptimeNode == null)
                {
                    return 1000;
                }
                else
                {
                    return int.Parse(sleeptimeNode.InnerText);
                }
            }
        }

        /// <summary>
        /// Returns the Start Mode from the XML file.
        /// </summary>
        public WMI.StartMode StartMode
        {
            get
            {
                XmlNode startmodeNode = dom.SelectSingleNode("//startmode");

                if (startmodeNode == null)
                {
                    return WMI.StartMode.Automatic;
                }
                else
                {
                    string lowerText = startmodeNode.InnerText.ToLower();
                    if ((lowerText == "auto") ||
                         (lowerText == "automatic"))
                    {
                        return WMI.StartMode.Automatic;
                    }
                    else if (lowerText == "manual")
                    {
                        return WMI.StartMode.Manual;
                    }
                    else
                    {
                        throw new InvalidDataException("<startmode> must be either Manual or Automatic.");
                    }
                }
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

        private string getXmlNodeAttribute(XmlNode n, string attr, bool expandEnv)
        {
            string valueString = null;

            try
            {
                if (expandEnv)
                {
                    valueString = Environment.ExpandEnvironmentVariables(n.Attributes[attr].Value);
                }
                else
                {
                    valueString = n.Attributes[attr].Value;
                }
            }
            catch
            {
                valueString = null;
            }

            return valueString;
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
                    string key = getXmlNodeAttribute(n, "name", false);
                    string value = getXmlNodeAttribute(n, "value", true);
                    string op = getXmlNodeAttribute(n, "op", false);
                    string separator = getXmlNodeAttribute(n, "sep", false);
                    string origKey = getXmlNodeAttribute(n, "orig", false);

                    if (op == null)
                    {
                        op = "replace";
                    }

                    if (separator == null)
                    {
                        separator = " ";
                    }

                    if ((op != "replace") &&
                        (op != "append") &&
                        (op != "prepend") &&
                        (op != "ifempty"))
                    {
                        continue;
                    }


                    if ((op == "append") ||
                        (op == "prepend") ||
                        (op == "ifempty"))
                    {
                        string oldValue = null;

                        if (origKey == null)
                        {
                            origKey = key;
                        }

                        try
                        {
                            oldValue = Environment.GetEnvironmentVariable(origKey);
                        }
                        catch
                        {
                            oldValue = null;
                        }

                        if (op == "append")
                        {
                            if (oldValue != null)
                            {
                                value = oldValue + separator + value;
                            }
                        }
                        else if (op == "prepend")
                        {
                            if (oldValue != null)
                            {
                                value = value + separator + oldValue;
                            }
                        }
                        else if (op == "ifempty")
                        {
                            // only use this new value if the old value is empty
                            if ((oldValue != null) && (oldValue != ""))
                            {
                                value = oldValue;
                            }
                        }
                    }

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
                foreach (XmlNode n in dom.SelectNodes("//download"))
                {
                    string from = getXmlNodeAttribute(n, "from", true);
                    string to = getXmlNodeAttribute(n, "to", true);

                    r.Add(new Download(from, to));
                }
                return r;
            }
        }

        /// <summary>
        /// List of network drives to be mounted by the wrapper before starting
        /// a service.
        /// </summary>
        public List<NetDrive> NetDrives
        {
            get
            {
                List<NetDrive> r = new List<NetDrive>();
                foreach (XmlNode n in dom.SelectNodes("//netdrive"))
                {
                    string d = getXmlNodeAttribute(n, "localdrive", true);
                    string s = getXmlNodeAttribute(n, "sharename", true);

                    r.Add(new NetDrive(d, s));
                }
                return r;
            }
        }
    }
}

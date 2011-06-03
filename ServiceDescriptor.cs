﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.IO;
using System.Net;
using WMI;
using System.Xml;
using System.Threading;
using Microsoft.Win32;
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
                return AppendTags("stopexecutable");
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
                    var tagName = "arguments";
                    var argumentsNode = dom.SelectSingleNode("//" + tagName);

                    if (argumentsNode == null)
                    {
                        if (AppendTags("startargument") == null)
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

        /// <summary>
        /// Combines the contents of all the elements of the given name,
        /// or return null if no element exists.
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
                    arguments += " " + argument.InnerText;
                }

                return Environment.ExpandEnvironmentVariables(arguments);
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


        /// <summary>
        /// Logmode to 'reset', 'rotate' once or 'append' [default] the out.log and err.log files.
        /// </summary>
        public string Logmode
        {
            get
            {
                XmlNode logmodeNode = dom.SelectSingleNode("//logmode");

                if (logmodeNode == null)
                {
                    return "append";
                }
                else
                {
                    return logmodeNode.InnerText;
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

        /// <summary>
        /// True if the service should when finished on shutdown.
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
                foreach (XmlNode n in dom.SelectNodes("//download"))
                {
                    r.Add(new Download(n));
                }
                return r;
            }
        }
    }
}

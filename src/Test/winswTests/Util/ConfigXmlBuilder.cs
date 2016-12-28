using System;
using System.Collections.Generic;
using System.Text;
using winsw;

namespace winswTests.Util
{
    /// <summary>
    /// Configuration XML builder, which simplifies testing of WinSW Configuration file.
    /// </summary>
    class ConfigXmlBuilder
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Description { get; set; }
        public string Executable { get; set; }

        private List<String> configEntries;

        private ConfigXmlBuilder()
        {
            configEntries = new List<string>();
        }

        public static ConfigXmlBuilder create(string id = null, string name = null, 
            string description = null, string executable = null)
        {
            var config = new ConfigXmlBuilder();
            config.Id = id ?? "myapp";
            config.Name = name ?? "MyApp Service";
            config.Description = description ?? "MyApp Service (powered by WinSW)";
            config.Executable = executable ?? "%BASE%\\myExecutable.exe";  
            return config;
        }

        public string ToXMLString(bool dumpConfig = false)
        {
            StringBuilder str = new StringBuilder();
            str.Append("<service>\n");
            str.AppendFormat("  <id>{0}</id>\n", Id);
            str.AppendFormat("  <name>{0}</name>\n", Name);
            str.AppendFormat("  <description>{0}</description>\n", Description);
            str.AppendFormat("  <executable>{0}</executable>\n", Executable);
            foreach (String entry in configEntries)
            {
                // We do not care much about pretty formatting here
                str.AppendFormat("  {0}\n", entry);
            }
            str.Append("</service>\n");
            string res = str.ToString();
            if (dumpConfig)
            {
                Console.Out.WriteLine("Produced config:");
                Console.Out.WriteLine(res);
            }
            return res;
        }

        public ServiceDescriptor ToServiceDescriptor(bool dumpConfig = false)
        {
            return ServiceDescriptor.FromXML(ToXMLString(dumpConfig));
        } 

        public ConfigXmlBuilder WithRawEntry(string entry)
        {
            configEntries.Add(entry);
            return this;
        }

        public ConfigXmlBuilder WithTag(string tagName, string value)
        { 
            return WithRawEntry(String.Format("<{0}>{1}</{0}>", tagName, value));
        }
    }
}

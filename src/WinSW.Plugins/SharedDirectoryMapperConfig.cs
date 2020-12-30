using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using WinSW.Util;

namespace WinSW.Plugins
{
    /// <summary>
    /// Stores configuration entries for SharedDirectoryMapper extension.
    /// </summary>
    public class SharedDirectoryMapperConfig
    {
        public bool EnableMapping { get; set; }
        public string Label { get; set; }
        public string UNCPath { get; set; }

        public SharedDirectoryMapperConfig(bool enableMapping, string label, string uncPath)
        {
            this.EnableMapping = enableMapping;
            this.Label = label;
            this.UNCPath = uncPath;
        }

        public static SharedDirectoryMapperConfig FromXml(XmlElement node)
        {
            bool enableMapping = XmlHelper.SingleAttribute(node, "enabled", true);
            string label = XmlHelper.SingleAttribute<string>(node, "label");
            string uncPath = XmlHelper.SingleAttribute<string>(node, "uncpath");
            return new SharedDirectoryMapperConfig(enableMapping, label, uncPath);
        }

        public static SharedDirectoryMapperConfig FromYaml(object yamlObject)
        {
            if (yamlObject is not Dictionary<object, object> dict)
            {
                // TODO : throw ExtensionExeption
                throw new InvalidDataException("SharedDirectoryMapperConfig config error");
            }

            string enableMappingConfig = Environment.ExpandEnvironmentVariables((string)dict["enabled"]);
            bool enableMapping = ConfigHelper.YamlBoolParse(enableMappingConfig);

            string label = Environment.ExpandEnvironmentVariables((string)dict["label"]);
            string uncPath = Environment.ExpandEnvironmentVariables((string)dict["uncPath"]);

            return new SharedDirectoryMapperConfig(enableMapping, label, uncPath);
        }
    }
}

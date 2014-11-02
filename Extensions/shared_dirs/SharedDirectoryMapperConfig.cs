using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using winsw.Utils;

namespace winsw.extensions.shared_dirs
{
    /// <summary>
    /// Stores configuration entries for SharedDirectoryMapper extension.
    /// </summary>
    internal class SharedDirectoryMapperConfig
    {
        public bool EnableMapping { get; set; }
        public String Label { get; set; }
        public String UNCPath { get; set; }

        public SharedDirectoryMapperConfig(bool enableMapping, string label, string UNCPath)
        {
            this.EnableMapping = enableMapping;
            this.Label = label;
            this.UNCPath = UNCPath;
        }

        public static SharedDirectoryMapperConfig FromXml(XmlElement node)
        {
            bool enableMapping = XmlHelper.SingleAttribute(node, "enabled", true);
            string label = XmlHelper.SingleAttribute<string>(node, "label");
            string uncPath = XmlHelper.SingleAttribute<string>(node, "uncpath");
            return new SharedDirectoryMapperConfig(enableMapping, label, uncPath);
        }
    }
}

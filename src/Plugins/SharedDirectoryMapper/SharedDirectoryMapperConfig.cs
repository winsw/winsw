using System.Xml;
using winsw.Util;

namespace winsw.Plugins.SharedDirectoryMapper
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
            EnableMapping = enableMapping;
            Label = label;
            UNCPath = uncPath;
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

using System.Xml;
using WinSW.Util;

namespace WinSW.Plugins.SharedDirectoryMapper
{
    /// <summary>
    /// Stores configuration entries for SharedDirectoryMapper extension.
    /// </summary>
    public class SharedDirectoryMapperConfig
    {
        public bool EnableMapping { get; }

        public string Label { get; }

        public string UNCPath { get; }

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
    }
}

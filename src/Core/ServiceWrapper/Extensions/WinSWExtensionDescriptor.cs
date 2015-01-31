using System;
using System.Xml;
using winsw.Util;

namespace winsw.Extensions
{
    public class WinSWExtensionDescriptor
    {
        /// <summary>
        /// Unique extension ID
        /// </summary>
        public String Id { get; private set; }

        /// <summary>
        /// Exception is enabled
        /// </summary>
        public bool Enabled { get; private set; }

        /// <summary>
        /// Extension classname
        /// </summary>
        public String ClassName { get; private set; }

        private WinSWExtensionDescriptor(string id, string className, bool enabled)
        {
            Id = id;
            Enabled = enabled;
            ClassName = className;
        }

        public static WinSWExtensionDescriptor FromXml(XmlElement node)
        {
            bool enabled = XmlHelper.SingleAttribute(node, "enabled", true);
            string className = XmlHelper.SingleAttribute<string>(node, "className");
            string id = XmlHelper.SingleAttribute<string>(node, "id");
            return new WinSWExtensionDescriptor(id, className, enabled);
        }
    }
}

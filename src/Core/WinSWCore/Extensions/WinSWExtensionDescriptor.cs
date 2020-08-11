using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;
using WinSW.Util;

namespace WinSW.Extensions
{
    /// <summary>
    /// Describes WinSW extensions in <see cref="IWinSWExtension"/>
    /// </summary>
    /// <remarks>
    /// Any extension has its own descriptor instance.
    /// </remarks>
    public class WinSWExtensionDescriptor
    {
        /// <summary>
        /// Unique extension ID
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Exception is enabled
        /// </summary>
        public bool Enabled { get; private set; }

        /// <summary>
        /// Extension classname
        /// </summary>
        public string ClassName { get; private set; }

        private WinSWExtensionDescriptor(string id, string className, bool enabled)
        {
            this.Id = id;
            this.Enabled = enabled;
            this.ClassName = className;
        }

        public static WinSWExtensionDescriptor FromXml(XmlElement node)
        {
            bool enabled = XmlHelper.SingleAttribute(node, "enabled", true);
            string className = XmlHelper.SingleAttribute<string>(node, "className");
            string id = XmlHelper.SingleAttribute<string>(node, "id");
            return new WinSWExtensionDescriptor(id, className, enabled);
        }

        public static WinSWExtensionDescriptor FromYaml(object node)
        {
            if (!(node is Dictionary<object, object> config))
            {
                throw new InvalidDataException("Cannot get the configuration entry");
            }

            bool enabled = ConfigHelper.YamlBoolParse((string)config["enabled"]);
            string className = (string)config["classname"];
            string id = (string)config["id"];

            return new WinSWExtensionDescriptor(id, className, enabled);
        }
    }
}

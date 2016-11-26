using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.IO;

namespace winsw.Util
{
    public class XmlHelper
    {
        /// <summary>
        /// Retrieves a single string element 
        /// </summary>
        /// <param name="node">Parent node</param>
        /// <param name="tagName">Element name</param>
        /// <param name="optional">If optional, don't throw an exception if the elemen is missing</param>
        /// <returns>String value or null</returns>
        /// <exception cref="InvalidDataException">The required element is missing</exception>
        public static string SingleElement(XmlNode node, string tagName, Boolean optional)
        {
            var n = node.SelectSingleNode(tagName);
            if (n == null && !optional) throw new InvalidDataException("<" + tagName + "> is missing in configuration XML");
            return n == null ? null : Environment.ExpandEnvironmentVariables(n.InnerText);
        }

        /// <summary>
        /// Retrieves a single node
        /// </summary>
        /// <param name="node">Parent node</param>
        /// <param name="tagName">Element name</param>
        /// <param name="optional">If otional, don't throw an exception if the elemen is missing</param>
        /// <returns>String value or null</returns>
        /// <exception cref="InvalidDataException">The required element is missing</exception>
        public static XmlNode SingleNode(XmlNode node, string tagName, Boolean optional)
        {
            var n = node.SelectSingleNode(tagName);
            if (n == null && !optional) throw new InvalidDataException("<" + tagName + "> is missing in configuration XML");
            return n;
        }

        /// <summary>
        /// Retrieves a single mandatory attribute
        /// </summary>
        /// <param name="node">Parent node</param>
        /// <param name="attributeName">Attribute name</param>
        /// <returns>Attribute value</returns>
        /// <exception cref="InvalidDataException">The required attribute is missing</exception>
        public static TAttributeType SingleAttribute <TAttributeType> (XmlElement node, string attributeName)
        {
            if (!node.HasAttribute(attributeName))
            {
                throw new InvalidDataException("Attribute <" + attributeName + "> is missing in configuration XML");
            }

            return SingleAttribute<TAttributeType>(node, attributeName, default(TAttributeType));
        }

        /// <summary>
        /// Retrieves a single optional attribute
        /// </summary>
        /// <param name="node">Parent node</param>
        /// <param name="attributeName">Attribute name</param>
        /// <param name="defaultValue">Default value</param>
        /// <returns>Attribute value (or default)</returns>
        public static TAttributeType SingleAttribute<TAttributeType>(XmlElement node, string attributeName, TAttributeType defaultValue)
        {
             if (!node.HasAttribute(attributeName)) return defaultValue;

             string rawValue = node.GetAttribute(attributeName);
             string substitutedValue = Environment.ExpandEnvironmentVariables(rawValue);
             var value = (TAttributeType)Convert.ChangeType(substitutedValue, typeof(TAttributeType));
             return value;
        }
    }
}

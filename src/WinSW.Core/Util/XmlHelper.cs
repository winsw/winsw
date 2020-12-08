using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Xml;

namespace WinSW.Util
{
    public class XmlHelper
    {
        /// <summary>
        /// Retrieves a single string element
        /// </summary>
        /// <param name="node">Parent node</param>
        /// <param name="tagName">Element name</param>
        /// <param name="optional">If optional, don't throw an exception if the element is missing</param>
        /// <returns>String value or null</returns>
        /// <exception cref="InvalidDataException">The required element is missing</exception>
        public static string? SingleElement(XmlNode node, string tagName, bool optional)
        {
            var n = node.SelectSingleNode(tagName);
            if (n is null && !optional)
            {
                throw new InvalidDataException("<" + tagName + "> is missing in configuration XML");
            }

            return n is null ? null : Environment.ExpandEnvironmentVariables(n.InnerText);
        }

        /// <summary>
        /// Retrieves a single node
        /// </summary>
        /// <param name="node">Parent node</param>
        /// <param name="tagName">Element name</param>
        /// <param name="optional">If otional, don't throw an exception if the element is missing</param>
        /// <returns>String value or null</returns>
        /// <exception cref="InvalidDataException">The required element is missing</exception>
        public static XmlNode? SingleNode(XmlNode node, string tagName, bool optional)
        {
            var n = node.SelectSingleNode(tagName);
            if (n is null && !optional)
            {
                throw new InvalidDataException("<" + tagName + "> is missing in configuration XML");
            }

            return n;
        }

        /// <summary>
        /// Retrieves a single mandatory attribute
        /// </summary>
        /// <param name="node">Parent node</param>
        /// <param name="attributeName">Attribute name</param>
        /// <returns>Attribute value</returns>
        /// <exception cref="InvalidDataException">The required attribute is missing</exception>
        public static TAttributeType SingleAttribute<TAttributeType>(XmlElement node, string attributeName)
        {
            if (!node.HasAttribute(attributeName))
            {
                throw new InvalidDataException("Attribute <" + attributeName + "> is missing in configuration XML");
            }

            return SingleAttribute<TAttributeType>(node, attributeName, default)!;
        }

        /// <summary>
        /// Retrieves a single optional attribute
        /// </summary>
        /// <param name="node">Parent node</param>
        /// <param name="attributeName">Attribute name</param>
        /// <param name="defaultValue">Default value</param>
        /// <returns>Attribute value (or default)</returns>
        [return: MaybeNull]
        public static TAttributeType SingleAttribute<TAttributeType>(XmlElement node, string attributeName, [AllowNull] TAttributeType defaultValue)
        {
            if (!node.HasAttribute(attributeName))
            {
                return defaultValue;
            }

            string rawValue = node.GetAttribute(attributeName);
            string substitutedValue = Environment.ExpandEnvironmentVariables(rawValue);
            var value = (TAttributeType)Convert.ChangeType(substitutedValue, typeof(TAttributeType));
            return value;
        }

        /// <summary>
        /// Retireves a single enum attribute
        /// </summary>
        /// <typeparam name="TAttributeType">Type of the enum</typeparam>
        /// <param name="node">Parent node</param>
        /// <param name="attributeName">Attribute name</param>
        /// <param name="defaultValue">Default value</param>
        /// <returns>Attribute value (or default)</returns>
        /// <exception cref="InvalidDataException">Wrong enum value</exception>
        public static TAttributeType EnumAttribute<TAttributeType>(XmlElement node, string attributeName, TAttributeType defaultValue)
            where TAttributeType : struct
        {
            if (!node.HasAttribute(attributeName))
            {
                return defaultValue;
            }

            string rawValue = node.GetAttribute(attributeName);
            string substitutedValue = Environment.ExpandEnvironmentVariables(rawValue);
#if NET20
            try
            {
                object value = Enum.Parse(typeof(TAttributeType), substitutedValue, true);
                return (TAttributeType)value;
            }
            catch (ArgumentException ex)
            {
                throw new InvalidDataException(
                    "Cannot parse <" + attributeName + "> Enum value from string '" + substitutedValue + "'. Enum type: " + typeof(TAttributeType), ex);
            }
#else
            if (!Enum.TryParse(substitutedValue, true, out TAttributeType result))
            {
                throw new InvalidDataException("Cannot parse <" + attributeName + "> Enum value from string '" + substitutedValue + "'. Enum type: " + typeof(TAttributeType));
            }

            return result;
#endif
        }
    }
}

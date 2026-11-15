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
            if (uncPath is null)
            {
                throw new ArgumentNullException(nameof(uncPath));
            }

            this.UNCPath = uncPath.TrimEnd('\\', '/');
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
                throw new InvalidDataException("SharedDirectoryMapperConfig mapping entry should be a dictionary");
            }

            string enableMappingConfig = GetRequiredYamlString(dict, "enabled");
            bool enableMapping = ConfigHelper.YamlBoolParse(enableMappingConfig);

            string label = NormalizeDriveLabel(GetRequiredYamlString(dict, "label"));
            string uncPath = GetRequiredYamlString(dict, "uncPath");

            return new SharedDirectoryMapperConfig(enableMapping, label, uncPath);
        }

        private static string NormalizeDriveLabel(string label)
        {
            label = label.Trim();
            if (label.Length == 1 && char.IsLetter(label[0]))
            {
                return label + ":";
            }

            return label;
        }

        private static string GetRequiredYamlString(Dictionary<object, object> dict, string key)
        {
            if (!TryGetYamlValue(dict, key, out object? rawValue))
            {
                throw new InvalidDataException($"SharedDirectoryMapperConfig mapping entry is missing '{key}'");
            }

            if (rawValue is null)
            {
                throw new InvalidDataException($"SharedDirectoryMapperConfig mapping entry key '{key}' cannot be null");
            }

            string? rawString = rawValue as string ?? rawValue.ToString();
            if (rawString is null)
            {
                throw new InvalidDataException($"SharedDirectoryMapperConfig mapping entry key '{key}' must be a scalar value");
            }

            return Environment.ExpandEnvironmentVariables(rawString);
        }

        private static bool TryGetYamlValue(Dictionary<object, object> dict, string key, out object? value)
        {
            if (dict.TryGetValue(key, out value))
            {
                return true;
            }

            bool found = false;
            object? foundValue = null;
            foreach (var entry in dict)
            {
                if (entry.Key is not string stringKey)
                {
                    continue;
                }

                if (!string.Equals(stringKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (found)
                {
                    throw new InvalidDataException($"SharedDirectoryMapperConfig mapping entry contains multiple '{key}' keys");
                }

                found = true;
                foundValue = entry.Value;
            }

            value = foundValue;
            return found;
        }
    }
}

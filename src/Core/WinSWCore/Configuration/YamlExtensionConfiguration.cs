using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;

namespace WinSW.Configuration
{
    public class YamlExtensionConfiguration
    {
        [YamlMember(Alias = "id")]
        public string? ExtensionId { get; set; }

        [YamlMember(Alias = "classname")]
        public string? ExtensionClassName { get; set; }

        [YamlMember(Alias = "enabled")]
        public bool Enabled { get; set; }

        [YamlMember(Alias = "settings")]
        public Dictionary<object, object>? Settings { get; set; }

        public string GetId()
        {
            if (this.ExtensionId is null)
            {
                throw new InvalidDataException("Extension Id can't be empty");
            }

            return this.ExtensionId;
        }

        public string GetClassName()
        {
            if (this.ExtensionClassName is null)
            {
                throw new InvalidDataException("Extension ClassName can't be empty");
            }

            return this.ExtensionClassName;
        }

        public Dictionary<object, object> GetSettings()
        {
            if (this.Settings is null)
            {
                throw new InvalidDataException("Extension settings not found");
            }

            return this.Settings;
        }
    }
}

using System.Collections.Generic;
using WinSW.Configuration;
using WinSW.Util;

namespace WinSW.Extensions
{
    public class ExtensionConfigurationProvider
    {
        private readonly IWinSWConfiguration serviceDescriptor;

        private readonly List<WinSWExtensionConfiguration> extensionConfigList;

        public ExtensionConfigurationProvider(IWinSWConfiguration serviceConfigs)
        {
            this.serviceDescriptor = serviceConfigs;
            this.extensionConfigList = this.CreateExtensionConfigList();
        }

        public WinSWExtensionConfiguration? GetExtenstionConfiguration(string id)
        {
            foreach (var item in this.extensionConfigList)
            {
                if (item.Id.Equals(id))
                {
                    return item;
                }
            }

            return null;
        }

        private List<WinSWExtensionConfiguration> CreateExtensionConfigList()
        {
            var result = new List<WinSWExtensionConfiguration>(0);

            var extensions = this.serviceDescriptor.ExtensionsConfiguration;

            if (extensions is null)
            {
                return result;
            }

            var extensionNodes = new ObjectQuery(extensions).AsList<object>();

            foreach (var extension in extensionNodes)
            {
                var query = new ObjectQuery(extension);

                var id = query.On("id").AsString();
                var enabled = query.On("enabled").AsBool();
                var className = query.On("classname").AsString();
                var settings = query.On("settings").AsParent();

                var extensionConfig = new WinSWExtensionConfiguration(id, enabled, className, settings);
                result.Add(extensionConfig);
            }

            return result;
        }
    }
}

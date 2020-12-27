using System.Collections.Generic;
using NUnit.Framework;
using WinSW;
using WinSW.Configuration;

namespace winswTests.Util
{
    public static class ServiceDescriptorAssert
    {
        // TODO: convert to Extension attributes once the .NET dependency is upgraded
        // BTW there is a way to get them working in .NET2, but KISS

        public static void AssertPropertyIsDefault(XmlServiceConfig desc, string property)
        {
            var actualProperty = typeof(XmlServiceConfig).GetProperty(property);
            Assert.That(actualProperty, Is.Not.Null);

            var defaultProperty = typeof(DefaultSettings).GetProperty(property);
            Assert.That(defaultProperty, Is.Not.Null);

            Assert.That(actualProperty.GetValue(desc, null), Is.EqualTo(defaultProperty.GetValue(XmlServiceConfig.Defaults, null)));
        }

        public static void AssertPropertyIsDefault(XmlServiceConfig desc, List<string> properties)
        {
            foreach (string prop in properties)
            {
                AssertPropertyIsDefault(desc, prop);
            }
        }

        public static void AssertAllOptionalPropertiesAreDefault(XmlServiceConfig desc)
        {
            AssertPropertyIsDefault(desc, AllOptionalProperties);
        }

        private static List<string> AllProperties
        {
            get
            {
                var res = new List<string>();
                var properties = typeof(IServiceConfig).GetProperties();
                foreach (var prop in properties)
                {
                    res.Add(prop.Name);
                }

                return res;
            }
        }

        private static List<string> AllOptionalProperties
        {
            get
            {
                var properties = AllProperties;
                properties.Remove(nameof(IServiceConfig.Name));
                properties.Remove(nameof(IServiceConfig.DisplayName));
                properties.Remove(nameof(IServiceConfig.Description));
                properties.Remove(nameof(IServiceConfig.Executable));
                properties.Remove(nameof(IServiceConfig.BaseName));
                properties.Remove(nameof(IServiceConfig.BasePath));
                properties.Remove(nameof(IServiceConfig.Log));
                properties.Remove(nameof(IServiceConfig.ServiceAccount));
                return properties;
            }
        }
    }
}

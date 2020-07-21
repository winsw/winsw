using System.Collections.Generic;
using System.Reflection;
using WinSW.Configuration;
using Xunit;

namespace WinSW.Tests.Util
{
    public static class ServiceDescriptorAssert
    {
        // TODO: convert to Extension attributes once the .NET dependency is upgraded
        // BTW there is a way to get them working in .NET2, but KISS
        public static void AssertPropertyIsDefault(ServiceDescriptor desc, string property)
        {
            PropertyInfo actualProperty = typeof(ServiceDescriptor).GetProperty(property);
            Assert.NotNull(actualProperty);

            PropertyInfo defaultProperty = typeof(DefaultWinSWSettings).GetProperty(property);
            Assert.NotNull(defaultProperty);

            Assert.Equal(defaultProperty.GetValue(ServiceDescriptor.Defaults, null), actualProperty.GetValue(desc, null));
        }

        public static void AssertPropertyIsDefault(ServiceDescriptor desc, List<string> properties)
        {
            foreach (var prop in properties)
            {
                AssertPropertyIsDefault(desc, prop);
            }
        }

        public static void AssertAllOptionalPropertiesAreDefault(ServiceDescriptor desc)
        {
            AssertPropertyIsDefault(desc, AllOptionalProperties);
        }

        private static List<string> AllProperties
        {
            get
            {
                var res = new List<string>();
                var properties = typeof(IWinSWConfiguration).GetProperties();
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
                properties.Remove("Id");
                properties.Remove("Caption");
                properties.Remove("Description");
                properties.Remove("Executable");
                return properties;
            }
        }
    }
}

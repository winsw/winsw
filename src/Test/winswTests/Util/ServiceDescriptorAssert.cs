using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using winsw;
using winsw.Configuration;

namespace winswTests.Util
{
    public static class ServiceDescriptorAssert
    {
        // TODO: convert to Extension attributes once the .NET dependency is upgraded
        // BTW there is a way to get them working in .NET2, but KISS

        public static void AssertPropertyIsDefault(ServiceDescriptor d, string property)
        {
            PropertyInfo actualProperty = typeof(ServiceDescriptor).GetProperty(property);
            Assert.IsNotNull(actualProperty, "Cannot find property " + property + " in the service descriptor" + d);
            PropertyInfo defaultProperty = typeof(DefaultWinSWSettings).GetProperty(property);
            Assert.IsNotNull(defaultProperty, "Cannot find property " + property + " in the default settings");

            Assert.AreEqual(defaultProperty.GetValue(ServiceDescriptor.Defaults, null), actualProperty.GetValue(d, null),
                "Value of property " + property + " does not equal to the default one");
        }

        public static void AssertPropertyIsDefault(ServiceDescriptor d, List<string> properties)
        {
            foreach (var prop in properties)
            {
                AssertPropertyIsDefault(d, prop);
            }
        }

        public static void AssertAllOptionalPropertiesAreDefault(ServiceDescriptor d)
        {
            AssertPropertyIsDefault(d, AllOptionalProperties);
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

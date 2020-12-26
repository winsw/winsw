﻿using System.Collections.Generic;
using NUnit.Framework;
using WinSW;
using WinSW.Configuration;

namespace winswTests.Util
{
    public static class ServiceDescriptorAssert
    {
        // TODO: convert to Extension attributes once the .NET dependency is upgraded
        // BTW there is a way to get them working in .NET2, but KISS

        public static void AssertPropertyIsDefault(ServiceDescriptor desc, string property)
        {
            var actualProperty = typeof(ServiceDescriptor).GetProperty(property);
            Assert.That(actualProperty, Is.Not.Null);

            var defaultProperty = typeof(DefaultWinSWSettings).GetProperty(property);
            Assert.That(defaultProperty, Is.Not.Null);

            Assert.That(actualProperty.GetValue(desc, null), Is.EqualTo(defaultProperty.GetValue(ServiceDescriptor.Defaults, null)));
        }

        public static void AssertPropertyIsDefault(ServiceDescriptor desc, List<string> properties)
        {
            foreach (string prop in properties)
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
                properties.Remove(nameof(IWinSWConfiguration.Name));
                properties.Remove(nameof(IWinSWConfiguration.DisplayName));
                properties.Remove(nameof(IWinSWConfiguration.Description));
                properties.Remove(nameof(IWinSWConfiguration.Executable));
                properties.Remove(nameof(IWinSWConfiguration.BaseName));
                properties.Remove(nameof(IWinSWConfiguration.BasePath));
                properties.Remove(nameof(IWinSWConfiguration.Log));
                properties.Remove(nameof(IWinSWConfiguration.ServiceAccount));
                return properties;
            }
        }
    }
}

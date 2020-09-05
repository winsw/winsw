using WinSW.Configuration;
using Xunit;

namespace WinSW.Tests.Util
{
    public static class ServiceConfigAssert
    {
        public static void AssertAllOptionalPropertiesAreDefault(XmlServiceConfig config)
        {
            var testConfig = new TestServiceConfig(config);
            foreach (var property in typeof(ServiceConfig).GetProperties())
            {
                if (property.GetMethod!.IsVirtual)
                {
                    Assert.Equal(property.GetValue(testConfig, null), property.GetValue(config, null));
                }
            }
        }

        private sealed class TestServiceConfig : ServiceConfig
        {
            private readonly XmlServiceConfig config;

            internal TestServiceConfig(XmlServiceConfig config) => this.config = config;

            public override string FullPath => this.config.FullPath;

            public override string Name => this.config.Name;

            public override string Executable => this.config.Executable;
        }
    }
}

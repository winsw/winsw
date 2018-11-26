using System;
using System.IO;
using System.Xml;
using winsw;
using winswTests.Util;
using Xunit;

namespace winswTests.Configuration
{
    /// <summary>
    /// Tests example configuration files.
    /// The test uses a relative path to example files, which is based on the current project structure.
    /// </summary>
    public class ExamplesTest
    {
        [Fact]
        public void AllOptionsConfigShouldDeclareDefaults()
        {
            ServiceDescriptor desc = Load("complete");

            Assert.Equal("myapp", desc.Id);
            Assert.Equal("MyApp Service (powered by WinSW)", desc.Caption);
            Assert.Equal("This service is a service created from a sample configuration", desc.Description);
            Assert.Equal("%BASE%\\myExecutable.exe", desc.Executable);

            ServiceDescriptorAssert.AssertAllOptionalPropertiesAreDefault(desc);
        }

        [Fact]
        public void MinimalConfigShouldDeclareDefaults()
        {
            ServiceDescriptor desc = Load("minimal");

            Assert.Equal("myapp", desc.Id);
            Assert.Equal("MyApp Service (powered by WinSW)", desc.Caption);
            Assert.Equal("This service is a service created from a minimal configuration", desc.Description);
            Assert.Equal("%BASE%\\myExecutable.exe", desc.Executable);

            ServiceDescriptorAssert.AssertAllOptionalPropertiesAreDefault(desc);
        }

        private static ServiceDescriptor Load(string exampleName)
        {
            string directory = Environment.CurrentDirectory;
            while (true)
            {
                if (File.Exists(Path.Combine(directory, ".gitignore")))
                {
                    break;
                }

                directory = Path.GetDirectoryName(directory);
                Assert.NotNull(directory);
            }

            string path = Path.Combine(directory, $@"samples\sample-{exampleName}.xml");
            Assert.True(File.Exists(path));

            XmlDocument dom = new XmlDocument();
            dom.Load(path);
            return new ServiceDescriptor(dom);
        }
    }
}

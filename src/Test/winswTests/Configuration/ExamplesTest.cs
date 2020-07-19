using System.IO;
using System.Reflection;
using System.Xml;
using NUnit.Framework;
using WinSW;
using winswTests.Util;

namespace winswTests.Configuration
{
    /// <summary>
    /// Tests example configuration files.
    /// The test uses a relative path to example files, which is based on the current project structure.
    /// </summary>
    [TestFixture]
    public class ExamplesTest
    {
        [Test]
        public void AllOptionsConfigShouldDeclareDefaults()
        {
            ServiceDescriptor desc = Load("allOptions");

            Assert.That(desc.Id, Is.EqualTo("myapp"));
            Assert.That(desc.Caption, Is.EqualTo("MyApp Service (powered by WinSW)"));
            Assert.That(desc.Description, Is.EqualTo("This service is a service created from a sample configuration"));
            Assert.That(desc.Executable, Is.EqualTo("%BASE%\\myExecutable.exe"));

            ServiceDescriptorAssert.AssertAllOptionalPropertiesAreDefault(desc);
        }

        [Test]
        public void MinimalConfigShouldDeclareDefaults()
        {
            ServiceDescriptor desc = Load("minimal");

            Assert.That(desc.Id, Is.EqualTo("myapp"));
            Assert.That(desc.Caption, Is.EqualTo("MyApp Service (powered by WinSW)"));
            Assert.That(desc.Description, Is.EqualTo("This service is a service created from a minimal configuration"));
            Assert.That(desc.Executable, Is.EqualTo("%BASE%\\myExecutable.exe"));

            ServiceDescriptorAssert.AssertAllOptionalPropertiesAreDefault(desc);
        }

        private static ServiceDescriptor Load(string exampleName)
        {
            string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            while (true)
            {
                if (File.Exists(Path.Combine(directory, ".gitignore")))
                {
                    break;
                }

                directory = Path.GetDirectoryName(directory);
                Assert.That(directory, Is.Not.Null);
            }

            string path = Path.Combine(directory, $@"examples\sample-{exampleName}.xml");
            Assert.That(path, Does.Exist);

            XmlDocument dom = new XmlDocument();
            dom.Load(path);
            return new ServiceDescriptor(dom);
        }
    }
}

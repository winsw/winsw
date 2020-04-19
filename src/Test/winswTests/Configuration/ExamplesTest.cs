using System.IO;
using System.Reflection;
using System.Xml;
using NUnit.Framework;
using winsw;
using winswTests.Util;

namespace winswTests.Configuration
{
    /// <summary>
    /// Tests example configuration files.
    /// The test uses a relative path to example files, which is based on the current project structure.
    /// </summary>
    [TestFixture]
    class ExamplesTest
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

        private ServiceDescriptor Load(string exampleName)
        {
            var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string path = Path.GetFullPath($@"{directory}\..\..\..\..\..\..\examples\sample-{exampleName}.xml");
            Assert.That(path, Does.Exist);

            XmlDocument dom = new XmlDocument();
            dom.Load(path);
            return new ServiceDescriptor(dom);
        }
    }
}

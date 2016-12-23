using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
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
        public void allOptionsConfigShouldDeclareDefaults()
        {
            ServiceDescriptor d = doLoad("allOptions");
            ServiceDescriptorAssert.AssertAllOptionalPropertiesAreDefault(d);
        }

        private ServiceDescriptor doLoad(string exampleName) {
            var dir = Directory.GetCurrentDirectory();
            string path = dir + "\\..\\..\\..\\..\\..\\examples\\config-" + exampleName + ".xml";
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Cannot find the XML file " + path, path);
            }
            XmlDocument dom = new XmlDocument();
            dom.Load(path);
            return new ServiceDescriptor(dom);
        }
    }
}

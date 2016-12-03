﻿using winsw;
using NUnit.Framework;
using winsw.Extensions;
using winsw.Plugins.SharedDirectoryMapper;
using winswTests.util;

namespace winswTests.Extensions
{
    [TestFixture]
    class SharedDirectoryMapperTest : ExtensionTestBase
    {
        ServiceDescriptor _testServiceDescriptor;
        readonly TestLogger _logger = new TestLogger();

        string testExtension = getExtensionClassNameWithAssembly(typeof(SharedDirectoryMapper));

        [SetUp]
        public void SetUp()
        {
            
            string seedXml = "<?xml version=\"1.0\" encoding=\"utf-8\" ?>"
                + "<service>                                                                                                        "
                + "  <id>SERVICE_NAME</id>                                                                                          "
                + "  <name>Jenkins Slave</name>                                                                                     "
                + "  <description>This service runs a slave for Jenkins continuous integration system.</description>                "
                + "  <executable>C:\\Program Files\\Java\\jre7\\bin\\java.exe</executable>                                               "
                + "  <arguments>-Xrs  -jar \\\"%BASE%\\slave.jar\\\" -jnlpUrl ...</arguments>                                              "
                + "  <logmode>rotate</logmode>                                                                                      "
                + "  <extensions>                                                                                                   "
                + "    <extension enabled=\"true\" className=\"" + testExtension + "\" id=\"mapNetworDirs\"> "
                + "      <mapping>                                                                                                  "
                + "        <map enabled=\"false\" label=\"N:\" uncpath=\"\\\\UNC\"/>                                                        "
                + "        <map enabled=\"false\" label=\"M:\" uncpath=\"\\\\UNC2\"/>                                                       "
                + "      </mapping>                                                                                                 "
                + "    </extension>         "
                + "    <extension enabled=\"true\" className=\"" + testExtension + "\" id=\"mapNetworDirs2\"> "
                + "      <mapping>                                                                                                  "
                + "        <map enabled=\"false\" label=\"X:\" uncpath=\"\\\\UNC\"/>                                                        "
                + "        <map enabled=\"false\" label=\"Y:\" uncpath=\"\\\\UNC2\"/>                                                       "
                + "      </mapping>                                                                                                 "
                + "    </extension>         "
                + "  </extensions>                                                                                                  "
                + "</service>";
            _testServiceDescriptor = ServiceDescriptor.FromXML(seedXml);
        }

        [Test]
        public void LoadExtensions()
        {
            WinSWExtensionManager manager = new WinSWExtensionManager(_testServiceDescriptor);
            manager.LoadExtensions(_logger);
            Assert.AreEqual(2, manager.Extensions.Count, "Two extensions should be loaded");
        }

        [Test]
        public void StartStopExtension()
        {
            WinSWExtensionManager manager = new WinSWExtensionManager(_testServiceDescriptor);
            manager.LoadExtensions(_logger);
            manager.OnStart(_logger);
            manager.OnStop(_logger);
        }
    }
}

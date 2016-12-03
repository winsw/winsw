using winsw;
using NUnit.Framework;
using winsw.Extensions;
using winsw.Plugins.SharedDirectoryMapper;
using winswTests.util;
using winsw.Plugins.RunawayProcessKiller;

namespace winswTests.Extensions
{
    [TestFixture]
    class RunawayProcessKillerExtensionTest : ExtensionTestBase
    {
        ServiceDescriptor _testServiceDescriptor;
        readonly TestLogger _logger = new TestLogger();

        string testExtension = getExtensionClassNameWithAssembly(typeof(RunawayProcessKillerExtension));
            
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
                + "    <extension enabled=\"true\" className=\"" + testExtension + "\" id=\"killRunawayProcess\"> "
                + "      <pidfile>foo/bar/pid.txt</pidfile>"
                + "      <stopTimeout>5000</stopTimeout> "
                + "      <stopParentFirst>true</stopParentFirst>"
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
            Assert.AreEqual(1, manager.Extensions.Count, "One extension should be loaded");

            // Check the file is correct
            var extension = manager.Extensions["killRunawayProcess"] as RunawayProcessKillerExtension;
            Assert.IsNotNull(extension, "RunawayProcessKillerExtension should be loaded");
            Assert.AreEqual("foo/bar/pid.txt", extension.Pidfile, "Loaded PID file path is not equal to the expected one");
            Assert.AreEqual(5000, extension.StopTimeout.TotalMilliseconds, "Loaded Stop Timeout is not equal to the expected one");
            Assert.AreEqual(true, extension.StopParentProcessFirst, "Loaded StopParentFirst is not equal to the expected one");
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
